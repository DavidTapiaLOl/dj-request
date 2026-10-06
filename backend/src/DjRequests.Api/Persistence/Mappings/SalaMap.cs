using DjRequests.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DjRequests.Api.Persistence.Mappings;

public class SalaMap : IEntityTypeConfiguration<Sala>
{
    public void Configure(EntityTypeBuilder<Sala> builder)
    {
        builder.ToTable("salas");
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        
        builder.Property(e => e.DjId).HasColumnName("dj_id");
        builder.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(120).IsRequired();
        builder.Property(e => e.Codigo).HasColumnName("codigo").HasMaxLength(12).IsRequired();
        builder.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(16).HasDefaultValue("abierta").IsRequired();
        builder.Property(e => e.StartsAt).HasColumnName("starts_at").IsRequired();
        builder.Property(e => e.EndsAt).HasColumnName("ends_at").IsRequired();
        builder.Property(e => e.Timezone).HasColumnName("timezone").HasMaxLength(64).HasDefaultValue("America/Mexico_City").IsRequired();
        builder.Property(e => e.PurgeAt).HasColumnName("purge_at").IsRequired();
        builder.Property(e => e.MaxSolicitudesSesion).HasColumnName("max_solicitudes_sesion").HasDefaultValue(10).IsRequired();
        builder.Property(e => e.MaxPlaylistsSesion).HasColumnName("max_playlists_sesion").HasDefaultValue(2).IsRequired();
        builder.Property(e => e.IntervaloEnvioSegundos).HasColumnName("intervalo_envio_segundos").HasDefaultValue(30).IsRequired();
        builder.Property(e => e.Version).HasColumnName("version").HasDefaultValue(1).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        
        builder.HasIndex(e => e.Codigo).IsUnique();
        builder.HasIndex(e => new { e.DjId, e.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(e => e.PurgeAt);
        
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_salas_estado_valido", "estado IN ('abierta','pausada','cerrada')");
            t.HasCheckConstraint("chk_salas_nombre_no_vacio", "btrim(nombre) <> ''");
            t.HasCheckConstraint("chk_sala_ends_gt_starts", "ends_at > starts_at");
            t.HasCheckConstraint("chk_sala_purge_gt_ends", "purge_at > ends_at");
            t.HasCheckConstraint("chk_sala_max_solicitudes", "max_solicitudes_sesion > 0");
            t.HasCheckConstraint("chk_sala_max_playlists", "max_playlists_sesion > 0");
            t.HasCheckConstraint("chk_sala_intervalo", "intervalo_envio_segundos >= 0");
        });
        
        builder.HasOne(e => e.Dj)
            .WithMany()
            .HasForeignKey(e => e.DjId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}