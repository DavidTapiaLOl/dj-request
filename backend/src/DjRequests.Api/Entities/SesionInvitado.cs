using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Entities;

public class SesionInvitado
{
    public Guid Id { get; set; }
    public Guid SalaId { get; set; }
    
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? UltimoEnvioAt { get; set; }
    
    public virtual Sala Sala { get; set; } = null!;
    public virtual ICollection<Solicitud> Solicitudes { get; set; } = new List<Solicitud>();
    public virtual ICollection<PlaylistCompartida> PlaylistsCompartidas { get; set; } = new List<PlaylistCompartida>();
}