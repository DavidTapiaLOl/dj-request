using DjRequests.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DjRequests.Api.Persistence.Mappings;

public class DjProfileMap : IEntityTypeConfiguration<DjProfile>
{
    public void Configure(EntityTypeBuilder<DjProfile> builder)
    {
        builder.ToTable("perfiles_dj", t =>
        {
            t.HasCheckConstraint(
                "chk_perfiles_dj_anio_inicio",
                "anio_inicio IS NULL OR anio_inicio BETWEEN 1900 AND 2100");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.DjId).HasColumnName("dj_id").IsRequired();
        builder.Property(e => e.Descripcion).HasColumnName("descripcion").HasMaxLength(1500);
        builder.Property(e => e.AnioInicio).HasColumnName("anio_inicio");
        builder.Property(e => e.FotoNombreArchivo).HasColumnName("foto_nombre_archivo").HasMaxLength(260);

        builder.Property(e => e.RedInstagram).HasColumnName("red_instagram").HasMaxLength(2048);
        builder.Property(e => e.RedFacebook).HasColumnName("red_facebook").HasMaxLength(2048);
        builder.Property(e => e.RedTiktok).HasColumnName("red_tiktok").HasMaxLength(2048);
        builder.Property(e => e.RedYoutube).HasColumnName("red_youtube").HasMaxLength(2048);
        builder.Property(e => e.RedSoundcloud).HasColumnName("red_soundcloud").HasMaxLength(2048);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => e.DjId)
            .IsUnique()
            .HasDatabaseName("IX_perfiles_dj_dj_id");

        builder.HasOne(e => e.Dj)
            .WithOne()
            .HasForeignKey<DjProfile>(e => e.DjId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
