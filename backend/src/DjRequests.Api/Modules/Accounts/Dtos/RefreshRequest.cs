using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Modules.Accounts.Dtos;

public sealed record RefreshRequest(
    [Required] string RefreshToken
);
