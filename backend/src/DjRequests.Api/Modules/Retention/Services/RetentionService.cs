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
        // Hora de elegibilidad: solo candidatos con PurgeAt <= ahora.
        var now = _timeProvider.GetUtcNow();

        var candidateIds = await _context.Salas
            .AsNoTracking()
            .Where(s => s.PurgeAt <= now)
            .OrderBy(s => s.PurgeAt)
            .ThenBy(s => s.Id)
            .Take(_options.BatchSize)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            return new RetentionBatchResult(0);
        }

        // Transaccion corta: solo bloqueo, revalidacion y borrado.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Bloqueo de fila en orden estable (purge_at, id) con SKIP LOCKED para
        // coordinar varias instancias sin esperas ni bloqueos cruzados.
        var ids = candidateIds.ToArray();
        var locked = await _context.Salas
            .FromSqlInterpolated($"""
                SELECT * FROM salas
                WHERE id = ANY({ids})
                ORDER BY purge_at, id
                FOR UPDATE SKIP LOCKED
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Hora POST-bloqueo: unica fuente de verdad de la revalidacion.
        var nowAfterLock = _timeProvider.GetUtcNow();

        var expiredIds = locked
            .Where(s => s.PurgeAt <= nowAfterLock)
            .Select(s => s.Id)
            .ToArray();

        if (expiredIds.Length == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new RetentionBatchResult(0);
        }

        // Solo se borran salas; las cascadas de PostgreSQL eliminan el contenido
        // temporal (sesiones, solicitudes y playlists). Se revalida PurgeAt en el
        // propio DELETE por si acaso, aunque bajo bloqueo no puede cambiar.
        var deleted = await _context.Salas
            .Where(s => expiredIds.Contains(s.Id) && s.PurgeAt <= nowAfterLock)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new RetentionBatchResult(deleted);
    }
}
