namespace DjRequests.Api.Modules.Guests.Dtos;

public sealed record RoomMinimalDto(
    Guid Id,
    string Name,
    string State,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Timezone,
    bool CanReceiveRequests,
    int MaxSolicitudesSesion,
    int MaxPlaylistsSesion,
    int IntervaloEnvioSegundos
);
