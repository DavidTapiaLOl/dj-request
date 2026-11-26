using DjRequests.Api.Modules.Profiles.Dtos;

namespace DjRequests.Api.Modules.Administration.Dtos;

public sealed record AdminDjDto(
    Guid Id,
    string Email,
    string ArtistName,
    string Estado,
    DateTimeOffset CreatedAt,
    string? Description,
    int? StartYear,
    string? PhotoUrl,
    SocialLinksDto SocialLinks);
