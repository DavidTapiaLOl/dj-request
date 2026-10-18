using System.Security.Cryptography;
using DjRequests.Api.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace DjRequests.Api.Modules.Guests.Endpoints;

public sealed class GuestSessionValidationMiddleware
{
    private readonly RequestDelegate _next;
    private const string GuestTokenHeaderName = "X-Guest-Token";
    private const int TokenByteLength = 32;
    private const int TokenBase64UrlLength = 43;

    public GuestSessionValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, DjRequestsDbContext dbContext)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null)
        {
            await _next(context);
            return;
        }

        var isGuestGet = endpoint.Metadata.OfType<EndpointNameMetadata>().Any(m => m.EndpointName == "GetGuestRoom");
        if (isGuestGet)
        {
            await ValidateGuestTokenForGet(context, dbContext);
        }

        await _next(context);
    }

    private static async Task ValidateGuestTokenForGet(HttpContext context, DjRequestsDbContext dbContext)
    {
        if (!context.Request.Headers.TryGetValue(GuestTokenHeaderName, out var tokenHeader) ||
            string.IsNullOrWhiteSpace(tokenHeader))
        {
            context.Items["GuestRateLimitPartition"] = GetClientIp(context);
            context.Items["GuestSessionValid"] = false;
            return;
        }

        var token = tokenHeader.ToString();
        if (context.Request.Headers[GuestTokenHeaderName].Count > 1)
        {
            context.Items["GuestRateLimitPartition"] = GetClientIp(context);
            context.Items["GuestSessionValid"] = false;
            return;
        }

        if (!IsValidBase64UrlToken(token, out var tokenBytes))
        {
            context.Items["GuestRateLimitPartition"] = GetClientIp(context);
            context.Items["GuestSessionValid"] = false;
            return;
        }

        var tokenHash = ComputeTokenHash(tokenBytes);

        var session = await dbContext.SesionesInvitado
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.TokenHash == tokenHash);

        if (session is null)
        {
            context.Items["GuestRateLimitPartition"] = GetClientIp(context);
            context.Items["GuestSessionValid"] = false;
            return;
        }

        var routeRoomId = context.Request.RouteValues["roomId"] as Guid?;
        if (routeRoomId.HasValue && session.SalaId != routeRoomId.Value)
        {
            context.Items["GuestRateLimitPartition"] = GetClientIp(context);
            context.Items["GuestSessionValid"] = false;
            return;
        }

        var partitionKey = string.Format("session:{0}:{1}", session.SalaId, session.Id);
        context.Items["GuestRateLimitPartition"] = partitionKey;
        context.Items["GuestSessionValid"] = true;
    }

    private static string GetClientIp(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
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
}

public static class GuestSessionValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseGuestSessionValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GuestSessionValidationMiddleware>();
    }
}