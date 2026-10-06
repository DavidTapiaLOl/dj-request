using DjRequests.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DjRequests.IntegrationTests.Fixtures;

/// <summary>
/// Crea una base de datos aislada y desechable por prueba dentro del contenedor.
/// Nunca apunta a dj_requests ni a la base de desarrollo.
/// </summary>
public sealed class DjRequestsDbContextFactory
{
    private readonly string _containerConnectionString;
    private readonly DbContextOptions<DjRequestsDbContext> _options;

    public DjRequestsDbContextFactory(string containerConnectionString)
    {
        _containerConnectionString = containerConnectionString;

        DatabaseName = $"dj_requests_test_{Guid.NewGuid():N}";

        _options = new DbContextOptionsBuilder<DjRequestsDbContext>()
            .UseNpgsql(new NpgsqlConnectionStringBuilder(containerConnectionString)
            {
                Database = DatabaseName
            }.ConnectionString)
            .Options;
    }

    public string DatabaseName { get; }

    /// <summary>
    /// Crea un contexto nuevo sobre una base recién migrada desde cero (esquema limpio y datos vacíos).
    /// </summary>
    public async Task<DjRequestsDbContext> CreateMigratedContextAsync()
    {
        var context = new DjRequestsDbContext(_options);
        await context.Database.MigrateAsync();
        return context;
    }
}