namespace DjRequests.Api.Modules.Profiles.Dtos;

public sealed record SocialLinksDto(
    string? Instagram,
    string? Facebook,
    string? Tiktok,
    string? Youtube,
    string? Soundcloud);

public sealed record ProfileDto(
    string ArtistName,
    string? Description,
    int? StartYear,
    string? PhotoUrl,
    SocialLinksDto SocialLinks);
