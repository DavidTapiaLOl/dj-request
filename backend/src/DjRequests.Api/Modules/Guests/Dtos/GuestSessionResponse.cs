namespace DjRequests.Api.Modules.Guests.Dtos;

public sealed record GuestSessionResponse(
    string GuestToken,
    DateTimeOffset ExpiresAt,
    RoomMinimalDto Room
);
