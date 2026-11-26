using System.ComponentModel.DataAnnotations;
using DjRequests.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace DjRequests.Api.Modules.Administration.Provisioning;

public sealed class AdminProvisioningService
{
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
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("El nombre del administrador es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
        {
            throw new InvalidOperationException("El correo electronico del administrador no es valido.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("La contrasena del administrador es obligatoria.");
        }

        var normalizedEmail = email.Trim();
        if (await _userManager.FindByEmailAsync(normalizedEmail) is not null)
        {
            throw new InvalidOperationException(
                "Ya existe una cuenta de administrador con ese correo electronico.");
        }

        var admin = new AdminAccount
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            Name = name.Trim(),
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = _timeProvider.GetUtcNow()
        };

        var result = await _userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            var codes = string.Join(", ", result.Errors.Select(e => e.Code));
            _logger.LogError("No se pudo crear la cuenta administrativa. Errores: {Codes}", codes);
            throw new InvalidOperationException(
                "No se pudo crear la cuenta administrativa. Revisa los requisitos de la contrasena.");
        }

        _logger.LogInformation("Cuenta administrativa creada con identificador {AdminId}", admin.Id);
        return admin.Id;
    }
}
