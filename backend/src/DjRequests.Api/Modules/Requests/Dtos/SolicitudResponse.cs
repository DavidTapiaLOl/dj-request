namespace DjRequests.Api.Modules.Requests.Dtos;

public sealed record SolicitudResponse(
    Guid Id,
    string Nombre,
    string? Artista,
    short? GeneroId,
    string? GeneroNombre,
    string? Enlace,
    string Estado,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
