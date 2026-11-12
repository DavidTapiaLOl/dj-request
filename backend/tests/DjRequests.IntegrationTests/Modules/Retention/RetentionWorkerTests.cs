using System.Collections.Concurrent;
using System.Diagnostics;
using DjRequests.Api.Modules.Retention.Options;
using DjRequests.Api.Modules.Retention.Services;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Retention;

public class RetentionWorkerTests
{
    [Fact]
    public async Task Worker_Enabled_RunsFirstPassAtStartupWithoutWaiting()
    {
        await using var harness = WorkerHarness.Create(enabled: true);
        var worker = harness.CreateWorker();

        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => harness.Service.Calls >= 1);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, harness.Service.Calls);
        Assert.Contains(harness.Logger.Messages, m => m.Contains("Retencion:"));
    }

    [Fact]
    public async Task Worker_Disabled_DoesNotRunAnyPass()
    {
        await using var harness = WorkerHarness.Create(enabled: false);
        var worker = harness.CreateWorker();

        await worker.StartAsync(CancellationToken.None);
        Assert.Equal(0, harness.Service.Calls);

        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, harness.Service.Calls);
        Assert.Contains(harness.Logger.Messages, m => m.Contains("deshabilitada"));
    }

    [Fact]
    public async Task Worker_TransientFailure_LogsAndWaitsForNextInterval()
    {
        await using var harness = WorkerHarness.Create(enabled: true, failuresBeforeSuccess: 1);
        var worker = harness.CreateWorker();

        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => harness.Service.Calls >= 1);

        // Sin avanzar el reloj no debe reintentar: no hay bucle cerrado.
        await Task.Delay(150);
        Assert.Equal(1, harness.Service.Calls);
        Assert.Contains(harness.Logger.Messages, m => m.Contains("fallo operativo"));

        harness.Time.Advance(TimeSpan.FromMinutes(15));
        await WaitUntilAsync(() => harness.Service.Calls >= 2);

        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, harness.Service.Calls);
    }

    [Fact]
    public async Task Worker_DrainsWhileSelectionIsFull_StopsWhenSelectionIsShorter()
    {
        await using var harness = WorkerHarness.Create(enabled: true, batchSize: 2);
        harness.Service.Enqueue(
            new RetentionBatchResult(2, 2),
            new RetentionBatchResult(2, 2),
            new RetentionBatchResult(1, 1));
        var worker = harness.CreateWorker();

        await worker.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => harness.Service.Calls >= 3);

        // Tras una seleccion corta la pasada termina: no vuelve a llamar.
        await Task.Delay(150);
        Assert.Equal(3, harness.Service.Calls);
        Assert.Contains(harness.Logger.Messages, m => m.Contains("5 salas"));

        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Worker_FullSelectionWithoutDeletions_DoesNotLoopForever()
    {
        await using var harness = WorkerHarness.Create(enabled: true, batchSize: 2);
        harness.Service.OnCall = _ => new RetentionBatchResult(2, 0);
        var worker = harness.CreateWorker();

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(200);

        // Una seleccion completa sin borrados no justifica repetir la misma pagina.
        Assert.Equal(1, harness.Service.Calls);
        Assert.Contains(harness.Logger.Messages, m => m.Contains("0 salas"));

        await worker.StopAsync(CancellationToken.None);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMilliseconds = 5000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.ElapsedMilliseconds > timeoutMilliseconds)
            {
                throw new TimeoutException("La condicion no se alcanzo dentro del tiempo limite.");
            }

            await Task.Delay(20);
        }
    }

    private sealed class FakeRetentionService : IRetentionService
    {
        private readonly ConcurrentQueue<RetentionBatchResult> _scripted = new();
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public int FailuresBeforeSuccess { get; set; }

        public Func<int, RetentionBatchResult>? OnCall { get; set; }

        public void Enqueue(params RetentionBatchResult[] results)
        {
            foreach (var result in results)
            {
                _scripted.Enqueue(result);
            }
        }

        public Task<RetentionBatchResult> PurgeExpiredBatchAsync(CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _calls);
            if (call <= FailuresBeforeSuccess)
            {
                throw new InvalidOperationException("fallo transitorio de prueba");
            }

            if (_scripted.TryDequeue(out var scripted))
            {
                return Task.FromResult(scripted);
            }

            if (OnCall is not null)
            {
                return Task.FromResult(OnCall(call));
            }

            return Task.FromResult(new RetentionBatchResult(0, 0));
        }
    }

    private sealed class ListLogger : ILogger<RetentionWorker>
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Enqueue($"{logLevel}: {formatter(state, exception)}");
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class WorkerHarness : IAsyncDisposable
    {
        private WorkerHarness(
            ServiceProvider provider,
            FakeRetentionService service,
            FakeTimeProvider time,
            ListLogger logger)
        {
            Provider = provider;
            Service = service;
            Time = time;
            Logger = logger;
        }

        public ServiceProvider Provider { get; }

        public FakeRetentionService Service { get; }

        public FakeTimeProvider Time { get; }

        public ListLogger Logger { get; }

        public static WorkerHarness Create(
            bool enabled,
            int failuresBeforeSuccess = 0,
            int batchSize = 100)
        {
            var service = new FakeRetentionService { FailuresBeforeSuccess = failuresBeforeSuccess };
            var time = new FakeTimeProvider();
            var logger = new ListLogger();

            var services = new ServiceCollection();
            services.AddSingleton<IRetentionService>(service);
            services.AddSingleton<IOptions<RetentionOptions>>(Options.Create(new RetentionOptions
            {
                Enabled = enabled,
                IntervalMinutes = 15,
                BatchSize = batchSize
            }));
            services.AddSingleton<TimeProvider>(time);
            services.AddSingleton<ILogger<RetentionWorker>>(logger);

            return new WorkerHarness(services.BuildServiceProvider(), service, time, logger);
        }

        public RetentionWorker CreateWorker() => new(
            Provider.GetRequiredService<IServiceScopeFactory>(),
            Provider.GetRequiredService<IOptions<RetentionOptions>>(),
            Provider.GetRequiredService<TimeProvider>(),
            Provider.GetRequiredService<ILogger<RetentionWorker>>());

        public async ValueTask DisposeAsync() => await Provider.DisposeAsync();
    }
}
