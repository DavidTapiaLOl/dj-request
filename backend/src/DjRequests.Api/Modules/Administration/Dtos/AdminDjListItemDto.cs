namespace DjRequests.Api.Modules.Administration.Dtos;

public sealed record AdminDjListItemDto(
    Guid Id,
    string Email,
    string ArtistName,
    string Estado,
    DateTimeOffset CreatedAt);
