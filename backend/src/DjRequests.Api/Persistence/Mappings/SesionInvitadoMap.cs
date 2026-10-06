using DjRequests.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DjRequests.Api.Persistence.Mappings;

public class SesionInvitadoMap : IEntityTypeConfiguration<SesionInvitado>
{
    public void Configure(EntityTypeBuilder<SesionInvitado> builder)
    {
        builder.ToTable("sesiones_invitado");
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        
        builder.Property(e => e.SalaId).HasColumnName("sala_id");
        builder.Property(e => e.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(e => e.RevokedAt).HasColumnName("revoked_at");
        builder.Property(e => e.UltimoEnvioAt).HasColumnName("ultimo_envio_at");
        
        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.HasIndex(e => e.SalaId);
        builder.HasIndex(e => new { e.SalaId, e.Id }).IsUnique();
        builder.HasKey(e => new { e.Id, e.SalaId });
        
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_sesion_expires_gt_created", "expires_at > created_at");
        });
        
        builder.HasOne(e => e.Sala)
            .WithMany(s => s.SesionesInvitado)
            .HasForeignKey(e => e.SalaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}