using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Modules.Accounts.Dtos;

public sealed record LoginRequest(
    [EmailAddress, Required, MaxLength(256)] string Email,
    [Required] string Password
);
