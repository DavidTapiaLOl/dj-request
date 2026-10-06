using System.ComponentModel.DataAnnotations;

namespace DjRequests.Api.Entities;

public class Genero
{
    public short Id { get; set; }
    
    [MaxLength(60)]
    public string Nombre { get; set; } = string.Empty;
    
    public bool Activo { get; set; } = true;
    
    public virtual ICollection<Solicitud> Solicitudes { get; set; } = new List<Solicitud>();
}