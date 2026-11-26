using DjRequests.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DjRequests.Api.Persistence;

public class AdminDbContext : IdentityUserContext<AdminAccount, Guid>
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_Admin";

    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options)
    {
    }

    public DbSet<AuditoriaAdmin> AdminAudits { get; set; } = null!;

    public static DbContextOptionsBuilder UseAdminStore(
        DbContextOptionsBuilder optionsBuilder,
        string? connectionString)
    {
        return optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AdminAccount>(ConfigureAdminAccount);
        builder.Entity<AuditoriaAdmin>(ConfigureAuditoriaAdmin);
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("admin_user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("admin_user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("admin_user_tokens");
    }

    private static void ConfigureAuditoriaAdmin(EntityTypeBuilder<AuditoriaAdmin> builder)
    {
        builder.ToTable("auditoria_admin", table =>
        {
            table.HasCheckConstraint("chk_auditoria_admin_accion_no_vacio", "btrim(\"accion\") <> ''");
            table.HasCheckConstraint("chk_auditoria_admin_tipo_recurso_no_vacio", "btrim(\"tipo_recurso\") <> ''");
            table.HasCheckConstraint("chk_auditoria_admin_resultado_no_vacio", "btrim(\"resultado\") <> ''");
        });

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.AdministratorId).HasColumnName("administrador_id");
        builder.Property(a => a.Action).HasColumnName("accion").HasMaxLength(80).IsRequired();
        builder.Property(a => a.ResourceType).HasColumnName("tipo_recurso").HasMaxLength(40).IsRequired();
        builder.Property(a => a.ResourceId).HasColumnName("recurso_id");
        builder.Property(a => a.Result).HasColumnName("resultado").HasMaxLength(20).IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(a => a.Administrator)
            .WithMany()
            .HasForeignKey(a => a.AdministratorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.CreatedAt)
            .HasDatabaseName("IX_auditoria_admin_created_at");
        builder.HasIndex(a => new { a.AdministratorId, a.CreatedAt })
            .HasDatabaseName("IX_auditoria_admin_administrador_id_created_at");
    }

    private static void ConfigureAdminAccount(EntityTypeBuilder<AdminAccount> builder)
    {
        builder.ToTable("admin_accounts", table =>
        {
            table.HasCheckConstraint("chk_admin_accounts_nombre_no_vacio", "btrim(\"nombre\") <> ''");
            table.HasCheckConstraint("chk_admin_accounts_email_no_vacio", "btrim(\"Email\") <> ''");
            table.HasCheckConstraint("chk_admin_accounts_normalized_email_no_vacio", "btrim(\"NormalizedEmail\") <> ''");
        });

        builder.Property(a => a.Name)
            .HasColumnName("nombre")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.IsActive)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(a => a.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(a => a.NormalizedEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(a => a.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("IX_admin_accounts_normalized_email");

        builder.HasIndex(a => a.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName("IX_admin_accounts_normalized_username");
    }
}
