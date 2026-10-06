using DjRequests.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DjRequests.Api.Persistence.Mappings;

public class GeneroMap : IEntityTypeConfiguration<Genero>
{
    public void Configure(EntityTypeBuilder<Genero> builder)
    {
        builder.ToTable("generos");
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        
        builder.Property(e => e.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(60)
            .IsRequired();
        
        builder.Property(e => e.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();
        
        builder.HasIndex(e => e.Nombre).IsUnique();
        
        builder.HasData(
            new Genero { Id = 1, Nombre = "Pop", Activo = true },
            new Genero { Id = 2, Nombre = "Rock", Activo = true },
            new Genero { Id = 3, Nombre = "Reguetón", Activo = true },
            new Genero { Id = 4, Nombre = "Regional mexicano", Activo = true },
            new Genero { Id = 5, Nombre = "Electrónica", Activo = true },
            new Genero { Id = 6, Nombre = "Salsa", Activo = true },
            new Genero { Id = 7, Nombre = "Cumbia", Activo = true },
            new Genero { Id = 8, Nombre = "Bachata", Activo = true },
            new Genero { Id = 9, Nombre = "Hip-hop/Rap", Activo = true },
            new Genero { Id = 10, Nombre = "R&B", Activo = true },
            new Genero { Id = 11, Nombre = "Otro", Activo = true }
        );
    }
}