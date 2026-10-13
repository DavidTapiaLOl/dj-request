namespace DjRequests.Api.Modules.Accounts.Dtos;

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn
);
