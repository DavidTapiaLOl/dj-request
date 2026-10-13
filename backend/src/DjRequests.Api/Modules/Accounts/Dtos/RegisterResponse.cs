namespace DjRequests.Api.Modules.Accounts.Dtos;

public sealed record RegisterResponse(
    Guid Id,
    string Email,
    string ArtistName,
    DateTimeOffset CreatedAt
);
