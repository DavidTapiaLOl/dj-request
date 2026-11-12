using DjRequests.Api.Modules.Retention.Options;
using DjRequests.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DjRequests.Api.Modules.Retention.Services;

public sealed class RetentionService : IRetentionService
{
    private readonly DjRequestsDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly RetentionOptions _options;

    public RetentionService(
        DjRequestsDbContext context,
        TimeProvider timeProvider,
        IOptions<RetentionOptions> options)
    {
        _context = context;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<RetentionBatchResult> PurgeExpiredBatchAsync(CancellationToken cancellationToken = default)
    {
        // Hora de elegibilidad para la consulta de seleccion.
        var now = _timeProvider.GetUtcNow();

        // Transaccion corta: seleccion y bloqueo en la misma consulta,
        // revalidacion y borrado.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Seleccionar y bloquear en una sola consulta: SKIP LOCKED salta las
        // filas que otra transaccion ya bloqueo y sigue el escaneo, de modo que
        // LIMIT llena el lote con las siguientes salas vencidas disponibles.
        // Orden estable (purge_at, id); LIMIT parametrizado.
        var locked = await _context.Salas
            .FromSqlInterpolated($"""
                SELECT * FROM salas
                WHERE purge_at <= {now}
                ORDER BY purge_at, id
                LIMIT {_options.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (locked.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new RetentionBatchResult(0, 0);
        }

        // Hora POST-bloqueo: unica fuente de verdad de la revalidacion.
        var nowAfterLock = _timeProvider.GetUtcNow();

        var expiredIds = locked
            .Where(s => s.PurgeAt <= nowAfterLock)
            .Select(s => s.Id)
            .ToArray();

        if (expiredIds.Length == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new RetentionBatchResult(locked.Count, 0);
        }

        // Solo se borran salas; las cascadas de PostgreSQL eliminan el contenido
        // temporal (sesiones, solicitudes y playlists). Se revalida PurgeAt en el
        // propio DELETE por si acaso, aunque bajo bloqueo no puede cambiar.
        var deleted = await _context.Salas
            .Where(s => expiredIds.Contains(s.Id) && s.PurgeAt <= nowAfterLock)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new RetentionBatchResult(locked.Count, deleted);
    }
}
