namespace DjRequests.Api.Modules.Playlists.Dtos;

public sealed record CreatePlaylistRequest(
    string? Nombre,
    string? Enlace,
    Guid IdempotencyKey
);
