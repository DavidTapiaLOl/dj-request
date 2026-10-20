namespace DjRequests.Api.Modules.Requests.Dtos;

public sealed record UpdateSolicitudStateRequest(
    string? Estado,
    long Version
);
