using System.Globalization;
using System.Security.Claims;
using DjRequests.Api.Modules.Common;
using DjRequests.Api.Modules.Guests;
using DjRequests.Api.Modules.Guests.Services;
using DjRequests.Api.Modules.Playlists.Dtos;
using DjRequests.Api.Modules.Playlists.Services;
using DjRequests.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DjRequests.Api.Modules.Playlists.Endpoints;

public static class PlaylistEndpoints
{
    public static void MapPlaylistEndpoints(this IEndpointRouteBuilder app)
    {
        // === Endpoints de invitado ===
        var guestGroup = app.MapGroup("/api/guest/rooms/{roomId:guid}/playlists")
            .WithTags("Playlists");

        guestGroup.MapPost("", CreateForGuestAsync)
            .WithName("CreatePlaylistAsGuest")
            .RequireRateLimiting("guest-create")
            .Produces<PlaylistResponse>(StatusCodes.Status201Created)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests);

        // No existe listado de playlists para invitados: solo el DJ propietario
        // puede consultarlas.

        // === Endpoints de DJ ===
        var djGroup = app.MapGroup("/api/rooms/{roomId:guid}/playlists")
            .WithTags("Playlists")
            .RequireAuthorization("ActiveDj");

        djGroup.MapGet("", ListForDjAsync)
            .WithName("ListPlaylistsAsDj")
            .Produces<PlaylistPageResponse>(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static Guid GetDjId(ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userId!);
    }

    #region POST /api/guest/rooms/{roomId}/playlists

    private static async Task<IResult> CreateForGuestAsync(
        Guid roomId,
        CreatePlaylistRequest request,
        HttpContext httpContext,
        IGuestSessionService sessionService,
        IPlaylistService playlistService,
        DjRequestsDbContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // Validar token primero
        var validation = await GuestTokenValidation.ValidateRequestAsync(
            httpContext, roomId, sessionService, context, timeProvider, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.Failure!;
        }

        var result = await playlistService.CreateAsync(roomId, validation.TokenHash!, request, timeProvider, cancellationToken);

        return NoStoreResults.AddNoStore(result.Status switch
        {
            PlaylistCreateStatus.Created => Results.Created($"/api/guest/rooms/{roomId}/playlists/{result.Response!.Id}", result.Response),
            PlaylistCreateStatus.IdempotentReplay => Results.Ok(result.Response),
            PlaylistCreateStatus.Unauthorized => Results.Problem(
                detail: result.ErrorMessage, statusCode: StatusCodes.Status401Unauthorized,
                title: "No autorizado", type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.2"),
            PlaylistCreateStatus.RoomNotFound => Results.Problem(
                detail: result.ErrorMessage, statusCode: StatusCodes.Status404NotFound,
                title: "Sala no encontrada", type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5"),
            PlaylistCreateStatus.RoomNotAvailable => Results.Problem(
                detail: result.ErrorMessage, statusCode: StatusCodes.Status409Conflict,
                title: "Sala no disponible", type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10"),
            PlaylistCreateStatus.QuotaExceeded => Results.Problem(
                detail: result.ErrorMessage, statusCode: StatusCodes.Status429TooManyRequests,
                title: "Límite de playlists alcanzado", type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.16"),
            PlaylistCreateStatus.RateLimited => CreateRateLimitedResponse(httpContext, result.RetryAfterSeconds),
            PlaylistCreateStatus.ValidationFailed => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [result.ErrorCode!] = new[] { result.ErrorMessage! }
            }),
            PlaylistCreateStatus.Conflict => Results.Problem(
                detail: result.ErrorMessage, statusCode: StatusCodes.Status409Conflict,
                title: "Conflicto", type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10"),
            _ => Results.Problem("Error inesperado.", statusCode: 500)
        });
    }

    private static IResult CreateRateLimitedResponse(HttpContext httpContext, int? retryAfterSeconds)
    {
        if (retryAfterSeconds.HasValue)
        {
            httpContext.Response.Headers.RetryAfter =
                Math.Max(1, retryAfterSeconds.Value).ToString(CultureInfo.InvariantCulture);
        }

        var detail = retryAfterSeconds.HasValue
            ? $"Debe esperar {retryAfterSeconds.Value} segundos entre envíos."
            : "Debe esperar entre envíos.";

        return Results.Problem(
            detail: detail,
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Demasiadas solicitudes",
            type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.16");
    }

    #endregion

    #region GET /api/rooms/{roomId}/playlists

    private static async Task<IResult> ListForDjAsync(
        Guid roomId,
        ClaimsPrincipal user,
        IPlaylistService playlistService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        var djId = GetDjId(user);

        if (!PaginationValidation.TryValidate(page, pageSize, out var paginationError))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["page"] = new[] { paginationError }
            });
        }

        var result = await playlistService.ListForDjAsync(roomId, djId, page, pageSize, timeProvider, cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "RoomNotFound" => Results.Problem(result.ErrorMessage, statusCode: 404, title: "Sala no encontrada"),
                "Pagination" => Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["page"] = new[] { result.ErrorMessage! }
                }),
                _ => Results.Problem("Error inesperado.", statusCode: 500)
            };
        }

        return Results.Ok(result.Page);
    }

    #endregion
}
