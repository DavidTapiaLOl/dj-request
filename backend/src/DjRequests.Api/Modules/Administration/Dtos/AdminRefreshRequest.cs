using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Modules.Administration.Dtos;

public sealed record AdminRefreshRequest(
    [Required] string RefreshToken);
