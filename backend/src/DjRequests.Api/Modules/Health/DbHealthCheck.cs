using Microsoft.EntityFrameworkCore;

namespace DjRequests.Api.Modules.Health;

public static class DbHealthCheck
{
    public static async Task<bool> CheckAsync(IServiceProvider serviceProvider)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Persistence.DjRequestsDbContext>();
            await dbContext.Database.OpenConnectionAsync();
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
            await dbContext.Database.CloseConnectionAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}