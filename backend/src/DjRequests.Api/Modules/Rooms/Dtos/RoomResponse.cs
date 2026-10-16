namespace DjRequests.Api.Modules.Rooms.Dtos;

public sealed record RoomResponse(
    Guid Id,
    Guid DjId,
    string Nombre,
    string Codigo,
    string Estado,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Timezone,
    DateTimeOffset PurgeAt,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int MaxSolicitudesSesion,
    int MaxPlaylistsSesion,
    int IntervaloEnvioSegundos,
    bool HasEnded,
    bool CanReceiveRequests,
    string InviteUrl
);