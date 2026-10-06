using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Entities;

public class Solicitud
{
    public Guid Id { get; set; }
    public Guid SalaId { get; set; }
    public Guid SesionId { get; set; }
    
    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? Artista { get; set; }
    
    public short? GeneroId { get; set; }
    
    [MaxLength(2048)]
    public string? Enlace { get; set; }
    
    [MaxLength(16)]
    public string Estado { get; set; } = "pendiente";
    
    public Guid IdempotencyKey { get; set; }
    
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    public virtual Sala Sala { get; set; } = null!;
    public virtual SesionInvitado SesionInvitado { get; set; } = null!;
    public virtual Genero? Genero { get; set; }
}