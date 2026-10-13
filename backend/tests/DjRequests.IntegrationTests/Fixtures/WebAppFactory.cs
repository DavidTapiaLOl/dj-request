using DjRequests.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace DjRequests.IntegrationTests.Fixtures;

public class WebAppFactory : WebApplicationFactory<Program>
{
    private readonly PostgresFixture _postgres;
    private readonly string _dbName = $"dj_requests_test_{Guid.NewGuid():N}";
    private bool _disposed;

    public WebAppFactory(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        await using var adminConn = new NpgsqlConnection(_postgres.Container.GetConnectionString());
        await adminConn.OpenAsync();

        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_dbName}\"", adminConn);
        await cmd.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var testConnectionString = _postgres.Container.GetConnectionString()
            .Replace("Database=postgres", $"Database={_dbName}");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FakeTimeProvider());
            services.AddSingleton<IStartupFilter>(new TestIpStartupFilter());

            services.AddDbContext<DjRequestsDbContext>(options =>
                options.UseNpgsql(testConnectionString));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();
        context.Database.Migrate();

        return host;
    }

    public async Task<DjRequestsDbContext> CreateDbContextAsync()
    {
        var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();
        await context.Database.MigrateAsync();
        return context;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        await base.DisposeAsync();

        try
        {
            await using var adminConn = new NpgsqlConnection(_postgres.Container.GetConnectionString());
            await adminConn.OpenAsync();

            await using (var cmd = new NpgsqlCommand($"""
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = '{_dbName}' AND pid <> pg_backend_pid()
                """, adminConn))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_dbName}\"", adminConn))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }
        catch
        {
        }
    }
}

public class FakeTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan span) => UtcNow = UtcNow.Add(span);
}
