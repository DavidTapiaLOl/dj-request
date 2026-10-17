namespace DjRequests.Api.Modules.Guests.Options;

public sealed class GuestRateLimitOptions
{
    public int PostPermitLimit { get; init; } = 60;
    public int GetPermitLimit { get; init; } = 120;
    public int WindowMinutes { get; init; } = 1;
}
