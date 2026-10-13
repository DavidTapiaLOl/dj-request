using Testcontainers.PostgreSql;
using Xunit;

namespace DjRequests.IntegrationTests.Fixtures;

public class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Container = new PostgreSqlBuilder("postgres:17")
            .WithDatabase("postgres")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await Container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}

[Xunit.CollectionDefinition(Name)]
public class IntegrationCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "IntegrationCollection";
}
