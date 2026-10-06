using DjRequests.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DjRequests.Api.Persistence;

public class DjRequestsDbContext : IdentityDbContext<DjAccount, IdentityRole<Guid>, Guid>
{
    public DjRequestsDbContext(DbContextOptions<DjRequestsDbContext> options) : base(options)
    {
    }

    public DbSet<Sala> Salas { get; set; } = null!;
    public DbSet<SesionInvitado> SesionesInvitado { get; set; } = null!;
    public DbSet<Genero> Generos { get; set; } = null!;
    public DbSet<Solicitud> Solicitudes { get; set; } = null!;
    public DbSet<PlaylistCompartida> PlaylistsCompartidas { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // Apply all entity configurations
        builder.ApplyConfigurationsFromAssembly(typeof(DjRequestsDbContext).Assembly);
        
        // Configure Identity tables to snake_case
        builder.Entity<DjAccount>().ToTable("djs");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}