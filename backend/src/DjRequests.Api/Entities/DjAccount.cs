using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace DjRequests.Api.Entities;

public class DjAccount : IdentityUser<Guid>
{
    [MaxLength(100)]
    public string NombreArtistico { get; set; } = string.Empty;

    [MaxLength(16)]
    public string Estado { get; set; } = "activo";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}