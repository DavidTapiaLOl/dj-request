namespace DjRequests.Api.Modules.Administration.Options;

public sealed class AdminAuthRateLimitOptions
{
    public int LoginPermitLimit { get; init; } = 10;
    public int RefreshPermitLimit { get; init; } = 20;
    public int WindowMinutes { get; init; } = 1;
}
