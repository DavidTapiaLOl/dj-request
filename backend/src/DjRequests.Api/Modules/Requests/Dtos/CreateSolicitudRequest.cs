namespace DjRequests.Api.Modules.Requests.Dtos;

public sealed record CreateSolicitudRequest(
    string? Nombre,
    string? Artista,
    short? GeneroId,
    string? Enlace,
    Guid IdempotencyKey
);
