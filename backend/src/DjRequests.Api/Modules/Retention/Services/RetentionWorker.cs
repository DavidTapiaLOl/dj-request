using System.Diagnostics;
using DjRequests.Api.Modules.Retention.Options;
using Microsoft.Extensions.Options;

namespace DjRequests.Api.Modules.Retention.Services;

public sealed class RetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<RetentionOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RetentionWorker> _logger;

    public RetentionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<RetentionOptions> options,
        TimeProvider timeProvider,
        ILogger<RetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (!options.Enabled)
        {
            _logger.LogInformation("Retencion deshabilitada (Retention:Enabled=false).");
            return;
        }

        var interval = TimeSpan.FromMinutes(options.IntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Primera pasada inmediata al arrancar; despues, cada intervalo.
            await RunPassAsync(options, stoppingToken);

            try
            {
                await Task.Delay(interval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunPassAsync(RetentionOptions options, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var totalDeleted = 0;

        try
        {
            // Se drenan lotes acotados hasta que quede menos de un lote completo.
            while (!cancellationToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRetentionService>();

                var result = await service.PurgeExpiredBatchAsync(cancellationToken);
                totalDeleted += result.DeletedRooms;

                if (result.DeletedRooms < options.BatchSize)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "Retencion: {DeletedRooms} salas eliminadas en {ElapsedMilliseconds} ms.",
                totalDeleted,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Apagado normal: no se registra como error.
        }
        catch (Exception ex)
        {
            // Fallo transitorio: se registra una sola vez y se reintenta en el
            // siguiente intervalo, sin bucle cerrado de reintentos.
            _logger.LogError(
                ex,
                "Retencion: fallo operativo tras {DeletedRooms} salas eliminadas en {ElapsedMilliseconds} ms. Se reintentara en el siguiente intervalo.",
                totalDeleted,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
