using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Common;
using DjRequests.Api.Modules.Requests.Dtos;
using DjRequests.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DjRequests.Api.Modules.Requests.Services;

public enum SolicitudCreateStatus
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

public sealed record SolicitudCreateResult(
    SolicitudCreateStatus Status,
    SolicitudResponse? Response,
    string? ErrorCode,
    string? ErrorMessage,
    int? RetryAfterSeconds
);

public sealed record SolicitudListResult(
    bool Success,
    SolicitudPageResponse? Page,
    string? ErrorCode,
    string? ErrorMessage
);

public sealed record SolicitudUpdateResult(
    bool Success,
    SolicitudResponse? Response,
    string? ErrorCode,
    string? ErrorMessage
);

public interface ISolicitudService
{
    Task<SolicitudCreateResult> CreateAsync(
        Guid roomId,
        string tokenHash,
        CreateSolicitudRequest request,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default);

    Task<SolicitudListResult> ListForGuestAsync(
        Guid roomId,
        string tokenHash,
        int page,
        int pageSize,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default);

    Task<SolicitudListResult> ListForDjAsync(
        Guid roomId,
        Guid djId,
        string? estado,
        short? generoId,
        int page,
        int pageSize,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default);

    Task<SolicitudUpdateResult> UpdateStateAsync(
        Guid roomId,
        Guid solicitudId,
        Guid djId,
        UpdateSolicitudStateRequest request,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default);
}

public sealed class SolicitudService : ISolicitudService
{
    private static readonly string[] ValidStates = { "pendiente", "aceptada", "reproducida", "rechazada" };

    private static readonly Dictionary<string, string[]> AllowedTransitions = new()
    {
        ["pendiente"] = new[] { "aceptada", "rechazada" },
        ["aceptada"] = new[] { "reproducida", "rechazada" }
    };

    private readonly DjRequestsDbContext _context;

    public SolicitudService(DjRequestsDbContext context)
    {
        _context = context;
    }

    public async Task<SolicitudCreateResult> CreateAsync(
        Guid roomId,
        string tokenHash,
        CreateSolicitudRequest request,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        // Validar nombre obligatorio
        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            return new SolicitudCreateResult(
                SolicitudCreateStatus.ValidationFailed, null,
                "Nombre", "El nombre de la canción es obligatorio.", null);
        }

        var nombre = request.Nombre.Trim();
        if (nombre.Length == 0)
        {
            return new SolicitudCreateResult(
                SolicitudCreateStatus.ValidationFailed, null,
                "Nombre", "El nombre de la canción no puede ser solo espacios.", null);
        }
        if (nombre.Length > 200)
        {
            return new SolicitudCreateResult(
                SolicitudCreateStatus.ValidationFailed, null,
                "Nombre", "El nombre no puede superar los 200 caracteres.", null);
        }

        // Normalizar artista: trim + vacío -> null
        string? artista = null;
        if (!string.IsNullOrWhiteSpace(request.Artista))
        {
            artista = request.Artista.Trim();
            if (artista.Length > 200)
            {
                return new SolicitudCreateResult(
                    SolicitudCreateStatus.ValidationFailed, null,
                    "Artista", "El artista no puede superar los 200 caracteres.", null);
            }
            if (artista.Length == 0) artista = null;
        }

        // Normalizar enlace: trim + vacío -> null + validar HTTP/HTTPS
        string? enlace = null;
        if (!string.IsNullOrWhiteSpace(request.Enlace))
        {
            enlace = request.Enlace.Trim();
            if (enlace.Length > 2048)
            {
                return new SolicitudCreateResult(
                    SolicitudCreateStatus.ValidationFailed, null,
                    "Enlace", "El enlace no puede superar los 2048 caracteres.", null);
            }
            if (enlace.Length == 0)
            {
                enlace = null;
            }
            else if (!Uri.TryCreate(enlace, UriKind.Absolute, out var uri)
                     || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return new SolicitudCreateResult(
                    SolicitudCreateStatus.ValidationFailed, null,
                    "Enlace", "El enlace debe ser una URL HTTP/HTTPS válida.", null);
            }
        }

        // Validar idempotencyKey
        if (request.IdempotencyKey == Guid.Empty)
        {
            return new SolicitudCreateResult(
                SolicitudCreateStatus.ValidationFailed, null,
                "IdempotencyKey", "La clave de idempotencia es obligatoria.", null);
        }

        // Validar género si se informa
        if (request.GeneroId.HasValue)
        {
            var generoExiste = await _context.Generos
                .AnyAsync(g => g.Id == request.GeneroId.Value && g.Activo, cancellationToken);
            if (!generoExiste)
            {
                return new SolicitudCreateResult(
                    SolicitudCreateStatus.ValidationFailed, null,
                    "GeneroId", "El género informado no existe o no está activo.", null);
            }
        }

        // Transacción con bloqueo: sala -> sesión (orden consistente)
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
            return new SolicitudCreateResult(
                SolicitudCreateStatus.RoomNotFound, null, null, "Sala no encontrada.", null);
        }

        // Bloquear sesión
        var lockedSessions = await _context.SesionesInvitado
            .FromSqlRaw("SELECT * FROM sesiones_invitado WHERE sala_id = {0} AND token_hash = {1} FOR UPDATE", sala.Id, tokenHash)
            .ToListAsync(cancellationToken);

        var sesion = lockedSessions.SingleOrDefault();

        // La hora definitiva se toma después de adquirir ambos bloqueos para
        // revalidar condiciones temporales que pudieron cambiar durante la espera.
        var now = timeProvider.GetUtcNow();

        if (sala.PurgeAt <= now || sala.Dj is null || sala.Dj.Estado != "activo")
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SolicitudCreateResult(
                SolicitudCreateStatus.RoomNotFound, null, null, "Sala no encontrada.", null);
        }

        if (now >= sala.EndsAt)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SolicitudCreateResult(
                SolicitudCreateStatus.RoomNotAvailable, null, null, "La sala ya ha finalizado.", null);
        }

        if (sala.Estado != "abierta")
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SolicitudCreateResult(
                SolicitudCreateStatus.RoomNotAvailable, null, null, "La sala no está abierta para nuevas solicitudes.", null);
        }

        if (sesion is null || sesion.RevokedAt is not null || now >= sesion.ExpiresAt)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SolicitudCreateResult(
                SolicitudCreateStatus.Unauthorized, null, null, "Credencial de invitado inválida, revocada o expirada.", null);
        }

        // Verificar idempotencia ANTES de consumir cuota
        var existingSolicitud = await _context.Solicitudes
            .AsNoTracking()
            .Include(s => s.Genero)
            .SingleOrDefaultAsync(s => s.SesionId == sesion.Id && s.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (existingSolicitud is not null)
        {
            await transaction.RollbackAsync(cancellationToken);

            // Mismo payload -> devolver original sin consumir cuota
            bool mismoPayload =
                existingSolicitud.Nombre == nombre &&
                existingSolicitud.Artista == artista &&
                existingSolicitud.GeneroId == request.GeneroId &&
                existingSolicitud.Enlace == enlace;

            if (mismoPayload)
            {
                var replayResponse = BuildSolicitudResponse(existingSolicitud);
                return new SolicitudCreateResult(
                    SolicitudCreateStatus.IdempotentReplay, replayResponse, null, null, null);
            }

            // Payload distinto -> conflicto
            return new SolicitudCreateResult(
                SolicitudCreateStatus.Conflict, null,
                "IdempotencyKey", "La clave de idempotencia ya fue usada con datos diferentes.", null);
        }

        // Verificar cuota: COUNT de solicitudes de la sesión (incluye rechazadas)
        var solicitudCount = await _context.Solicitudes
            .CountAsync(s => s.SesionId == sesion.Id, cancellationToken);

        if (solicitudCount >= sala.MaxSolicitudesSesion)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SolicitudCreateResult(
                SolicitudCreateStatus.QuotaExceeded, null,
                "Quota", $"Se alcanzó el límite de {sala.MaxSolicitudesSesion} solicitudes por sesión.", null);
        }

        // Verificar intervalo entre envíos
        if (sesion.UltimoEnvioAt.HasValue && sala.IntervaloEnvioSegundos > 0)
        {
            var elapsed = now - sesion.UltimoEnvioAt.Value;
            var required = TimeSpan.FromSeconds(sala.IntervaloEnvioSegundos);
            if (elapsed < required)
            {
                var retryAfter = (int)Math.Ceiling((required - elapsed).TotalSeconds);
                await transaction.RollbackAsync(cancellationToken);
                return new SolicitudCreateResult(
                    SolicitudCreateStatus.RateLimited, null,
                    "Interval", $"Debe esperar {sala.IntervaloEnvioSegundos} segundos entre envíos.", retryAfter);
            }
        }

        // Crear solicitud
        var solicitud = new Solicitud
        {
            Id = Guid.NewGuid(),
            SalaId = sala.Id,
            SesionId = sesion.Id,
            Nombre = nombre,
            Artista = artista,
            GeneroId = request.GeneroId,
            Enlace = enlace,
            Estado = "pendiente",
            IdempotencyKey = request.IdempotencyKey,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.Solicitudes.Add(solicitud);

        // Actualizar último envío de la sesión
        sesion.UltimoEnvioAt = now;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SolicitudCreateResult(
                SolicitudCreateStatus.Conflict, null, null, "Conflicto de concurrencia. Intente de nuevo.", null);
        }

        // Recargar con género para respuesta
        await _context.Entry(solicitud).Reference(s => s.Genero).LoadAsync(cancellationToken);

        var response = BuildSolicitudResponse(solicitud);
        return new SolicitudCreateResult(SolicitudCreateStatus.Created, response, null, null, null);
    }

    public async Task<SolicitudListResult> ListForGuestAsync(
        Guid roomId,
        string tokenHash,
        int page,
        int pageSize,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        if (!PaginationValidation.TryValidate(page, pageSize, out var paginationError))
        {
            return new SolicitudListResult(false, null, "Pagination", paginationError);
        }

        var now = timeProvider.GetUtcNow();

        // Validar sesión
        var session = await _context.SesionesInvitado
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

        if (session is null || session.RevokedAt is not null || now >= session.ExpiresAt)
        {
            return new SolicitudListResult(false, null, "Unauthorized", "Credencial de invitado inválida.");
        }

        var sala = await _context.Salas
            .AsNoTracking()
            .Include(s => s.Dj)
            .SingleOrDefaultAsync(s => s.Id == roomId && s.Id == session.SalaId, cancellationToken);

        if (sala is null || sala.PurgeAt <= now || sala.Dj is null || sala.Dj.Estado != "activo")
        {
            return new SolicitudListResult(false, null, "RoomNotFound", "Sala no encontrada.");
        }

        // Todos los invitados de la sala ven el contenido de esa sala.
        var query = _context.Solicitudes
            .AsNoTracking()
            .Where(s => s.SalaId == roomId)
            .Include(s => s.Genero)
            .OrderByDescending(s => s.CreatedAt)
            .ThenBy(s => s.Id);

        var totalCount = await query.CountAsync(cancellationToken);
        var offset = PaginationValidation.GetOffset(page, pageSize);
        var items = await query
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var pageResponse = new SolicitudPageResponse(
            items.Select(BuildSolicitudResponse).ToList(),
            page,
            pageSize,
            totalCount,
            offset + items.Count < totalCount);

        return new SolicitudListResult(true, pageResponse, null, null);
    }

    public async Task<SolicitudListResult> ListForDjAsync(
        Guid roomId,
        Guid djId,
        string? estado,
        short? generoId,
        int page,
        int pageSize,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        if (!PaginationValidation.TryValidate(page, pageSize, out var paginationError))
        {
            return new SolicitudListResult(false, null, "Pagination", paginationError);
        }

        var now = timeProvider.GetUtcNow();

        // Verificar propiedad de sala
        var sala = await _context.Salas
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == roomId && s.DjId == djId && s.PurgeAt > now, cancellationToken);

        if (sala is null)
        {
            return new SolicitudListResult(false, null, "RoomNotFound", "Sala no encontrada, ajena o caducada.");
        }

        IQueryable<Solicitud> query = _context.Solicitudes
            .AsNoTracking()
            .Where(s => s.SalaId == roomId)
            .Include(s => s.Genero);

        if (!string.IsNullOrWhiteSpace(estado))
        {
            var estadoTrimmed = estado.Trim().ToLowerInvariant();
            if (ValidStates.Contains(estadoTrimmed))
            {
                query = query.Where(s => s.Estado == estadoTrimmed);
            }
        }

        if (generoId.HasValue)
        {
            query = query.Where(s => s.GeneroId == generoId.Value);
        }

        var orderedQuery = query.OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Id);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);
        var offset = PaginationValidation.GetOffset(page, pageSize);
        var items = await orderedQuery
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var pageResponse = new SolicitudPageResponse(
            items.Select(BuildSolicitudResponse).ToList(),
            page,
            pageSize,
            totalCount,
            offset + items.Count < totalCount);

        return new SolicitudListResult(true, pageResponse, null, null);
    }

    public async Task<SolicitudUpdateResult> UpdateStateAsync(
        Guid roomId,
        Guid solicitudId,
        Guid djId,
        UpdateSolicitudStateRequest request,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        // Validar estado
        if (string.IsNullOrWhiteSpace(request.Estado))
        {
            return new SolicitudUpdateResult(false, null, "Estado", "El estado es obligatorio.");
        }

        var nuevoEstado = request.Estado.Trim().ToLowerInvariant();
        if (!ValidStates.Contains(nuevoEstado))
        {
            return new SolicitudUpdateResult(false, null, "Estado", "Estado inválido. Estados permitidos: pendiente, aceptada, reproducida, rechazada.");
        }

        if (request.Version <= 0)
        {
            return new SolicitudUpdateResult(false, null, "Version", "La versión debe ser un entero positivo.");
        }

        // Transacción con bloqueo: sala -> solicitud (orden consistente)
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Bloquear sala
        var lockedSalas = await _context.Salas
            .FromSqlRaw("SELECT * FROM salas WHERE id = {0} FOR UPDATE", roomId)
            .ToListAsync(cancellationToken);

        var sala = lockedSalas.SingleOrDefault();

        if (sala is null || sala.DjId != djId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SolicitudUpdateResult(false, null, "RoomNotFound", "Sala no encontrada, ajena o caducada.");
        }

        // Bloquear solicitud
        var lockedSolicitudes = await _context.Solicitudes
            .FromSqlRaw("SELECT * FROM solicitudes WHERE id = {0} FOR UPDATE", solicitudId)
            .Include(s => s.Genero)
            .ToListAsync(cancellationToken);

        var solicitud = lockedSolicitudes.SingleOrDefault();

        if (solicitud is null || solicitud.SalaId != roomId)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SolicitudUpdateResult(false, null, "SolicitudNotFound", "Solicitud no encontrada.");
        }

        // La hora definitiva se toma después de adquirir ambos bloqueos para
        // revalidar la caducidad de la sala que pudo cambiar durante la espera.
        var now = timeProvider.GetUtcNow();

        if (sala.PurgeAt <= now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SolicitudUpdateResult(false, null, "RoomNotFound", "Sala no encontrada, ajena o caducada.");
        }

        // Revalidar versión bajo bloqueo
        if (solicitud.Version != request.Version)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SolicitudUpdateResult(false, null, "VersionObsoleta", "La versión de la solicitud no coincide con la versión actual.");
        }

        // Validar transición
        if (!AllowedTransitions.TryGetValue(solicitud.Estado, out var allowed))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SolicitudUpdateResult(false, null, "Transition",
                $"No se puede cambiar desde el estado '{solicitud.Estado}'.");
        }

        if (!allowed.Contains(nuevoEstado))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SolicitudUpdateResult(false, null, "Transition",
                $"Transición inválida: '{solicitud.Estado}' → '{nuevoEstado}'. Permitidos: {string.Join(", ", allowed)}.");
        }

        // Aplicar cambio
        solicitud.Estado = nuevoEstado;
        solicitud.Version = request.Version + 1;
        solicitud.UpdatedAt = now;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new SolicitudUpdateResult(false, null, "Concurrencia", "Conflicto de concurrencia. La solicitud fue modificada por otra operación.");
        }

        var response = BuildSolicitudResponse(solicitud);
        return new SolicitudUpdateResult(true, response, null, null);
    }

    private static SolicitudResponse BuildSolicitudResponse(Solicitud s)
    {
        return new SolicitudResponse(
            s.Id,
            s.Nombre,
            s.Artista,
            s.GeneroId,
            s.Genero?.Nombre,
            s.Enlace,
            s.Estado,
            s.Version,
            s.CreatedAt,
            s.UpdatedAt
        );
    }
}
