using DjRequests.Api.Entities;
using DjRequests.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DjRequests.IntegrationTests.Fixtures;

public sealed class RegisterInsertBarrierInterceptor : SaveChangesInterceptor
{
    private readonly int _participants;
    private readonly TimeSpan _timeout;
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrivals;

    public RegisterInsertBarrierInterceptor(int participants, TimeSpan timeout)
    {
        _participants = participants;
        _timeout = timeout;
    }

    public int Arrivals => Volatile.Read(ref _arrivals);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is DjRequestsDbContext context &&
            context.ChangeTracker.Entries<DjAccount>().Any(entry => entry.State == EntityState.Added))
        {
            var arrival = Interlocked.Increment(ref _arrivals);
            if (arrival >= _participants)
            {
                _released.TrySetResult();
            }

            try
            {
                await _released.Task.WaitAsync(_timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException(
                    $"La barrera de registro esperaba {_participants} inserciones de DjAccount y solo llegaron {Arrivals} en {_timeout.TotalSeconds} segundos.");
            }
        }

        return result;
    }
}
