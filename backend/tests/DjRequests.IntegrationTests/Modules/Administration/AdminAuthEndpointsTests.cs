using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Accounts.Dtos;
using DjRequests.Api.Modules.Administration.Dtos;
using DjRequests.Api.Modules.Administration.Provisioning;
using DjRequests.Api.Persistence;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Administration;

[Collection(IntegrationCollection.Name)]
public class AdminAuthEndpointsTests : IAsyncLifetime
{
    private const string Password = "AdminPass123!";
    private const string Email = "admin@test.com";
    private const string DjPassword = "DjPass123456!";
    private static readonly DateTimeOffset ControlledNow =
        new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;
    private WebAppFactory _factory = null!;
    private HttpClient _client = null!;

    public AdminAuthEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_postgres);
        await _factory.InitializeAsync();
        FakeTime.UtcNow = ControlledNow;
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    private FakeTimeProvider FakeTime =>
        (FakeTimeProvider)_factory.Services.GetRequiredService<TimeProvider>();

    private HttpClient Client
    {
        get
        {
            if (_client == null)
            {
                _client = _factory.CreateClient();
                _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            return _client;
        }
    }

    private async Task<AdminAccount> CreateAdminAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<AdminProvisioningService>();
        var adminId = await provisioning.ProvisionAsync("Admin Principal", Email, Password);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AdminAccount>>();
        var admin = await userManager.FindByIdAsync(adminId.ToString());
        Assert.NotNull(admin);
        return admin;
    }

    private async Task UpdateAdminAsync(Action<AdminAccount> update)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AdminAccount>>();
        var admin = await userManager.FindByEmailAsync(Email);
        Assert.NotNull(admin);

        update(admin);
        var result = await userManager.UpdateAsync(admin);
        Assert.True(result.Succeeded);
    }

    private async Task ChangeSecurityStampAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AdminAccount>>();
        var admin = await userManager.FindByEmailAsync(Email);
        Assert.NotNull(admin);

        var result = await userManager.UpdateSecurityStampAsync(admin);
        Assert.True(result.Succeeded);
    }

    private async Task CreateDjWithIdAsync(Guid id, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<DjAccount>>();
        var dj = new DjAccount
        {
            Id = id,
            UserName = email,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            Estado = "activo",
            NombreArtistico = "DJ Mismo Id",
            CreatedAt = FakeTime.GetUtcNow(),
            UpdatedAt = FakeTime.GetUtcNow()
        };

        var result = await userManager.CreateAsync(dj, DjPassword);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    private async Task<int> CountAdminsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        return await context.Users.CountAsync();
    }

    private static async Task<Tokens> ReadTokensAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        return new Tokens(
            root.GetProperty("accessToken").GetString()!,
            root.GetProperty("refreshToken").GetString()!);
    }

    private async Task<Tokens> LoginAsync(string email = Email, string password = Password)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new AdminLoginRequest(email, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);
        return await ReadTokensAsync(response);
    }

    private async Task<Tokens> RegisterAndLoginDjAsync(string email)
    {
        var register = await Client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, DjPassword, "DJ Test"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        return await LoginDjAsync(email);
    }

    private async Task<Tokens> LoginDjAsync(string email)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, DjPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadTokensAsync(response);
    }

    private static HttpRequestMessage MeRequest(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static HttpRequestMessage DjMeRequest(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private Task<HttpResponseMessage> RefreshAdminAsync(string refreshToken) =>
        Client.PostAsJsonAsync("/api/admin/auth/refresh", new AdminRefreshRequest(refreshToken));

    private Task<HttpResponseMessage> RefreshDjAsync(string refreshToken) =>
        Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(refreshToken));

    private static void AssertNoStore(HttpResponseMessage response) =>
        Assert.True(response.Headers.CacheControl?.NoStore, "Falta Cache-Control: no-store.");

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokensAndMeReturnsProfile()
    {
        var admin = await CreateAdminAsync();

        var tokens = await LoginAsync();

        using var response = await Client.SendAsync(MeRequest(tokens.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);

        var body = await response.Content.ReadFromJsonAsync<AdminMeResponse>();
        Assert.NotNull(body);
        Assert.Equal(admin.Id, body.Id);
        Assert.Equal("Admin Principal", body.Name);
        Assert.Equal(Email, body.Email);
    }

    [Fact]
    public async Task Me_ResponseJson_ExposesOnlyPublicProperties()
    {
        await CreateAdminAsync();
        var tokens = await LoginAsync();

        using var response = await Client.SendAsync(MeRequest(tokens.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);

        var raw = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(raw);
        var properties = document.RootElement.EnumerateObject()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "email", "id", "name" }, properties);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lockout", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("activo", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorizedWithNoStore()
    {
        await CreateAdminAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new AdminLoginRequest(Email, "WrongPass123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertNoStore(response);
    }

    [Fact]
    public async Task Login_ValidationFailure_Returns400WithNoStore()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new AdminLoginRequest("correo-invalido", Password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertNoStore(response);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Email", problem.Errors.Keys);
    }

    [Fact]
    public async Task Login_MalformedJson_Returns400WithNoStore()
    {
        var content = new StringContent("{", Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/admin/auth/login", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertNoStore(response);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401WithNoStore()
    {
        var response = await Client.GetAsync("/api/admin/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertNoStore(response);
    }

    [Fact]
    public async Task Me_WithInvalidToken_Returns401WithNoStore()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.here");

        var response = await Client.GetAsync("/api/admin/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertNoStore(response);
    }

    [Fact]
    public async Task Me_AccessTokenExpired_Returns401WithNoStore()
    {
        await CreateAdminAsync();
        var tokens = await LoginAsync();

        FakeTime.Advance(TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(59));
        using var before = await Client.SendAsync(MeRequest(tokens.AccessToken));
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        AssertNoStore(before);

        FakeTime.Advance(TimeSpan.FromSeconds(2));
        using var after = await Client.SendAsync(MeRequest(tokens.AccessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        AssertNoStore(after);
    }

    [Fact]
    public async Task Refresh_ExpiredRefreshToken_Returns401WithNoStore()
    {
        await CreateAdminAsync();
        var tokens = await LoginAsync();

        FakeTime.Advance(TimeSpan.FromHours(7) + TimeSpan.FromMinutes(59));
        using var before = await RefreshAdminAsync(tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        AssertNoStore(before);

        FakeTime.Advance(TimeSpan.FromMinutes(1));
        using var after = await RefreshAdminAsync(tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        AssertNoStore(after);
    }

    [Fact]
    public async Task Refresh_ValidRefreshToken_ReturnsNewTokens()
    {
        await CreateAdminAsync();
        var tokens = await LoginAsync();

        var response = await RefreshAdminAsync(tokens.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);

        var refreshed = await ReadTokensAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshed.RefreshToken));

        using var meResponse = await Client.SendAsync(MeRequest(refreshed.AccessToken));
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        AssertNoStore(meResponse);
    }

    [Fact]
    public async Task Refresh_InvalidRefreshToken_ReturnsUnauthorizedWithNoStore()
    {
        var response = await RefreshAdminAsync("token-invalido");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertNoStore(response);
    }

    [Fact]
    public async Task Refresh_AccessTokenAsRefreshToken_ReturnsUnauthorizedWithNoStore()
    {
        await CreateAdminAsync();
        var tokens = await LoginAsync();

        var response = await RefreshAdminAsync(tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertNoStore(response);
    }

    [Fact]
    public async Task InactiveAdmin_LoginAndRefreshAreRejected()
    {
        await CreateAdminAsync();
        var tokens = await LoginAsync();

        await UpdateAdminAsync(admin => admin.IsActive = false);

        var login = await Client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new AdminLoginRequest(Email, Password));
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        AssertNoStore(login);

        using var refresh = await RefreshAdminAsync(tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        AssertNoStore(refresh);
    }

    [Fact]
    public async Task Me_DeactivatedAdmin_ReturnsForbiddenWithNoStore()
    {
        await CreateAdminAsync();
        var tokens = await LoginAsync();

        await UpdateAdminAsync(admin => admin.IsActive = false);

        using var response = await Client.SendAsync(MeRequest(tokens.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        AssertNoStore(response);
    }

    [Fact]
    public async Task Me_AfterSecurityStampChange_ReturnsForbiddenAndRefreshUnauthorized()
    {
        await CreateAdminAsync();
        var tokens = await LoginAsync();

        await ChangeSecurityStampAsync();

        using var meResponse = await Client.SendAsync(MeRequest(tokens.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, meResponse.StatusCode);
        AssertNoStore(meResponse);

        using var refreshResponse = await RefreshAdminAsync(tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
        AssertNoStore(refreshResponse);
    }

    [Fact]
    public async Task RefreshTokens_AreNotInterchangeableWithDjAuthentication()
    {
        await CreateAdminAsync();
        var adminTokens = await LoginAsync();
        var djTokens = await RegisterAndLoginDjAsync("dj-cross@test.com");

        using var adminRefreshWithDjToken = await RefreshAdminAsync(djTokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, adminRefreshWithDjToken.StatusCode);
        AssertNoStore(adminRefreshWithDjToken);

        using var djRefreshWithAdminToken = await RefreshDjAsync(adminTokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, djRefreshWithAdminToken.StatusCode);
    }

    [Fact]
    public async Task AccessTokens_AreIsolatedWhenDjAndAdminShareTheSameGuid()
    {
        var admin = await CreateAdminAsync();
        await CreateDjWithIdAsync(admin.Id, "dj-same-id@test.com");

        var adminTokens = await LoginAsync();
        var djTokens = await LoginDjAsync("dj-same-id@test.com");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<DjAccount>>();
        var dj = await userManager.FindByEmailAsync("dj-same-id@test.com");
        Assert.NotNull(dj);
        Assert.Equal(admin.Id, dj.Id);

        using var adminMe = await Client.SendAsync(MeRequest(adminTokens.AccessToken));
        Assert.Equal(HttpStatusCode.OK, adminMe.StatusCode);
        AssertNoStore(adminMe);

        using var djMe = await Client.SendAsync(DjMeRequest(djTokens.AccessToken));
        Assert.Equal(HttpStatusCode.OK, djMe.StatusCode);

        using var adminMeWithDjToken = await Client.SendAsync(MeRequest(djTokens.AccessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, adminMeWithDjToken.StatusCode);
        AssertNoStore(adminMeWithDjToken);

        using var djMeWithAdminToken = await Client.SendAsync(DjMeRequest(adminTokens.AccessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, djMeWithAdminToken.StatusCode);
    }

    [Fact]
    public async Task Login_LockoutAfterFiveFailedAttempts_RejectsCorrectPassword()
    {
        await CreateAdminAsync();

        for (var i = 0; i < 4; i++)
        {
            using var failed = await Client.PostAsJsonAsync(
                "/api/admin/auth/login",
                new AdminLoginRequest(Email, "WrongPass123!"));
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
            AssertNoStore(failed);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
            var admin = await context.Users.SingleAsync(a => a.Email == Email);
            Assert.Equal(4, admin.AccessFailedCount);
            Assert.Null(admin.LockoutEnd);
        }

        using var fifth = await Client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new AdminLoginRequest(Email, "WrongPass123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, fifth.StatusCode);
        AssertNoStore(fifth);

        using var correctWhileLocked = await Client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new AdminLoginRequest(Email, Password));
        Assert.Equal(HttpStatusCode.Unauthorized, correctWhileLocked.StatusCode);
        AssertNoStore(correctWhileLocked);

        using var scopeAfter = _factory.Services.CreateScope();
        var contextAfter = scopeAfter.ServiceProvider.GetRequiredService<AdminDbContext>();
        var lockedAdmin = await contextAfter.Users.SingleAsync(a => a.Email == Email);
        Assert.True(lockedAdmin.LockoutEnabled);
        Assert.NotNull(lockedAdmin.LockoutEnd);
        Assert.True(lockedAdmin.LockoutEnd > FakeTime.GetUtcNow());
        Assert.Equal(0, lockedAdmin.AccessFailedCount);
    }

    [Fact]
    public async Task Tokens_AreNotInterchangeableWithDjAuthentication()
    {
        await CreateAdminAsync();
        var adminTokens = await LoginAsync();

        using var djMeWithAdminToken = await Client.SendAsync(DjMeRequest(adminTokens.AccessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, djMeWithAdminToken.StatusCode);

        var djTokens = await RegisterAndLoginDjAsync("dj@test.com");

        using var adminMeWithDjToken = await Client.SendAsync(MeRequest(djTokens.AccessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, adminMeWithDjToken.StatusCode);
        AssertNoStore(adminMeWithDjToken);
    }

    [Fact]
    public async Task AdminLogin_ExceedingRateLimit_Returns429WithNoStore()
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["AdminAuthRateLimits:LoginPermitLimit"] = "2",
            ["AdminAuthRateLimits:RefreshPermitLimit"] = "2"
        };

        await using var factory = new WebAppFactory(_postgres, settings);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Client-IP", "203.0.113.44");

        for (var i = 0; i < 2; i++)
        {
            var allowed = await client.PostAsJsonAsync(
                "/api/admin/auth/login",
                new AdminLoginRequest(Email, Password));
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
            Assert.True(allowed.Headers.CacheControl?.NoStore);
            Assert.False(allowed.Headers.TryGetValues("Retry-After", out _));
        }

        var rejected = await client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new AdminLoginRequest(Email, Password));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.TryGetValues("Retry-After", out _));
        Assert.True(rejected.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task AdminRefresh_ExceedingRateLimit_Returns429WithNoStore()
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["AdminAuthRateLimits:LoginPermitLimit"] = "10",
            ["AdminAuthRateLimits:RefreshPermitLimit"] = "2"
        };

        await using var factory = new WebAppFactory(_postgres, settings);
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Client-IP", "203.0.113.45");

        for (var i = 0; i < 2; i++)
        {
            var allowed = await client.PostAsJsonAsync(
                "/api/admin/auth/refresh",
                new AdminRefreshRequest("token-invalido"));
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
            Assert.True(allowed.Headers.CacheControl?.NoStore);
            Assert.False(allowed.Headers.TryGetValues("Retry-After", out _));
        }

        var rejected = await client.PostAsJsonAsync(
            "/api/admin/auth/refresh",
            new AdminRefreshRequest("token-invalido"));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.TryGetValues("Retry-After", out _));
        Assert.True(rejected.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task RegisterEndpoint_IsNotPubliclyAvailable()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/admin/auth/register",
            new { email = "nuevo-admin@test.com", password = Password });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertNoStore(response);
        Assert.Equal(0, await CountAdminsAsync());
    }

    private sealed record Tokens(string AccessToken, string RefreshToken);
}
