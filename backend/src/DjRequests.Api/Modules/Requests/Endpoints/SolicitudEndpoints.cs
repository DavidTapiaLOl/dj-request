using System.Security.Claims;
using System.Security.Cryptography;
using DjRequests.Api.Modules.Guests.Dtos;
using DjRequests.Api.Modules.Guests.Services;
using DjRequests.Api.Modules.Requests.Dtos;
using DjRequests.Api.Modules.Requests.Services;
using DjRequests.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DjRequests.Api.Modules.Requests.Endpoints;

public static class SolicitudEndpoints
{
    private const string GuestTokenHeaderName = "X-Guest-Token";
    private const int TokenByteLength = 32;
    private const int TokenBase64UrlLength = 43;

    public static void MapSolicitudEndpoints(this IEndpointRouteBuilder app)
    {
        // === Endpoints de invitado ===
        var guestGroup = app.MapGroup("/api/guest/rooms/{roomId:guid}/requests")
            .WithTags("Requests");

        guestGroup.MapPost("", CreateForGuestAsync)
            .WithName("CreateSolicitudAsGuest")
            .RequireRateLimiting("guest-create")
            .Produces<SolicitudResponse>(StatusCodes.Status201Created)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status429TooManyRequests);

        guestGroup.MapGet("", ListForGuestAsync)
            .WithName("ListSolicitudesAsGuest")
            .RequireRateLimiting("guest-get")
            .Produces<SolicitudPageResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        // === Endpoints de DJ ===
        var djGroup = app.MapGroup("/api/rooms/{roomId:guid}/requests")
            .WithTags("Requests")
            .RequireAuthorization("ActiveDj");

        djGroup.MapGet("", ListForDjAsync)
            .WithName("ListSolicitudesAsDj")
            .Produces<SolicitudPageResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        djGroup.MapPatch("/{solicitudId:guid}", UpdateStateAsync)
            .WithName("UpdateSolicitudState")
            .Produces<SolicitudResponse>(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static bool IsValidBase64UrlToken(string token, out byte[] bytes)
    {
        bytes = null!;
        if (string.IsNullOrWhiteSpace(token) || token.Length != TokenBase64UrlLength)
        {
            return false;
        }

        for (int i = 0; i < token.Length; i++)
        {
            var c = token[i];
            if (!(c >= 'A' && c <= 'Z') && !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '_')
            {
                return false;
            }
        }

        try
        {
            var base64 = token.Replace('-', '+').Replace('_', '/');
            var padding = 4 - (base64.Length % 4);
            if (padding != 4) base64 += new string('=', padding);
            bytes = Convert.FromBase64String(base64);
            if (bytes.Length != TokenByteLength) return false;
            var reencoded = Convert.ToBase64String(bytes);
            var reencodedUrl = reencoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return string.Equals(reencodedUrl, token, StringComparison.Ordinal);
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

    private static bool TryDecodeGuestToken(string token, out string tokenHash)
    {
        tokenHash = string.Empty;
        if (!IsValidBase64UrlToken(token, out var tokenBytes))
        {
            return false;
        }
        tokenHash = ComputeTokenHash(tokenBytes);
        return true;
    }

    private static Guid GetDjId(ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userId!);
    }

    private static async Task<IResult> ValidateGuestTokenAsync(
        HttpContext httpContext,
        Guid roomId,
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

        if (httpContext.Request.Headers[GuestTokenHeaderName].Count > 1 ||
            !IsValidBase64UrlToken(token, out var tokenBytes))
        {
            return Results.Problem(
                detail: "Credencial de invitado inválida.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No autorizado",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.2");
        }

        var tokenHash = ComputeTokenHash(tokenBytes);
        var result = await sessionService.ValidateSessionAsync(tokenHash, roomId, timeProvider, context, cancellationToken);

        return result.Status switch
        {
            GuestSessionStatus.Unauthorized => Results.Problem(
                detail: "Credencial de invitado inválida, revocada o expirada.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No autorizado",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.2"),
            GuestSessionStatus.RoomNotFound => Results.Problem(
                detail: "Sala no encontrada.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Sala no encontrada",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5"),
            _ => Results.Ok() // Token válido, continuar
        };
    }

    #region POST /api/guest/rooms/{roomId}/requests

    private static async Task<IResult> CreateForGuestAsync(
        Guid roomId,
        CreateSolicitudRequest request,
        HttpContext httpContext,
        IGuestSessionService sessionService,
        ISolicitudService solicitudService,
        DjRequestsDbContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // Validar token primero
        var validationResult = await ValidateGuestTokenAsync(httpContext, roomId, sessionService, context, timeProvider, cancellationToken);
        if (validationResult is not Microsoft.AspNetCore.Http.HttpResults.Ok)
        {
            return validationResult;
        }

        // Obtener hash del token para el servicio (ya validado arriba)
        if (!TryDecodeGuestToken(httpContext.Request.Headers[GuestTokenHeaderName].ToString(), out var tokenHash))
        {
            return Results.Problem(
                detail: "Credencial de invitado inválida.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No autorizado",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.2");
        }

        var result = await solicitudService.CreateAsync(roomId, tokenHash, request, timeProvider, cancellationToken);

        return result.Status switch
        {
            SolicitudCreateStatus.Created => Results.Created($"/api/guest/rooms/{roomId}/requests/{result.Response!.Id}", result.Response),
            SolicitudCreateStatus.IdempotentReplay => Results.Ok(result.Response),
            SolicitudCreateStatus.Unauthorized => Results.Problem(
                detail: result.ErrorMessage, statusCode: StatusCodes.Status401Unauthorized,
                title: "No autorizado", type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.2"),
            SolicitudCreateStatus.RoomNotFound => Results.Problem(
                detail: result.ErrorMessage, statusCode: StatusCodes.Status404NotFound,
                title: "Sala no encontrada", type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5"),
            SolicitudCreateStatus.RoomNotAvailable => Results.Problem(
                detail: result.ErrorMessage, statusCode: StatusCodes.Status409Conflict,
                title: "Sala no disponible", type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10"),
            SolicitudCreateStatus.QuotaExceeded => Results.Problem(
                detail: result.ErrorMessage, statusCode: StatusCodes.Status429TooManyRequests,
                title: "Límite de solicitudes alcanzado", type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.16"),
            SolicitudCreateStatus.RateLimited => CreateRateLimitedResponse(result.RetryAfterSeconds),
            SolicitudCreateStatus.ValidationFailed => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [result.ErrorCode!] = new[] { result.ErrorMessage! }
            }),
            SolicitudCreateStatus.Conflict => Results.Problem(
                detail: result.ErrorMessage, statusCode: StatusCodes.Status409Conflict,
                title: "Conflicto", type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10"),
            _ => Results.Problem("Error inesperado.", statusCode: 500)
        };
    }

    private static IResult CreateRateLimitedResponse(int? retryAfterSeconds)
    {
        var result = Results.Problem(
            detail: "Debe esperar entre envíos.",
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Demasiadas solicitudes",
            type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.16");

        if (retryAfterSeconds.HasValue)
        {
            // Agregar Retry-After al ProblemDetails
            return Results.Json(
                new
                {
                    type = "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.16",
                    title = "Demasiadas solicitudes",
                    status = 429,
                    detail = "Debe esperar entre envíos.",
                    retryAfter = retryAfterSeconds.Value
                },
                statusCode: 429);
        }

        return result;
    }

    #endregion

    #region GET /api/guest/rooms/{roomId}/requests

    private static async Task<IResult> ListForGuestAsync(
        Guid roomId,
        HttpContext httpContext,
        IGuestSessionService sessionService,
        ISolicitudService solicitudService,
        DjRequestsDbContext context,
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        var validationResult = await ValidateGuestTokenAsync(httpContext, roomId, sessionService, context, timeProvider, cancellationToken);
        if (validationResult is not Microsoft.AspNetCore.Http.HttpResults.Ok)
        {
            return validationResult;
        }

        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["page"] = new[] { "page debe ser >= 1 y pageSize entre 1 y 100." }
            });
        }

        if (!TryDecodeGuestToken(httpContext.Request.Headers[GuestTokenHeaderName].ToString(), out var tokenHash))
        {
            return Results.Problem(
                detail: "Credencial de invitado inválida.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No autorizado",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.2");
        }

        var result = await solicitudService.ListForGuestAsync(roomId, tokenHash, page, pageSize, timeProvider, cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "Unauthorized" => Results.Problem(result.ErrorMessage, statusCode: 401, title: "No autorizado"),
                "RoomNotFound" => Results.Problem(result.ErrorMessage, statusCode: 404, title: "Sala no encontrada"),
                _ => Results.Problem("Error inesperado.", statusCode: 500)
            };
        }

        return Results.Ok(result.Page);
    }

    #endregion

    #region GET /api/rooms/{roomId}/requests

    private static async Task<IResult> ListForDjAsync(
        Guid roomId,
        DjRequestsDbContext context,
        ClaimsPrincipal user,
        ISolicitudService solicitudService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken,
        string? estado = null,
        short? generoId = null,
        int page = 1,
        int pageSize = 20)
    {
        var djId = GetDjId(user);

        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["page"] = new[] { "page debe ser >= 1 y pageSize entre 1 y 100." }
            });
        }

        var result = await solicitudService.ListForDjAsync(roomId, djId, estado, generoId, page, pageSize, timeProvider, cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "RoomNotFound" => Results.Problem(result.ErrorMessage, statusCode: 404, title: "Sala no encontrada"),
                _ => Results.Problem("Error inesperado.", statusCode: 500)
            };
        }

        return Results.Ok(result.Page);
    }

    #endregion

    #region PATCH /api/rooms/{roomId}/requests/{solicitudId}

    private static async Task<IResult> UpdateStateAsync(
        Guid roomId,
        Guid solicitudId,
        UpdateSolicitudStateRequest request,
        DjRequestsDbContext context,
        ClaimsPrincipal user,
        ISolicitudService solicitudService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var djId = GetDjId(user);

        var result = await solicitudService.UpdateStateAsync(roomId, solicitudId, djId, request, timeProvider, cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "Estado" or "Version" or "Transition" => Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [result.ErrorCode!] = new[] { result.ErrorMessage! }
                }),
                "RoomNotFound" or "SolicitudNotFound" => Results.Problem(result.ErrorMessage, statusCode: 404, title: "No encontrado"),
                _ => Results.Problem("Error inesperado.", statusCode: 500)
            };
        }

        return Results.Ok(result.Response);
    }

    #endregion
}
