using DjRequests.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DjRequests.Api.Persistence.Mappings;

public class PlaylistCompartidaMap : IEntityTypeConfiguration<PlaylistCompartida>
{
    public void Configure(EntityTypeBuilder<PlaylistCompartida> builder)
    {
        builder.ToTable("playlists_compartidas", t =>
        {
            t.HasCheckConstraint("chk_playlist_enlace_no_vacio", "btrim(enlace) <> ''");
        });
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        
        builder.Property(e => e.SalaId).HasColumnName("sala_id");
        builder.Property(e => e.SesionId).HasColumnName("sesion_id");
        builder.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(200);
        builder.Property(e => e.Enlace).HasColumnName("enlace").HasMaxLength(2048).IsRequired();
        builder.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        
        builder.HasIndex(e => new { e.SalaId, e.CreatedAt, e.Id });
        builder.HasIndex(e => new { e.SesionId, e.IdempotencyKey }).IsUnique();
        
        builder.HasOne(e => e.Sala)
            .WithMany(s => s.PlaylistsCompartidas)
            .HasForeignKey(e => e.SalaId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(e => e.SesionInvitado)
            .WithMany(s => s.PlaylistsCompartidas)
            .HasForeignKey(e => new { e.SesionId, e.SalaId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}