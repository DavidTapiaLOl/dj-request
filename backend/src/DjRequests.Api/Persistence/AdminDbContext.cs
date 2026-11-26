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
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("admin_user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("admin_user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("admin_user_tokens");
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
