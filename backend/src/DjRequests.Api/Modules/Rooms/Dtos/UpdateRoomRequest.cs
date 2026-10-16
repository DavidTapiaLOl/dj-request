using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Modules.Rooms.Dtos;

public sealed record UpdateRoomRequest(
    long Version,
    [Required, MaxLength(120)] string Name,
    [Required] string StartsAt,
    [Required] string EndsAt,
    [Required, MaxLength(64)] string Timezone
);
