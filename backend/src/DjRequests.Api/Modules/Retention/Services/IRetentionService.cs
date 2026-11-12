namespace DjRequests.Api.Modules.Retention.Services;

public sealed record RetentionBatchResult(int DeletedRooms);

public interface IRetentionService
{
    /// <summary>
    /// Elimina, como maximo un lote, las salas con PurgeAt <= hora actual.
    /// Devuelve la cantidad de salas eliminadas en el lote.
    /// </summary>
    Task<RetentionBatchResult> PurgeExpiredBatchAsync(CancellationToken cancellationToken = default);
}
