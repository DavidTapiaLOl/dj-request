namespace DjRequests.Api.Modules.Health;

public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health/live", () => Results.Ok("live"))
            .WithName("HealthLive")
            .WithTags("Health");
        
        app.MapGet("/health/ready", async (IServiceProvider serviceProvider) =>
        {
            var isReady = await DbHealthCheck.CheckAsync(serviceProvider);
            return isReady ? Results.Ok("ready") : Results.StatusCode(503);
        })
            .WithName("HealthReady")
            .WithTags("Health");
        
        return app;
    }
}