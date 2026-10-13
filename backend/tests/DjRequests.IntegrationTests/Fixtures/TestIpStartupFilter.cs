using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace DjRequests.IntegrationTests.Fixtures;

internal sealed class TestIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use((context, nextDelegate) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Client-IP", out var raw))
                {
                    var value = raw.ToString();
                    if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Connection.RemoteIpAddress = null;
                    }
                    else if (IPAddress.TryParse(value, out var address))
                    {
                        context.Connection.RemoteIpAddress = address;
                    }
                }
                return nextDelegate();
            });
            next(app);
        };
    }
}
