using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Entities;

public class PlaylistCompartida
{
    public Guid Id { get; set; }
    public Guid SalaId { get; set; }
    public Guid SesionId { get; set; }
    
    [MaxLength(200)]
    public string? Nombre { get; set; }
    
    [MaxLength(2048)]
    public string Enlace { get; set; } = string.Empty;
    
    public Guid IdempotencyKey { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public virtual Sala Sala { get; set; } = null!;
    public virtual SesionInvitado SesionInvitado { get; set; } = null!;
}