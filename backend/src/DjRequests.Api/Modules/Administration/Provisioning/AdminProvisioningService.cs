using System.ComponentModel.DataAnnotations;
using DjRequests.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DjRequests.Api.Modules.Administration.Provisioning;

public sealed class AdminProvisioningService
{
    private const string DuplicateEmailIndex = "IX_admin_accounts_normalized_email";
    private const string DuplicateUserNameIndex = "IX_admin_accounts_normalized_username";

    private readonly UserManager<AdminAccount> _userManager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AdminProvisioningService> _logger;

    public AdminProvisioningService(
        UserManager<AdminAccount> userManager,
        TimeProvider timeProvider,
        ILogger<AdminProvisioningService> logger)
    {
        _userManager = userManager;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Guid> ProvisionAsync(string name, string email, string password)
    {
        var normalizedName = (name ?? string.Empty).Trim();
        if (normalizedName.Length == 0)
        {
            throw new InvalidOperationException("El nombre del administrador es obligatorio.");
        }

        if (normalizedName.Length > 100)
        {
            throw new InvalidOperationException("El nombre del administrador no puede superar los 100 caracteres.");
        }

        var normalizedEmail = (email ?? string.Empty).Trim();
        if (normalizedEmail.Length == 0)
        {
            throw new InvalidOperationException("El correo electronico del administrador es obligatorio.");
        }

        if (normalizedEmail.Length > 256)
        {
            throw new InvalidOperationException("El correo electronico del administrador no puede superar los 256 caracteres.");
        }

        if (!new EmailAddressAttribute().IsValid(normalizedEmail))
        {
            throw new InvalidOperationException("El correo electronico del administrador no es valido.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("La contrasena del administrador es obligatoria.");
        }

        if (await _userManager.FindByEmailAsync(normalizedEmail) is not null)
        {
            throw DuplicateEmail();
        }

        var admin = new AdminAccount
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            Name = normalizedName,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = _timeProvider.GetUtcNow()
        };

        IdentityResult result;
        try
        {
            result = await _userManager.CreateAsync(admin, password);
        }
        catch (DbUpdateException ex) when (IsDuplicateAccount(ex))
        {
            throw DuplicateEmail();
        }

        if (!result.Succeeded)
        {
            var codes = string.Join(", ", result.Errors.Select(e => e.Code));
            _logger.LogError("No se pudo crear la cuenta administrativa. Errores: {Codes}", codes);

            if (result.Errors.Any(e => e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)))
            {
                throw DuplicateEmail();
            }

            throw new InvalidOperationException(
                "No se pudo crear la cuenta administrativa. Revisa los requisitos de la contrasena.");
        }

        _logger.LogInformation("Cuenta administrativa creada con identificador {AdminId}", admin.Id);
        return admin.Id;
    }

    private static InvalidOperationException DuplicateEmail() =>
        new("Ya existe una cuenta de administrador con ese correo electronico.");

    private static bool IsDuplicateAccount(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                    && postgresException.ConstraintName is DuplicateEmailIndex or DuplicateUserNameIndex;
            }
        }

        return false;
    }
}
