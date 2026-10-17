namespace DjRequests.Api.Modules.Guests.Dtos;

public sealed record GuestRoomResponse(
    RoomMinimalDto Room,
    DateTimeOffset ExpiresAt
);
