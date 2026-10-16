using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DjRequests.IntegrationTests.Fixtures;

public sealed class RoomLockPauseInterceptor : DbCommandInterceptor
{
    private readonly TaskCompletionSource _paused = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _resumed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool _shouldPause = true;

    public Task Paused => _paused.Task;

    public void Resume() => _resumed.TrySetResult();

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (_shouldPause && command.CommandText.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            _shouldPause = false;
            _paused.TrySetResult();
            await _resumed.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }

        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
