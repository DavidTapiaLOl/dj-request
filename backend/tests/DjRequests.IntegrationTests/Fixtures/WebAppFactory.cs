using DjRequests.Api.Modules.Guests.Services;
using DjRequests.Api.Modules.Rooms.Services;
using DjRequests.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace DjRequests.IntegrationTests.Fixtures;

public class WebAppFactory : WebApplicationFactory<Program>
{
    private readonly PostgresFixture _postgres;
    private readonly IReadOnlyDictionary<string, string?> _settings;
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

    public WebAppFactory(
        PostgresFixture postgres,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        _postgres = postgres;
        _settings = settings ?? new Dictionary<string, string?>();
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
        // Retention queda deshabilitado por defecto en todos los tests para que
        // unos User Secrets locales no activen la limpieza fisica. Un test puede
        // sobreescribir la clave si necesita el worker.
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Retention:Enabled"] = "false"
        };
        foreach (var pair in _settings)
        {
            settings[pair.Key] = pair.Value;
        }

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(settings));

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

            services.AddDbContext<AdminDbContext>(options =>
                AdminDbContext.UseAdminStore(options, testConnectionString));

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

        var adminContext = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        adminContext.Database.Migrate();

        return host;
    }

    public async Task<DjRequestsDbContext> CreateDbContextAsync()
    {
        var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();
        await context.Database.MigrateAsync();
        return context;
    }

    public async Task<AdminDbContext> CreateAdminDbContextAsync()
    {
        var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
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
    private readonly object _sync = new();
    private readonly List<FakeTimer> _timers = new();
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public DateTimeOffset UtcNow
    {
        get
        {
            lock (_sync)
            {
                return _utcNow;
            }
        }
        set
        {
            lock (_sync)
            {
                _utcNow = value;
            }
        }
    }

    public override DateTimeOffset GetUtcNow() => UtcNow;

    // Advance mueve el reloj y dispara los timers vencidos. Asignar UtcNow
    // directamente solo cambia el reloj y no dispara timers.
    public void Advance(TimeSpan span)
    {
        List<FakeTimer> due;
        lock (_sync)
        {
            _utcNow = _utcNow.Add(span);

            due = new List<FakeTimer>();
            foreach (var timer in _timers.ToArray())
            {
                if (timer.TryMarkDue(_utcNow))
                {
                    due.Add(timer);
                }
            }
        }

        // Los callbacks se invocan fuera del lock para no bloquear otros hilos.
        foreach (var timer in due)
        {
            timer.InvokeCallback();
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);

        lock (_sync)
        {
            var timer = new FakeTimer(this, callback, state, dueTime, period);
            _timers.Add(timer);
            return timer;
        }
    }

    private sealed class FakeTimer : ITimer
    {
        private readonly FakeTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private TimeSpan _period;
        private DateTimeOffset? _dueAt;
        private bool _disposed;

        public FakeTimer(
            FakeTimeProvider owner,
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
            _period = period;
            _dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner._utcNow + dueTime;
        }

        // Se invoca siempre con el lock del owner tomado.
        public bool TryMarkDue(DateTimeOffset now)
        {
            if (_disposed || _dueAt is null || now < _dueAt.Value)
            {
                return false;
            }

            if (_period == Timeout.InfiniteTimeSpan || _period <= TimeSpan.Zero)
            {
                _dueAt = null;
                _owner._timers.Remove(this);
                return true;
            }

            _dueAt = now + _period;
            return true;
        }

        public void InvokeCallback()
        {
            lock (_owner._sync)
            {
                if (_disposed)
                {
                    return;
                }
            }

            _callback(_state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (_owner._sync)
            {
                if (_disposed)
                {
                    return false;
                }

                _period = period;
                _dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : _owner._utcNow + dueTime;

                if (_dueAt is null)
                {
                    _owner._timers.Remove(this);
                }
                else if (!_owner._timers.Contains(this))
                {
                    _owner._timers.Add(this);
                }

                return true;
            }
        }

        public void Dispose()
        {
            lock (_owner._sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}