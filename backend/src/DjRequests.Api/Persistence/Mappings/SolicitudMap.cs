using DjRequests.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DjRequests.Api.Persistence.Mappings;

public class SolicitudMap : IEntityTypeConfiguration<Solicitud>
{
    public void Configure(EntityTypeBuilder<Solicitud> builder)
    {
        builder.ToTable("solicitudes");
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        
        builder.Property(e => e.SalaId).HasColumnName("sala_id");
        builder.Property(e => e.SesionId).HasColumnName("sesion_id");
        builder.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Artista).HasColumnName("artista").HasMaxLength(200);
        builder.Property(e => e.GeneroId).HasColumnName("genero_id");
        builder.Property(e => e.Enlace).HasColumnName("enlace").HasMaxLength(2048);
        builder.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(16).HasDefaultValue("pendiente").IsRequired();
        builder.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key").IsRequired();
        builder.Property(e => e.Version).HasColumnName("version").HasDefaultValue(1).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        
        builder.HasIndex(e => new { e.SalaId, e.CreatedAt, e.Id });
        builder.HasIndex(e => new { e.SalaId, e.Estado, e.CreatedAt, e.Id });
        builder.HasIndex(e => e.GeneroId);
        builder.HasIndex(e => new { e.SesionId, e.IdempotencyKey }).IsUnique();
        
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_solicitud_nombre_no_vacio", "btrim(nombre) <> ''");
            t.HasCheckConstraint("chk_solicitud_estado_valido", "estado IN ('pendiente', 'aceptada', 'reproducida', 'rechazada')");
        });
        
        builder.HasOne(e => e.Sala)
            .WithMany(s => s.Solicitudes)
            .HasForeignKey(e => e.SalaId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(e => e.SesionInvitado)
            .WithMany(s => s.Solicitudes)
            .HasForeignKey(e => new { e.SesionId, e.SalaId })
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(e => e.Genero)
            .WithMany(g => g.Solicitudes)
            .HasForeignKey(e => e.GeneroId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}