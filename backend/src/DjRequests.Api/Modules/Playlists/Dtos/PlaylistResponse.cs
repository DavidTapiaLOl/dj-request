namespace DjRequests.Api.Modules.Playlists.Dtos;

public sealed record PlaylistResponse(
    Guid Id,
    string? Nombre,
    string Enlace,
    DateTimeOffset CreatedAt
);
