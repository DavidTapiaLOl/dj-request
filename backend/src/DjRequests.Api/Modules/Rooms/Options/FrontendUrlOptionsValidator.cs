using System;
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
                return ValidateOptionsResult.Fail(
                    "Frontend.BaseUrl con HTTP solo se permite en localhost/loopback durante Development.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
