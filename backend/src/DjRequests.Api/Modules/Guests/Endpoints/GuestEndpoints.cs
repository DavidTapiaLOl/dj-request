using System.Security.Cryptography;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Guests.Dtos;
using DjRequests.Api.Modules.Guests.Options;
using DjRequests.Api.Modules.Guests.Services;
using DjRequests.Api.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DjRequests.Api.Modules.Guests.Endpoints;

public static class GuestEndpoints
{
    private const int MaxTokenAttempts = 5;
    private const string TokenHashUniqueIndexName = "IX_sesiones_invitado_token_hash";
    private const string PostgresUniqueViolationSqlState = "23505";
    private const string GuestTokenHeaderName = "X-Guest-Token";
    private const int TokenByteLength = 32;
    private const int TokenBase64UrlLength = 43;

    public static void MapGuestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/guest")
            .WithTags("Guests");

        group.MapPost("/sessions", CreateSessionAsync)
            .WithName("CreateGuestSession")
            .RequireRateLimiting("guest-create")
            .Produces<GuestSessionResponse>(StatusCodes.Status201Created)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapGet("/rooms/{roomId:guid}", GetRoomAsync)
            .WithName("GetGuestRoom")
            .RequireRateLimiting("guest-get")
            .Produces<GuestRoomResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static bool IsValidCode(string? code)
    {
        if (code is null || code.Length != 10) return false;
        foreach (var c in code)
        {
            if (!char.IsUpper(c) && !(c >= '2' && c <= '9')) return false;
            if (c == 'I' || c == 'O') return false;
        }
        return true;
    }

    private static bool IsValidBase64UrlToken(string token, out byte[] bytes)
    {
        bytes = null!;
        if (string.IsNullOrWhiteSpace(token) || token.Length != TokenBase64UrlLength)
        {
            return false;
        }

        try
        {
            var base64 = token.Replace('-', '+').Replace('_', '/');
            var padding = 4 - (base64.Length % 4);
            if (padding != 4) base64 += new string('=', padding);
            bytes = Convert.FromBase64String(base64);
            return bytes.Length == TokenByteLength;
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeTokenHash(byte[] tokenBytes)
    {
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static RoomMinimalDto ToRoomMinimalDto(Sala sala, DateTimeOffset now)
    {
        return new RoomMinimalDto(
            sala.Id,
            sala.Nombre,
            sala.Estado,
            sala.StartsAt,
            sala.EndsAt,
            sala.Timezone,
            sala.Estado == "abierta" && now < sala.EndsAt && now < sala.PurgeAt,
            sala.MaxSolicitudesSesion,
            sala.MaxPlaylistsSesion,
            sala.IntervaloEnvioSegundos
        );
    }

    #region POST /api/guest/sessions

    private static async Task<IResult> CreateSessionAsync(
        CreateGuestSessionRequest request,
        IGuestTokenGenerator tokenGenerator,
        DjRequestsDbContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim().ToUpperInvariant();

        if (!IsValidCode(code))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Code"] = new[] { "Codigo de sala invalido. Debe tener 10 caracteres (A-H, J-N, P-Z, 2-9)." }
            });
        }

        var sala = await context.Salas
            .Include(s => s.Dj)
            .SingleOrDefaultAsync(s => s.Codigo == code, cancellationToken);

        if (sala is null)
        {
            return Results.Problem(
                detail: "Sala no encontrada.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Sala no encontrada",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var lockedSalas = await context.Salas
            .FromSqlRaw("SELECT * FROM salas WHERE id = {0} FOR UPDATE", sala.Id)
            .Include(s => s.Dj)
            .ToListAsync(cancellationToken);

        var lockedSala = lockedSalas.SingleOrDefault();

        var now = timeProvider.GetUtcNow();

        if (lockedSala is null || lockedSala.Dj is null || lockedSala.PurgeAt <= now || lockedSala.Dj.Estado != "activo")
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(
                detail: "Sala no encontrada.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Sala no encontrada",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5");
        }

        if (lockedSala.EndsAt <= now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(
                detail: "La sala ya ha finalizado.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Sala finalizada",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5");
        }

        if (lockedSala.Estado != "abierta")
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(
                detail: "La sala no esta abierta para nuevas sesiones.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Sala no disponible",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10");
        }

        SesionInvitado? sesion = null;
        string? generatedToken = null;
        for (int attempt = 0; attempt < MaxTokenAttempts; attempt++)
        {
            var (token, tokenHash) = tokenGenerator.GenerateToken();

            var entity = new SesionInvitado
            {
                Id = Guid.NewGuid(),
                SalaId = lockedSala.Id,
                TokenHash = tokenHash,
                CreatedAt = now,
                ExpiresAt = lockedSala.EndsAt,
                RevokedAt = null,
                UltimoEnvioAt = null
            };

            context.SesionesInvitado.Add(entity);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                sesion = entity;
                generatedToken = token;
                break;
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is PostgresException pgEx
                && pgEx.SqlState == PostgresUniqueViolationSqlState
                && pgEx.ConstraintName == TokenHashUniqueIndexName)
            {
                context.Entry(entity).State = EntityState.Detached;
            }
        }

        if (sesion is null || generatedToken is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Problem(
                detail: "No se pudo generar un token unico tras varios intentos.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Token no disponible",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10");
        }

        await transaction.CommitAsync(cancellationToken);

        var response = new GuestSessionResponse(
            GuestToken: generatedToken,
            ExpiresAt: sesion.ExpiresAt,
            Room: ToRoomMinimalDto(lockedSala, now)
        );

        return Results.Created($"/api/guest/rooms/{lockedSala.Id}", response);
    }

    #endregion

    #region GET /api/guest/rooms/{roomId}

    private static async Task<IResult> GetRoomAsync(
        Guid roomId,
        HttpContext httpContext,
        IGuestSessionService sessionService,
        DjRequestsDbContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Headers.TryGetValue(GuestTokenHeaderName, out var tokenHeader) ||
            string.IsNullOrWhiteSpace(tokenHeader))
        {
            return Results.Problem(
                detail: "Credencial de invitado requerida.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No autorizado",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.2");
        }

        var token = tokenHeader.ToString();

        if (!IsValidBase64UrlToken(token, out var tokenBytes))
        {
            return Results.Problem(
                detail: "Credencial de invitado invalida.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No autorizado",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.2");
        }

        var tokenHash = ComputeTokenHash(tokenBytes);

        var result = await sessionService.ValidateSessionAsync(tokenHash, roomId, timeProvider, context, cancellationToken);

        switch (result.Status)
        {
            case GuestSessionStatus.Unauthorized:
                return Results.Problem(
                    detail: "Credencial de invitado invalida, revocada o expirada.",
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "No autorizado",
                    type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.2");

            case GuestSessionStatus.RoomNotFound:
                return Results.Problem(
                    detail: "Sala no encontrada.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Sala no encontrada",
                    type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5");

            default:
                var session = result.Session!;
                var sala = result.Sala!;
                var now = timeProvider.GetUtcNow();
                var effectiveExpiresAt = session.ExpiresAt < sala.EndsAt ? session.ExpiresAt : sala.EndsAt;

                var response = new GuestRoomResponse(
                    Room: ToRoomMinimalDto(sala, now),
                    ExpiresAt: effectiveExpiresAt
                );

                return Results.Ok(response);
        }
    }

    #endregion
}