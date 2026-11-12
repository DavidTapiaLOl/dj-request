namespace DjRequests.Api.Modules.Retention.Services;

public sealed record RetentionBatchResult(int SelectedRooms, int DeletedRooms);

public interface IRetentionService
{
    /// <summary>
    /// Selecciona y bloquea, como maximo un lote, las salas con
    /// PurgeAt <= hora actual y elimina las que sigan vencidas tras el bloqueo.
    /// Devuelve cuantas filas bloqueo el lote y cuantas elimino.
    /// </summary>
    Task<RetentionBatchResult> PurgeExpiredBatchAsync(CancellationToken cancellationToken = default);
}
