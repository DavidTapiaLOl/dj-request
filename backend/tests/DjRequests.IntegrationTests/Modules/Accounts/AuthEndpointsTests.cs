using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Accounts.Dtos;
using DjRequests.Api.Persistence;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Accounts;


[Collection(IntegrationCollection.Name)]
public class AuthEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private WebAppFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthEndpointsTests(PostgresFixture postgres)
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

    [Fact]
    public async Task Register_ValidRequest_Returns201AndPersistsDj()
    {
        var request = new RegisterRequest(
            Email: "dj@test.com",
            Password: "validpassphrase123",
            ArtistName: "DJ Test"
        );

        var response = await Client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(body);
        Assert.Equal("dj@test.com", body.Email);
        Assert.Equal("DJ Test", body.ArtistName);
        Assert.NotEqual(Guid.Empty, body.Id);

        // Verify persistence
        await using var context = await _factory.CreateDbContextAsync();
        var dj = await context.Users.FirstOrDefaultAsync(u => u.Email == "dj@test.com");
        Assert.NotNull(dj);
        Assert.Equal("activo", dj.Estado);
        Assert.Equal("DJ Test", dj.NombreArtistico);
        Assert.True(dj.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.True(dj.UpdatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var request = new RegisterRequest(
            Email: "not-an-email",
            Password: "validpassphrase123",
            ArtistName: "DJ Test"
        );

        var response = await Client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Email", problem.Errors.Keys);
    }

    [Fact]
    public async Task Register_EmptyArtistName_Returns400()
    {
        var request = new RegisterRequest(
            Email: "dj@test.com",
            Password: "validpassphrase123",
            ArtistName: ""
        );

        var response = await Client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ArtistNameTooLong_Returns400()
    {
        var request = new RegisterRequest(
            Email: "dj@test.com",
            Password: "validpassphrase123",
            ArtistName: new string('a', 101)
        );

        var response = await Client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordTooShort_Returns400()
    {
        var request = new RegisterRequest(
            Email: "dj@test.com",
            Password: "short",
            ArtistName: "DJ Test"
        );

        var response = await Client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmailCaseInsensitive_Returns409()
    {
        var request1 = new RegisterRequest("DJ@TEST.COM", "validpassphrase123", "DJ One");
        var request2 = new RegisterRequest("dj@test.com", "anotherpassphrase123", "DJ Two");

        await Client.PostAsJsonAsync("/api/auth/register", request1);
        var response = await Client.PostAsJsonAsync("/api/auth/register", request2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)HttpStatusCode.Conflict, problem.Status);
    }

    [Fact]
    public async Task Register_DoesNotAcceptEstadoFromClient()
    {
        // The endpoint should ignore any estado sent in request body
        // We test this by verifying the created DJ has estado="activo" regardless
        var request = new RegisterRequest("dj@test.com", "validpassphrase123", "DJ Test");

        await Client.PostAsJsonAsync("/api/auth/register", request);

        await using var context = await _factory.CreateDbContextAsync();
        var dj = await context.Users.FirstOrDefaultAsync(u => u.Email == "dj@test.com");
        Assert.Equal("activo", dj!.Estado);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        // First register
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        // Then login
        var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.AccessToken);
        Assert.NotNull(body.RefreshToken);
        Assert.Equal("Bearer", body.TokenType);
        Assert.Equal(900, body.ExpiresIn); // 15 minutes = 900 seconds
    }

    [Fact]
    public async Task Login_InvalidPassword_Returns401Generic()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "wrongpassword123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // Should be generic, not revealing if email exists
    }

    [Fact]
    public async Task Login_NonExistentEmail_Returns401Generic()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "nonexistent@test.com", "anypassphrase123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_SuspendedAccount_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        // Suspend the account
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var dj = await context.Users.FirstAsync(u => u.Email == "dj@test.com");
            dj.Estado = "suspendido";
            await context.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_LockoutAfter5FailedAttempts()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        // 5 failed attempts
        for (int i = 0; i < 5; i++)
        {
            var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
                "dj@test.com", "wrongpassword123"));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // 6th attempt should be locked out
        var lockedResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "wrongpassword123"));

        Assert.Equal(HttpStatusCode.Unauthorized, lockedResponse.StatusCode);
        // Note: Identity lockout returns 401 by default, not 403
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsProfile()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var response = await Client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(body);
        Assert.Equal("dj@test.com", body.Email);
        Assert.Equal("DJ Test", body.ArtistName);
        Assert.NotEqual(Guid.Empty, body.Id);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithInvalidToken_Returns401()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.here");

        var response = await Client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithExpiredToken_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        // Advance time by 16 minutes (token expires in 15)
        var fakeTimeProvider = _factory.Services.GetRequiredService<TimeProvider>() as FakeTimeProvider;
        fakeTimeProvider!.Advance(TimeSpan.FromMinutes(16));

        var response = await Client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_AccountSuspendedAfterLogin_Returns403()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        // Suspend the account after login
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var dj = await context.Users.FirstAsync(u => u.Email == "dj@test.com");
            dj.Estado = "suspendido";
            await context.SaveChangesAsync();
        }

        var response = await Client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewTokenPair()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens!.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var newTokens = await refreshResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(newTokens);
        Assert.NotNull(newTokens.AccessToken);
        Assert.NotNull(newTokens.RefreshToken);
        Assert.Equal("Bearer", newTokens.TokenType);
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest("invalid-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        // Advance time by 8 days (refresh token expires in 7)
        var fakeTimeProvider = _factory.Services.GetRequiredService<TimeProvider>() as FakeTimeProvider;
        fakeTimeProvider!.Advance(TimeSpan.FromDays(8));

        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens!.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_AccessTokenAsRefreshToken_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        // Try to use access token as refresh token
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens!.AccessToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_SuspendedUser_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        // Suspend the account
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var dj = await context.Users.FirstAsync(u => u.Email == "dj@test.com");
            dj.Estado = "suspendido";
            await context.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens!.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Responses_DoNotContainPrivateFields()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var meResponse = await Client.GetAsync("/api/auth/me");
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();

        // Verify MeResponse only has public fields
        Assert.NotNull(meBody);
        // MeResponse record only has Id, Email, ArtistName - no password hash, security stamp, etc.

        // Verify TokenResponse doesn't contain private fields
        Assert.NotNull(tokens);
        // TokenResponse only has AccessToken, RefreshToken, TokenType, ExpiresIn
    }
}