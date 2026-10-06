using DjRequests.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace DjRequests.IntegrationTests.Fixtures;

public class DatabaseFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Container = new PostgreSqlBuilder()
            .WithImage("postgres:17")
            .WithDatabase("dj_requests_test")
            .WithUsername("test")
            .WithPassword("test")
            .Build();

        await Container.StartAsync();
    }

    /// <summary>
    /// Devuelve una base de datos nueva y migrada para una prueba aislada.
    /// </summary>
    public async Task<DjRequestsDbContext> CrearContextoAisladoAsync()
    {
        var factory = new DjRequestsDbContextFactory(Container.GetConnectionString());
        return await factory.CreateMigratedContextAsync();
    }

    /// <summary>
    /// Crea un contexto adicional sobre la misma base de datos aislada.
    /// </summary>
    public DjRequestsDbContext CrearContextoEnLaMismaBase(DjRequestsDbContext origen)
    {
        var connectionString = origen.Database.GetConnectionString()
            ?? throw new InvalidOperationException("El contexto no expone cadena de conexion.");

        var opciones = new DbContextOptionsBuilder<DjRequestsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DjRequestsDbContext(opciones);
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}

[Xunit.CollectionDefinition(Name)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "DatabaseCollection";
}