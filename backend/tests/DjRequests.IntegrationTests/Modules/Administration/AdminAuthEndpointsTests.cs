using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Accounts.Dtos;
using DjRequests.Api.Modules.Administration.Dtos;
using DjRequests.Api.Modules.Administration.Provisioning;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Administration;

[Collection(IntegrationCollection.Name)]
public class AdminAuthEndpointsTests : IAsyncLifetime
{
    private const string Password = "AdminPass123!";
    private const string Email = "admin@test.com";

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
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

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
        Assert.True(response.Headers.CacheControl?.NoStore);
        return await ReadTokensAsync(response);
    }

    private static HttpRequestMessage MeRequest(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokensAndMeReturnsProfile()
    {
        var admin = await CreateAdminAsync();

        var tokens = await LoginAsync();

        using var response = await Client.SendAsync(MeRequest(tokens.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);

        var body = await response.Content.ReadFromJsonAsync<AdminMeResponse>();
        Assert.NotNull(body);
        Assert.Equal(admin.Id, body.Id);
        Assert.Equal("Admin Principal", body.Name);
        Assert.Equal(Email, body.Email);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorizedWithNoStore()
    {
        await CreateAdminAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new AdminLoginRequest(Email, "WrongPass123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Refresh_ValidRefreshToken_ReturnsNewTokens()
    {
        await CreateAdminAsync();
        var tokens = await LoginAsync();

        var response = await Client.PostAsJsonAsync(
            "/api/admin/auth/refresh",
            new AdminRefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);

        var refreshed = await ReadTokensAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshed.RefreshToken));

        using var meResponse = await Client.SendAsync(MeRequest(refreshed.AccessToken));
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_InvalidRefreshToken_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/admin/auth/refresh",
            new AdminRefreshRequest("token-invalido"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Me_DeactivatedAdmin_ReturnsForbidden()
    {
        await CreateAdminAsync();
        var tokens = await LoginAsync();

        await UpdateAdminAsync(admin => admin.IsActive = false);

        using var response = await Client.SendAsync(MeRequest(tokens.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Me_AfterSecurityStampChange_ReturnsForbidden()
    {
        await CreateAdminAsync();
        var tokens = await LoginAsync();

        await ChangeSecurityStampAsync();

        using var response = await Client.SendAsync(MeRequest(tokens.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var refreshResponse = await Client.PostAsJsonAsync(
            "/api/admin/auth/refresh",
            new AdminRefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Tokens_AreNotInterchangeableWithDjAuthentication()
    {
        await CreateAdminAsync();
        var adminTokens = await LoginAsync();

        using var djMeWithAdminToken = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        djMeWithAdminToken.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);
        using var djResponse = await Client.SendAsync(djMeWithAdminToken);
        Assert.Equal(HttpStatusCode.Unauthorized, djResponse.StatusCode);

        var register = await Client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("dj@test.com", "validpassphrase123", "DJ Test"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var djLogin = await Client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("dj@test.com", "validpassphrase123"));
        Assert.Equal(HttpStatusCode.OK, djLogin.StatusCode);
        var djTokens = await ReadTokensAsync(djLogin);

        using var adminMeWithDjToken = await Client.SendAsync(MeRequest(djTokens.AccessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, adminMeWithDjToken.StatusCode);
    }

    [Fact]
    public async Task AdminLogin_ExceedingRateLimit_Returns429()
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
            Assert.False(allowed.Headers.TryGetValues("Retry-After", out _));
        }

        var rejected = await client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new AdminLoginRequest(Email, Password));
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.TryGetValues("Retry-After", out _));
        Assert.True(rejected.Headers.CacheControl?.NoStore);
    }

    private sealed record Tokens(string AccessToken, string RefreshToken);
}
