using System.Security.Cryptography;
using DjRequests.Api.Modules.Common;
using DjRequests.Api.Modules.Guests.Services;
using DjRequests.Api.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DjRequests.Api.Modules.Guests;

public sealed record GuestTokenValidationResult(string? TokenHash, IResult? Failure)
{
    public bool IsValid => TokenHash is not null;
}

/// <summary>
/// Lectura y validacion de la cabecera X-Guest-Token usada por los endpoints
/// de invitado. Devuelve el hash del token cuando la sesion es valida o el
/// resultado de error (401/404) en caso contrario.
/// </summary>
public static class GuestTokenValidation
{
    private const string GuestTokenHeaderName = "X-Guest-Token";
    private const int TokenByteLength = 32;
    private const int TokenBase64UrlLength = 43;

    public static bool IsValidBase64UrlToken(string token, out byte[] bytes)
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

    public static string ComputeTokenHash(byte[] tokenBytes)
    {
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static bool TryDecodeGuestToken(string token, out string tokenHash)
    {
        tokenHash = string.Empty;
        if (!IsValidBase64UrlToken(token, out var tokenBytes))
        {
            return false;
        }
        tokenHash = ComputeTokenHash(tokenBytes);
        return true;
    }

    public static async Task<GuestTokenValidationResult> ValidateRequestAsync(
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
            return new GuestTokenValidationResult(null, Unauthorized("Credencial de invitado requerida."));
        }

        var token = tokenHeader.ToString();

        if (httpContext.Request.Headers[GuestTokenHeaderName].Count > 1 ||
            !IsValidBase64UrlToken(token, out var tokenBytes))
        {
            return new GuestTokenValidationResult(null, Unauthorized("Credencial de invitado inválida."));
        }

        var tokenHash = ComputeTokenHash(tokenBytes);
        var result = await sessionService.ValidateSessionAsync(tokenHash, roomId, timeProvider, context, cancellationToken);

        return result.Status switch
        {
            GuestSessionStatus.Unauthorized => new GuestTokenValidationResult(
                null, Unauthorized("Credencial de invitado inválida, revocada o expirada.")),
            GuestSessionStatus.RoomNotFound => new GuestTokenValidationResult(
                null, NoStoreResults.AddNoStore(Results.Problem(
                    detail: "Sala no encontrada.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Sala no encontrada",
                    type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5"))),
            _ => new GuestTokenValidationResult(tokenHash, null)
        };
    }

    private static IResult Unauthorized(string detail)
    {
        return NoStoreResults.AddNoStore(Results.Problem(
            detail: detail,
            statusCode: StatusCodes.Status401Unauthorized,
            title: "No autorizado",
            type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.2"));
    }
}
