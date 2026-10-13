using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Modules.Accounts.Dtos;

public sealed record RegisterRequest(
    [EmailAddress, Required, MaxLength(256)] string Email,
    [Required, MinLength(12)] string Password,
    [Required, MaxLength(100)] string ArtistName
);
