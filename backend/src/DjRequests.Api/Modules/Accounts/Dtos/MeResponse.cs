namespace DjRequests.Api.Modules.Accounts.Dtos;

public sealed record MeResponse(
    Guid Id,
    string Email,
    string ArtistName
);
