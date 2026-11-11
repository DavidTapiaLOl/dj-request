namespace DjRequests.Api.Entities;

public class DjProfile
{
    public Guid Id { get; set; }
    public Guid DjId { get; set; }

    public string? Descripcion { get; set; }
    public int? AnioInicio { get; set; }
    public string? FotoNombreArchivo { get; set; }

    public string? RedInstagram { get; set; }
    public string? RedFacebook { get; set; }
    public string? RedTiktok { get; set; }
    public string? RedYoutube { get; set; }
    public string? RedSoundcloud { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual DjAccount Dj { get; set; } = null!;
}
