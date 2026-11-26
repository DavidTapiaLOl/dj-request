namespace DjRequests.Api.Modules.Administration.Authentication;

public static class AdminAuthenticationDefaults
{
    public const string BearerScheme = "AdminBearer";
    public const string Policy = "ActiveAdmin";
    public const string LoginRateLimitPolicy = "admin-login";
    public const string RefreshRateLimitPolicy = "admin-refresh";
    public const string AccessTokenProtectorPurpose = "DjRequests.AdminAuth.AccessToken.v1";
    public const string RefreshTokenProtectorPurpose = "DjRequests.AdminAuth.RefreshToken.v1";
}
