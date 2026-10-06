using DjRequests.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DjRequests.Api.Persistence.Mappings;

public class DjAccountMap : IEntityTypeConfiguration<DjAccount>
{
    public void Configure(EntityTypeBuilder<DjAccount> builder)
    {
        builder.ToTable("djs", t =>
        {
            t.HasCheckConstraint("chk_djs_estado_valido", "estado IN ('activo','suspendido')");
            t.HasCheckConstraint("chk_djs_email_no_vacio", "btrim(\"Email\") <> ''");
            t.HasCheckConstraint("chk_djs_normalized_email_no_vacio", "btrim(\"NormalizedEmail\") <> ''");
        });
        
        builder.Property(e => e.NombreArtistico)
            .HasColumnName("nombre_artistico")
            .HasMaxLength(100)
            .IsRequired();
        
        builder.Property(e => e.Estado)
            .HasColumnName("estado")
            .HasMaxLength(16)
            .HasDefaultValue("activo")
            .IsRequired();
        
        builder.Property(e => e.Email)
            .HasMaxLength(256)
            .IsRequired();
        
        builder.Property(e => e.NormalizedEmail)
            .HasMaxLength(256)
            .IsRequired();
        
        builder.HasIndex(e => e.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("IX_djs_normalized_email");
        
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
        
        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}