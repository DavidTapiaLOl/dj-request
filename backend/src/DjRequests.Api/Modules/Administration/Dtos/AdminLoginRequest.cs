using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Modules.Administration.Dtos;

public sealed record AdminLoginRequest(
    [EmailAddress, Required, MaxLength(256)] string Email,
    [Required] string Password);
