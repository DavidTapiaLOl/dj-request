using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Entities;

public sealed class AuditoriaAdmin
{
    public Guid Id { get; set; }

    public Guid? AdministratorId { get; set; }

    [MaxLength(80)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(40)]
    public string ResourceType { get; set; } = string.Empty;

    public Guid? ResourceId { get; set; }

    [MaxLength(20)]
    public string Result { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public AdminAccount? Administrator { get; set; }
}
