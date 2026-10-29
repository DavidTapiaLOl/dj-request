using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DjRequests.Api.Modules.Rooms.Options;

public sealed class FrontendUrlOptionsValidator : IValidateOptions<FrontendUrlOptions>
{
    private readonly IHostEnvironment _environment;

    public FrontendUrlOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, FrontendUrlOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return ValidateOptionsResult.Fail("Frontend.BaseUrl es obligatoria.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri))
        {
            return ValidateOptionsResult.Fail("Frontend.BaseUrl debe ser una URL absoluta valida.");
        }

        // Solo se permiten esquemas HTTP y HTTPS en cualquier entorno
        var scheme = uri.Scheme;
        if (!scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            && !scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                "Frontend.BaseUrl solo permite esquemas HTTP o HTTPS.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return ValidateOptionsResult.Fail("Frontend.BaseUrl no debe incluir credenciales.");
        }

        if (!string.IsNullOrEmpty(uri.Query))
        {
            return ValidateOptionsResult.Fail("Frontend.BaseUrl no debe incluir query string.");
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            return ValidateOptionsResult.Fail("Frontend.BaseUrl no debe incluir fragmento.");
        }

        var isDevelopment = _environment.IsDevelopment();

        if (!isDevelopment && !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail("Frontend.BaseUrl debe usar HTTPS fuera de Development.");
        }

        if (isDevelopment && uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            var host = uri.Host;
            var isLoopback = host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host == "127.0.0.1"
                || host == "::1";
            if (!isLoopback)
            {
                if (!options.AllowHttpLan)
                {
                    return ValidateOptionsResult.Fail(
                        "Frontend.BaseUrl con HTTP fuera de loopback requiere Frontend:AllowHttpLan=true durante Development.");
                }

                if (!IsPrivateIpv4(host))
                {
                    return ValidateOptionsResult.Fail(
                        "Frontend:AllowHttpLan solo permite direcciones IPv4 privadas RFC1918.");
                }
            }
        }

        return ValidateOptionsResult.Success;
    }

    private static bool IsPrivateIpv4(string host)
    {
        if (!IPAddress.TryParse(host, out var address)
            || address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
}
