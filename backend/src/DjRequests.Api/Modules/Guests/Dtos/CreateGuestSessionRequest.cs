using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Modules.Guests.Dtos;

public sealed record CreateGuestSessionRequest(
    [Required, MaxLength(12)] string Code
);
