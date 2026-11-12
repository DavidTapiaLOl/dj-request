using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Common;
using DjRequests.Api.Modules.Playlists.Dtos;
using DjRequests.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DjRequests.Api.Modules.Playlists.Services;

public enum PlaylistCreateStatus
{
    Created,
    IdempotentReplay,
    Unauthorized,
    RoomNotFound,
    RoomNotAvailable,
    QuotaExceeded,
    RateLimited,
    ValidationFailed,
    Conflict
}

public sealed record PlaylistCreateResult(
    PlaylistCreateStatus Status,
    PlaylistResponse? Response,
    string? ErrorCode,
    string? ErrorMessage,
    int? RetryAfterSeconds
);

public sealed record PlaylistListResult(
    bool Success,
    PlaylistPageResponse? Page,
    string? ErrorCode,
    string? ErrorMessage
);

public interface IPlaylistService
{
    Task<PlaylistCreateResult> CreateAsync(
        Guid roomId,
        string tokenHash,
        CreatePlaylistRequest request,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default);

    Task<PlaylistListResult> ListForDjAsync(
        Guid roomId,
        Guid djId,
        int page,
        int pageSize,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default);
}

public sealed class PlaylistService : IPlaylistService
{
    private readonly DjRequestsDbContext _context;

    public PlaylistService(DjRequestsDbContext context)
    {
        _context = context;
    }

    public async Task<PlaylistCreateResult> CreateAsync(
        Guid roomId,
        string tokenHash,
        CreatePlaylistRequest request,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        // Nombre opcional: trim + vacio -> null
        string? nombre = null;
        if (!string.IsNullOrWhiteSpace(request.Nombre))
        {
            nombre = request.Nombre.Trim();
            if (nombre.Length == 0) nombre = null;
            else if (nombre.Length > 200)
            {
                return new PlaylistCreateResult(
                    PlaylistCreateStatus.ValidationFailed, null,
                    "Nombre", "El nombre no puede superar los 200 caracteres.", null);
            }
        }

        // Enlace obligatorio: trim + validar URL absoluta HTTP/HTTPS con dominio
        if (string.IsNullOrWhiteSpace(request.Enlace))
        {
            return new PlaylistCreateResult(
                PlaylistCreateStatus.ValidationFailed, null,
                "Enlace", "El enlace de la playlist es obligatorio.", null);
        }

        var enlace = request.Enlace.Trim();
        if (enlace.Length == 0)
        {
            return new PlaylistCreateResult(
                PlaylistCreateStatus.ValidationFailed, null,
                "Enlace", "El enlace de la playlist es obligatorio.", null);
        }
        if (enlace.Length > 2048)
        {
            return new PlaylistCreateResult(
                PlaylistCreateStatus.ValidationFailed, null,
                "Enlace", "El enlace no puede superar los 2048 caracteres.", null);
        }
        if (!Uri.TryCreate(enlace, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return new PlaylistCreateResult(
                PlaylistCreateStatus.ValidationFailed, null,
                "Enlace", "El enlace debe ser una URL HTTP/HTTPS valida con dominio y sin credenciales.", null);
        }

        // Validar idempotencyKey
        if (request.IdempotencyKey == Guid.Empty)
        {
            return new PlaylistCreateResult(
                PlaylistCreateStatus.ValidationFailed, null,
                "IdempotencyKey", "La clave de idempotencia es obligatoria.", null);
        }

        // Transaccion con bloqueo: sala -> sesion (orden consistente)
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Bloquear sala
        var lockedSalas = await _context.Salas
            .FromSqlRaw("SELECT * FROM salas WHERE id = {0} FOR UPDATE", roomId)
            .Include(s => s.Dj)
            .ToListAsync(cancellationToken);

        var sala = lockedSalas.SingleOrDefault();

        if (sala is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PlaylistCreateResult(
                PlaylistCreateStatus.RoomNotFound, null, null, "Sala no encontrada.", null);
        }

        // Bloquear sesion
        var lockedSessions = await _context.SesionesInvitado
            .FromSqlRaw("SELECT * FROM sesiones_invitado WHERE sala_id = {0} AND token_hash = {1} FOR UPDATE", sala.Id, tokenHash)
            .ToListAsync(cancellationToken);

        var sesion = lockedSessions.SingleOrDefault();

        // La hora definitiva se toma despues de adquirir ambos bloqueos para
        // revalidar condiciones temporales que pudieron cambiar durante la espera.
        var now = timeProvider.GetUtcNow();

        if (sala.PurgeAt <= now || sala.Dj is null || sala.Dj.Estado != "activo")
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PlaylistCreateResult(
                PlaylistCreateStatus.RoomNotFound, null, null, "Sala no encontrada.", null);
        }

        if (now >= sala.EndsAt)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PlaylistCreateResult(
                PlaylistCreateStatus.RoomNotAvailable, null, null, "La sala ya ha finalizado.", null);
        }

        if (sala.Estado != "abierta")
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PlaylistCreateResult(
                PlaylistCreateStatus.RoomNotAvailable, null, null, "La sala no está abierta para nuevas playlists.", null);
        }

        if (sesion is null || sesion.RevokedAt is not null || now >= sesion.ExpiresAt)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PlaylistCreateResult(
                PlaylistCreateStatus.Unauthorized, null, null, "Credencial de invitado inválida, revocada o expirada.", null);
        }

        // Verificar idempotencia ANTES de consumir cuota
        var existingPlaylist = await _context.PlaylistsCompartidas
            .AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.SesionId == sesion.Id && p.IdempotencyKey == request.IdempotencyKey,
                cancellationToken);

        if (existingPlaylist is not null)
        {
            await transaction.RollbackAsync(cancellationToken);

            // Mismo payload -> devolver original sin consumir cuota
            bool mismoPayload =
                existingPlaylist.Nombre == nombre &&
                existingPlaylist.Enlace == enlace;

            if (mismoPayload)
            {
                var replayResponse = BuildPlaylistResponse(existingPlaylist);
                return new PlaylistCreateResult(
                    PlaylistCreateStatus.IdempotentReplay, replayResponse, null, null, null);
            }

            // Payload distinto -> conflicto
            return new PlaylistCreateResult(
                PlaylistCreateStatus.Conflict, null,
                "IdempotencyKey", "La clave de idempotencia ya fue usada con datos diferentes.", null);
        }

        // Verificar cuota: COUNT de playlists de la sesion
        var playlistCount = await _context.PlaylistsCompartidas
            .CountAsync(p => p.SesionId == sesion.Id, cancellationToken);

        if (playlistCount >= sala.MaxPlaylistsSesion)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PlaylistCreateResult(
                PlaylistCreateStatus.QuotaExceeded, null,
                "Quota", $"Se alcanzó el límite de {sala.MaxPlaylistsSesion} playlists por sesión.", null);
        }

        // Verificar intervalo compartido entre envios de contenido (canciones y playlists)
        if (sesion.UltimoEnvioAt.HasValue && sala.IntervaloEnvioSegundos > 0)
        {
            var elapsed = now - sesion.UltimoEnvioAt.Value;
            var required = TimeSpan.FromSeconds(sala.IntervaloEnvioSegundos);
            if (elapsed < required)
            {
                var retryAfter = (int)Math.Ceiling((required - elapsed).TotalSeconds);
                await transaction.RollbackAsync(cancellationToken);
                return new PlaylistCreateResult(
                    PlaylistCreateStatus.RateLimited, null,
                    "Interval", $"Debe esperar {sala.IntervaloEnvioSegundos} segundos entre envíos.", retryAfter);
            }
        }

        // Crear playlist
        var playlist = new PlaylistCompartida
        {
            Id = Guid.NewGuid(),
            SalaId = sala.Id,
            SesionId = sesion.Id,
            Nombre = nombre,
            Enlace = enlace,
            IdempotencyKey = request.IdempotencyKey,
            CreatedAt = now
        };

        _context.PlaylistsCompartidas.Add(playlist);

        // Actualizar el intervalo compartido de la sesion
        sesion.UltimoEnvioAt = now;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new PlaylistCreateResult(
                PlaylistCreateStatus.Conflict, null, null, "Conflicto de concurrencia. Intente de nuevo.", null);
        }

        var response = BuildPlaylistResponse(playlist);
        return new PlaylistCreateResult(PlaylistCreateStatus.Created, response, null, null, null);
    }

    public async Task<PlaylistListResult> ListForDjAsync(
        Guid roomId,
        Guid djId,
        int page,
        int pageSize,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        if (!PaginationValidation.TryValidate(page, pageSize, out var paginationError))
        {
            return new PlaylistListResult(false, null, "Pagination", paginationError);
        }

        var now = timeProvider.GetUtcNow();

        // Verificar propiedad de sala. Solo se exige vigencia (PurgeAt); el
        // propietario puede consultar las playlists dentro de la ventana de
        // retencion aunque el evento ya haya terminado.
        var salaExiste = await _context.Salas
            .AsNoTracking()
            .AnyAsync(s => s.Id == roomId && s.DjId == djId && s.PurgeAt > now, cancellationToken);

        if (!salaExiste)
        {
            return new PlaylistListResult(false, null, "RoomNotFound", "Sala no encontrada, ajena o caducada.");
        }

        var query = _context.PlaylistsCompartidas
            .AsNoTracking()
            .Where(p => p.SalaId == roomId)
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = PaginationValidation.GetOffset(page, pageSize);
        var items = await query
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var pageResponse = new PlaylistPageResponse(
            items.Select(BuildPlaylistResponse).ToList(),
            page,
            pageSize,
            totalCount,
            offset + items.Count < totalCount);

        return new PlaylistListResult(true, pageResponse, null, null);
    }

    private static PlaylistResponse BuildPlaylistResponse(PlaylistCompartida p)
    {
        return new PlaylistResponse(
            p.Id,
            p.Nombre,
            p.Enlace,
            p.CreatedAt
        );
    }
}
