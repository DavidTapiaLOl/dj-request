using DjRequests.Api.Entities;
using DjRequests.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DjRequests.Api.Modules.Guests.Services;

public enum GuestSessionStatus
{
    Valid,
    Unauthorized,
    RoomNotFound
}

public sealed record GuestSessionValidationResult(
    GuestSessionStatus Status,
    SesionInvitado? Session,
    Sala? Sala);

public interface IGuestSessionService
{
    Task<GuestSessionValidationResult> ValidateSessionAsync(
        string tokenHash,
        Guid roomId,
        TimeProvider timeProvider,
        DjRequestsDbContext context,
        CancellationToken cancellationToken = default);
}

public sealed class GuestSessionService : IGuestSessionService
{
    public async Task<GuestSessionValidationResult> ValidateSessionAsync(
        string tokenHash,
        Guid roomId,
        TimeProvider timeProvider,
        DjRequestsDbContext context,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();

        var session = await context.SesionesInvitado
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

        if (session is null)
        {
            return new GuestSessionValidationResult(GuestSessionStatus.Unauthorized, null, null);
        }

        if (session.RevokedAt is not null)
        {
            return new GuestSessionValidationResult(GuestSessionStatus.Unauthorized, session, null);
        }

        if (now >= session.ExpiresAt)
        {
            return new GuestSessionValidationResult(GuestSessionStatus.Unauthorized, session, null);
        }

        var sala = await context.Salas
            .AsNoTracking()
            .Include(s => s.Dj)
            .SingleOrDefaultAsync(s => s.Id == session.SalaId, cancellationToken);

        if (sala is null)
        {
            return new GuestSessionValidationResult(GuestSessionStatus.RoomNotFound, session, null);
        }

        if (sala.Id != roomId)
        {
            return new GuestSessionValidationResult(GuestSessionStatus.RoomNotFound, session, sala);
        }

        if (now >= sala.EndsAt || now >= sala.PurgeAt)
        {
            return new GuestSessionValidationResult(GuestSessionStatus.RoomNotFound, session, sala);
        }

        if (sala.Dj is null || sala.Dj.Estado != "activo")
        {
            return new GuestSessionValidationResult(GuestSessionStatus.RoomNotFound, session, sala);
        }

        return new GuestSessionValidationResult(GuestSessionStatus.Valid, session, sala);
    }
}