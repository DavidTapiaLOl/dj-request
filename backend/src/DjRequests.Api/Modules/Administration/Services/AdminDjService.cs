using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Administration.Dtos;
using DjRequests.Api.Modules.Common;
using DjRequests.Api.Modules.Profiles.Dtos;
using DjRequests.Api.Modules.Profiles.Services;
using DjRequests.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DjRequests.Api.Modules.Administration.Services;

public sealed class AdminDjService
{
    public const int MaxSearchLength = 100;

    private const string LikeEscape = "\\";

    public static readonly string[] ValidStates = ["activo", "suspendido"];

    private readonly DjRequestsDbContext _context;
    private readonly IFileStorage _fileStorage;

    public AdminDjService(DjRequestsDbContext context, IFileStorage fileStorage)
    {
        _context = context;
        _fileStorage = fileStorage;
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
