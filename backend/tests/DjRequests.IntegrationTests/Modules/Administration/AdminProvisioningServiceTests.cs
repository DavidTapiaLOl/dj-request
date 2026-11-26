using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Administration.Provisioning;
using DjRequests.Api.Persistence;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Administration;

[Collection(IntegrationCollection.Name)]
public class AdminProvisioningServiceTests : IAsyncLifetime
{
    private const string Password = "AdminPass123!";
    private static readonly DateTimeOffset ControlledNow =
        new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;
    private WebAppFactory _factory = null!;

    public AdminProvisioningServiceTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_postgres);
        await _factory.InitializeAsync();
        var timeProvider = (FakeTimeProvider)_factory.Services.GetRequiredService<TimeProvider>();
        timeProvider.UtcNow = ControlledNow;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    private async Task<Guid> ProvisionAsync(string name, string email, string password = Password)
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<AdminProvisioningService>();
        return await service.ProvisionAsync(name, email, password);
    }

    private async Task<int> CountAdminsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        return await context.Users.CountAsync();
    }

    private async Task<AdminAccount> FindAdminAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var admin = await context.Users.SingleAsync(a => a.Id == id);
        return admin;
    }

    private async Task AssertRejectedWithoutPersistingAsync(
        string name,
        string email,
        string password,
        string expectedMessage)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ProvisionAsync(name, email, password));
        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await CountAdminsAsync());
    }

    [Fact]
    public async Task ProvisionAsync_TrimsNameAndEmail_AndUsesControlledCreatedAt()
    {
        var id = await ProvisionAsync("  Admin Principal  ", "  admin@test.com  ");

        var admin = await FindAdminAsync(id);
        Assert.Equal("Admin Principal", admin.Name);
        Assert.Equal("admin@test.com", admin.Email);
        Assert.Equal("ADMIN@TEST.COM", admin.NormalizedEmail);
        Assert.True((admin.CreatedAt - ControlledNow).Duration() < TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ProvisionAsync_EmptyName_IsRejectedWithoutPersisting()
    {
        await AssertRejectedWithoutPersistingAsync(
            "   ",
            "admin@test.com",
            Password,
            "nombre del administrador es obligatorio");
    }

    [Fact]
    public async Task ProvisionAsync_NameAt100_IsAccepted()
    {
        var id = await ProvisionAsync(new string('a', 100), "admin@test.com");

        var admin = await FindAdminAsync(id);
        Assert.Equal(100, admin.Name.Length);
    }

    [Fact]
    public async Task ProvisionAsync_NameLongerThan100_IsRejectedWithoutPersisting()
    {
        await AssertRejectedWithoutPersistingAsync(
            new string('a', 101),
            "admin@test.com",
            Password,
            "no puede superar los 100 caracteres");
    }

    [Fact]
    public async Task ProvisionAsync_EmptyEmail_IsRejectedWithoutPersisting()
    {
        await AssertRejectedWithoutPersistingAsync(
            "Admin Principal",
            "   ",
            Password,
            "correo electronico del administrador es obligatorio");
    }

    [Fact]
    public async Task ProvisionAsync_InvalidEmail_IsRejectedWithoutPersisting()
    {
        await AssertRejectedWithoutPersistingAsync(
            "Admin Principal",
            "correo-invalido",
            Password,
            "correo electronico del administrador no es valido");
    }

    [Fact]
    public async Task ProvisionAsync_EmailAt256_IsAccepted()
    {
        var email = new string('a', 247) + "@test.com";
        Assert.Equal(256, email.Length);

        var id = await ProvisionAsync("Admin Principal", email);

        var admin = await FindAdminAsync(id);
        Assert.Equal(email, admin.Email);
    }

    [Fact]
    public async Task ProvisionAsync_EmailLongerThan256_IsRejectedWithoutPersisting()
    {
        var email = new string('a', 248) + "@test.com";
        Assert.Equal(257, email.Length);

        await AssertRejectedWithoutPersistingAsync(
            "Admin Principal",
            email,
            Password,
            "no puede superar los 256 caracteres");
    }

    [Fact]
    public async Task ProvisionAsync_EmptyPassword_IsRejectedWithoutPersisting()
    {
        await AssertRejectedWithoutPersistingAsync(
            "Admin Principal",
            "admin@test.com",
            "   ",
            "contrasena del administrador es obligatoria");
    }

    [Fact]
    public async Task ProvisionAsync_DuplicateEmail_IsRejectedWithoutOverwritingOriginal()
    {
        var originalId = await ProvisionAsync("Admin Original", "admin@test.com");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ProvisionAsync("Admin Nuevo", "  ADMIN@TEST.COM  "));
        Assert.Contains("Ya existe una cuenta de administrador", exception.Message);

        var original = await FindAdminAsync(originalId);
        Assert.Equal("Admin Original", original.Name);
        Assert.Equal("admin@test.com", original.Email);
        Assert.Equal(1, await CountAdminsAsync());
    }

    [Fact]
    public async Task ProvisionAsync_ConcurrentDuplicateEmail_LeavesSingleAccount()
    {
        using var firstScope = _factory.Services.CreateScope();
        using var secondScope = _factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<AdminProvisioningService>();
        var second = secondScope.ServiceProvider.GetRequiredService<AdminProvisioningService>();

        var attempts = await Task.WhenAll(
            TryProvisionAsync(first, "Admin Uno"),
            TryProvisionAsync(second, "Admin Dos"));

        Assert.Single(attempts, a => a.Succeeded);
        Assert.Single(attempts, a => !a.Succeeded);
        Assert.All(attempts.Where(a => !a.Succeeded), a =>
            Assert.Contains("Ya existe una cuenta de administrador", a.Error, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, await CountAdminsAsync());
    }

    private static async Task<(bool Succeeded, string? Error)> TryProvisionAsync(
        AdminProvisioningService service,
        string name)
    {
        try
        {
            await service.ProvisionAsync(name, "admin@test.com", Password);
            return (true, null);
        }
        catch (InvalidOperationException exception)
        {
            return (false, exception.Message);
        }
    }
}
