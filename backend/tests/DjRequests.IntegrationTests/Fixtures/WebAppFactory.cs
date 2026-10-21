using DjRequests.Api.Modules.Guests.Services;
using DjRequests.Api.Modules.Rooms.Services;
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

    public bool EnableRegisterInsertBarrier { get; set; }

    public RegisterInsertBarrierInterceptor? RegisterInsertBarrier { get; private set; }

    public IRoomCodeGenerator? CustomCodeGenerator { get; set; }

    public bool EnableRoomLockPause { get; set; }

    public RoomLockPauseInterceptor? RoomLockPause { get; private set; }

    public bool EnableGuestLockPause { get; set; }

    public GuestLockPauseInterceptor? GuestLockPause { get; private set; }

    public bool EnableSesionLockPause { get; set; }

    public SesionInvitadoLockPauseInterceptor? SesionLockPause { get; private set; }

    public IGuestTokenGenerator? CustomGuestTokenGenerator { get; set; }

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

        RegisterInsertBarrierInterceptor? barrier = null;
        if (EnableRegisterInsertBarrier)
        {
            barrier = new RegisterInsertBarrierInterceptor(
                participants: 2,
                timeout: TimeSpan.FromSeconds(10));
            RegisterInsertBarrier = barrier;
        }

        RoomLockPauseInterceptor? roomLockPause = null;
        if (EnableRoomLockPause)
        {
            roomLockPause = new RoomLockPauseInterceptor();
            RoomLockPause = roomLockPause;
        }

        GuestLockPauseInterceptor? guestLockPause = null;
        if (EnableGuestLockPause)
        {
            guestLockPause = new GuestLockPauseInterceptor();
            GuestLockPause = guestLockPause;
        }

        SesionInvitadoLockPauseInterceptor? sesionLockPause = null;
        if (EnableSesionLockPause)
        {
            sesionLockPause = new SesionInvitadoLockPauseInterceptor();
            SesionLockPause = sesionLockPause;
        }

        var customGenerator = CustomCodeGenerator;
        var customGuestGenerator = CustomGuestTokenGenerator;

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FakeTimeProvider());
            services.AddSingleton<IStartupFilter>(new TestIpStartupFilter());

            if (customGenerator is not null)
            {
                services.AddSingleton<IRoomCodeGenerator>(customGenerator);
            }

            if (customGuestGenerator is not null)
            {
                services.AddSingleton<IGuestTokenGenerator>(customGuestGenerator);
            }

            services.AddDbContext<DjRequestsDbContext>(options =>
            {
                options.UseNpgsql(testConnectionString);
                if (barrier is not null)
                {
                    options.AddInterceptors(barrier);
                }
                if (roomLockPause is not null)
                {
                    options.AddInterceptors(roomLockPause);
                }
                if (guestLockPause is not null)
                {
                    options.AddInterceptors(guestLockPause);
                }
                if (sesionLockPause is not null)
                {
                    options.AddInterceptors(sesionLockPause);
                }
            });
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
            // Liberar el pool de conexiones de esta base unica para no agotar
            // las conexiones del contenedor compartido entre muchos WebAppFactory.
            var testConnectionString = _postgres.Container.GetConnectionString()
                .Replace("Database=postgres", $"Database={_dbName}");
            NpgsqlConnection.ClearPool(new NpgsqlConnection(testConnectionString));

            await using var adminConn = new NpgsqlConnection(_postgres.Container.GetConnectionString());
            await adminConn.OpenAsync();

            await using (var cmd = new NpgsqlCommand($"""
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = "{_dbName}" AND pid <> pg_backend_pid()
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