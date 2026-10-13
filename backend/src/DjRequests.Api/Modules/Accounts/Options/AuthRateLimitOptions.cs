namespace DjRequests.Api.Modules.Accounts.Options;

public sealed class AuthRateLimitOptions
{
    public int RegisterPermitLimit { get; init; } = 5;
    public int LoginPermitLimit { get; init; } = 10;
    public int RefreshPermitLimit { get; init; } = 20;
    public int WindowMinutes { get; init; } = 1;
}
