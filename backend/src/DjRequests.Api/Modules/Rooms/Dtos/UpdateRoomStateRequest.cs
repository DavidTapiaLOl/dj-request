namespace DjRequests.Api.Modules.Rooms.Dtos;

public sealed record UpdateRoomStateRequest(
    string State,
    long Version
);