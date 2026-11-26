using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Administration.Dtos;
using DjRequests.Api.Modules.Common;
using DjRequests.Api.Modules.Profiles.Dtos;
using DjRequests.Api.Modules.Profiles.Services;
using System.Data;
using DjRequests.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DjRequests.Api.Modules.Administration.Services;

public enum AdminDjStateUpdateStatus
{
    Success,
    Unchanged,
    NotFound,
    Conflict
}

public sealed record AdminDjStateUpdateResult(
    AdminDjStateUpdateStatus Status,
    Guid Id,
    string Estado);

public sealed class AdminDjService
{
    public const int MaxSearchLength = 100;

    private const string LikeEscape = "\\";

    public static readonly string[] ValidStates = ["activo", "suspendido"];

    private readonly DjRequestsDbContext _context;
    private readonly IFileStorage _fileStorage;
    private readonly UserManager<DjAccount> _djUserManager;

    public AdminDjService(
        DjRequestsDbContext context,
        IFileStorage fileStorage,
        UserManager<DjAccount> djUserManager)
    {
        _context = context;
        _fileStorage = fileStorage;
        _djUserManager = djUserManager;
    }

    public static bool IsValidEstado(string estado) => ValidStates.Contains(estado);

    public async Task<AdminDjPageResponse> ListAsync(
        string? search,
        string? estado,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<DjAccount> query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = BuildLikePattern(search);
            query = query.Where(d =>
                EF.Functions.ILike(d.Email!, pattern, LikeEscape) ||
                EF.Functions.ILike(d.NombreArtistico, pattern, LikeEscape));
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            query = query.Where(d => d.Estado == estado);
        }

        var ordered = query
            .OrderByDescending(d => d.CreatedAt)
            .ThenBy(d => d.Id);

        var totalCount = await ordered.CountAsync(cancellationToken);
        var offset = PaginationValidation.GetOffset(page, pageSize);

        var items = await ordered
            .Skip((int)offset)
            .Take(pageSize)
            .Select(d => new AdminDjListItemDto(
                d.Id,
                d.Email!,
                d.NombreArtistico,
                d.Estado,
                d.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AdminDjPageResponse(
            items,
            page,
            pageSize,
            totalCount,
            offset + items.Count < totalCount);
    }

    public async Task<AdminDjDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dj = await _context.Users
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new AdminDjListItemDto(
                d.Id,
                d.Email!,
                d.NombreArtistico,
                d.Estado,
                d.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        if (dj is null)
        {
            return null;
        }

        var profile = await _context.PerfilesDj
            .AsNoTracking()
            .Where(p => p.DjId == id)
            .Select(p => new AdminDjProfileData(
                p.Descripcion,
                p.AnioInicio,
                p.FotoNombreArchivo,
                p.RedInstagram,
                p.RedFacebook,
                p.RedTiktok,
                p.RedYoutube,
                p.RedSoundcloud))
            .SingleOrDefaultAsync(cancellationToken);

        var photoUrl = profile?.FotoNombreArchivo is { Length: > 0 } fileName
            ? _fileStorage.GetPublicUrl(fileName)
            : null;

        return new AdminDjDto(
            dj.Id,
            dj.Email,
            dj.ArtistName,
            dj.Estado,
            dj.CreatedAt,
            profile?.Descripcion,
            profile?.AnioInicio,
            photoUrl,
            new SocialLinksDto(
                profile?.Instagram,
                profile?.Facebook,
                profile?.Tiktok,
                profile?.Youtube,
                profile?.Soundcloud));
    }

    public async Task<AdminDjStateUpdateResult> SetStateAsync(
        Guid djId,
        string requestedEstado,
        string expectedEstado,
        Guid administratorId,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidEstado(requestedEstado) || !IsValidEstado(expectedEstado))
        {
            throw new ArgumentException(
                $"Estados permitidos: {string.Join(", ", ValidStates)}.",
                nameof(requestedEstado));
        }

        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        _context.Database.UseTransaction(transaction);

        await using var adminContext = new AdminDbContext(
            new DbContextOptionsBuilder<AdminDbContext>()
                .UseNpgsql(
                    connection,
                    npgsql => npgsql.MigrationsHistoryTable(AdminDbContext.MigrationsHistoryTable))
                .Options);
        adminContext.Database.UseTransaction(transaction);

        var dj = await _context.Users
            .FromSqlRaw("SELECT * FROM djs WHERE \"Id\" = {0} FOR UPDATE", djId)
            .SingleOrDefaultAsync(cancellationToken);

        if (dj is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AdminDjStateUpdateResult(AdminDjStateUpdateStatus.NotFound, djId, requestedEstado);
        }

        if (!string.Equals(dj.Estado, expectedEstado, StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new AdminDjStateUpdateResult(AdminDjStateUpdateStatus.Conflict, dj.Id, dj.Estado);
        }

        if (string.Equals(dj.Estado, requestedEstado, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return new AdminDjStateUpdateResult(AdminDjStateUpdateStatus.Unchanged, dj.Id, dj.Estado);
        }

        await _context.Salas
            .FromSqlRaw("SELECT * FROM salas WHERE dj_id = {0} FOR UPDATE", djId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        dj.Estado = requestedEstado;
        dj.UpdatedAt = now;

        var stampResult = await _djUserManager.UpdateSecurityStampAsync(dj);
        if (!stampResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new InvalidOperationException(
                $"No se pudo renovar el security stamp del DJ: {string.Join("; ", stampResult.Errors.Select(e => e.Description))}");
        }

        await _context.Database.ExecuteSqlRawAsync(
            """
            UPDATE sesiones_invitado
            SET revoked_at = @now
            WHERE revoked_at IS NULL
              AND sala_id IN (SELECT id FROM salas WHERE dj_id = @djId)
            """,
            new object[]
            {
                new NpgsqlParameter("now", now),
                new NpgsqlParameter("djId", djId)
            },
            cancellationToken);

        adminContext.AdminAudits.Add(new AuditoriaAdmin
        {
            Id = Guid.NewGuid(),
            AdministratorId = administratorId,
            Action = requestedEstado == "suspendido" ? "DJ_Suspendido" : "DJ_Reactivado",
            ResourceType = "DJ",
            ResourceId = dj.Id,
            Result = "Exitoso",
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);
        await adminContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdminDjStateUpdateResult(AdminDjStateUpdateStatus.Success, dj.Id, dj.Estado);
    }

    private static string BuildLikePattern(string search)
    {
        var escaped = search
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }

    private sealed record AdminDjProfileData(
        string? Descripcion,
        int? AnioInicio,
        string? FotoNombreArchivo,
        string? Instagram,
        string? Facebook,
        string? Tiktok,
        string? Youtube,
        string? Soundcloud);
}
