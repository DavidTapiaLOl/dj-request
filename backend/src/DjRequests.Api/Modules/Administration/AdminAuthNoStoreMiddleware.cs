using DjRequests.Api.Modules.Common;
using Microsoft.AspNetCore.Builder;

namespace DjRequests.Api.Modules.Administration;

public static class AdminAuthNoStoreMiddleware
{
    public static IApplicationBuilder UseAdminAuthNoStore(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/api/admin/auth")
                || path.StartsWithSegments("/api/admin/djs"))
            {
                context.Response.Headers.CacheControl = NoStoreResults.CacheControlValue;
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = NoStoreResults.CacheControlValue;
                    return Task.CompletedTask;
                });
            }

            await next();
        });
    }
}
