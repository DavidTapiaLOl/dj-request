using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Entities;

public class Sala
{
    public Guid Id { get; set; }
    public Guid DjId { get; set; }
    
    [MaxLength(120)]
    public string Nombre { get; set; } = string.Empty;
    
    [MaxLength(12)]
    public string Codigo { get; set; } = string.Empty;
    
    [MaxLength(16)]
    public string Estado { get; set; } = "abierta";
    
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    
    [MaxLength(64)]
    public string Timezone { get; set; } = "America/Mexico_City";
    
    public DateTimeOffset PurgeAt { get; set; }
    
    public int MaxSolicitudesSesion { get; set; } = 10;
    public int MaxPlaylistsSesion { get; set; } = 2;
    public int IntervaloEnvioSegundos { get; set; } = 30;
    
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public virtual DjAccount Dj { get; set; } = null!;
    public virtual ICollection<SesionInvitado> SesionesInvitado { get; set; } = new List<SesionInvitado>();
    public virtual ICollection<Solicitud> Solicitudes { get; set; } = new List<Solicitud>();
    public virtual ICollection<PlaylistCompartida> PlaylistsCompartidas { get; set; } = new List<PlaylistCompartida>();
}