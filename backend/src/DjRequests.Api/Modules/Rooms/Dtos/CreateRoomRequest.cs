using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Modules.Rooms.Dtos;

public sealed record CreateRoomRequest(
    [Required, MaxLength(120)] string Name,
    [Required] string StartsAt,
    [Required] string EndsAt,
    string? Timezone
);
