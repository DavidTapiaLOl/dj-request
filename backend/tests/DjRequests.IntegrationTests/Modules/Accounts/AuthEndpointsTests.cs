using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Accounts.Dtos;
using DjRequests.Api.Persistence;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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

    private async Task<TokenResponse> LoginAndGetTokensAsync(string email = "dj@test.com", string password = "validpassphrase123")
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);
        return tokens;
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
    public async Task Register_NullEmail_Returns400ValidationProblem()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(null!, "validpassphrase123", "DJ Test"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("Email", problem.Errors.Keys);
    }

    [Fact]
    public async Task Register_OmittedEmailField_Returns400ValidationProblem()
    {
        var content = new StringContent(
            """{"password":"validpassphrase123","artistName":"DJ Test"}""",
            Encoding.UTF8,
            "application/json");

        var response = await Client.PostAsync("/api/auth/register", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("Email", problem.Errors.Keys);
    }

    [Fact]
    public async Task Register_EmailOver256Chars_Returns400()
    {
        var longEmail = new string('a', 250) + "@test.com";

        var response = await Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(longEmail, "validpassphrase123", "DJ Test"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Email", problem.Errors.Keys);
    }

    [Fact]
    public async Task Register_NullPassword_Returns400ValidationProblem()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("dj@test.com", null!, "DJ Test"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Password", problem.Errors.Keys);
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
    public async Task Register_ArtistNameWithin100AfterTrim_Returns201AndTrims()
    {
        var artistName = new string('a', 98) + "   ";

        var response = await Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("dj@test.com", "validpassphrase123", artistName));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var context = await _factory.CreateDbContextAsync();
        var dj = await context.Users.SingleAsync(u => u.Email == "dj@test.com");
        Assert.Equal(new string('a', 98), dj.NombreArtistico);
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
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Password", problem.Errors.Keys);
    }

    [Fact]
    public async Task Register_DuplicateEmailCaseInsensitive_Returns409()
    {
        var request1 = new RegisterRequest("DJ@TEST.COM", "validpassphrase123", "DJ One");
        var request2 = new RegisterRequest("dj@test.com", "anotherpassphrase123", "DJ Two");

        await Client.PostAsJsonAsync("/api/auth/register", request1);
        var response = await Client.PostAsJsonAsync("/api/auth/register", request2);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)HttpStatusCode.Conflict, problem.Status);
    }

    [Fact]
    public async Task Register_ConcurrentDuplicate_PersistsSingleAccount_One201One409_No500()
    {
        _factory.EnableRegisterInsertBarrier = true;

        using var client1 = _factory.CreateClient();
        using var client2 = _factory.CreateClient();
        client1.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client2.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new RegisterRequest("race@test.com", "validpassphrase123", "DJ Race");

        var task1 = client1.PostAsJsonAsync("/api/auth/register", payload);
        var task2 = client2.PostAsJsonAsync("/api/auth/register", payload);
        var responses = await Task.WhenAll(task1, task2);

        var statusCodes = responses
            .Select(r => (int)r.StatusCode)
            .OrderBy(c => c)
            .ToArray();
        Assert.Equal(new[] { 201, 409 }, statusCodes);
        Assert.DoesNotContain(responses, r => (int)r.StatusCode >= 500);

        Assert.NotNull(_factory.RegisterInsertBarrier);
        Assert.Equal(2, _factory.RegisterInsertBarrier.Arrivals);

        var conflict = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType?.MediaType);
        var conflictProblem = await conflict.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(conflictProblem);
        Assert.Equal(StatusCodes.Status409Conflict, conflictProblem.Status);

        await using var context = await _factory.CreateDbContextAsync();
        var persistedCount = await context.Users.CountAsync(u => u.Email == "race@test.com");
        Assert.Equal(1, persistedCount);
    }

    [Fact]
    public async Task Register_ExtraClientFields_AreIgnored_NoPrivilegeOrIdentityControl()
    {
        var rawJson = """
        {
          "email": "manip@test.com",
          "password": "validpassphrase123",
          "artistName": "DJ Manip",
          "id": "11111111-1111-1111-1111-111111111111",
          "estado": "suspendido",
          "roles": ["admin"],
          "normalizedEmail": "OTRO@TEST.COM",
          "passwordHash": "not-a-hash",
          "lockoutEnabled": false,
          "accessFailedCount": 99,
          "securityStamp": "client-controlled"
        }
        """;
        var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/auth/register", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var context = await _factory.CreateDbContextAsync();
        var dj = await context.Users.SingleAsync(u => u.Email == "manip@test.com");
        Assert.Equal("activo", dj.Estado);
        Assert.NotEqual(Guid.Parse("11111111-1111-1111-1111-111111111111"), dj.Id);
        Assert.Equal("MANIP@TEST.COM", dj.NormalizedEmail);
        Assert.True(dj.LockoutEnabled);
        Assert.Equal(0, dj.AccessFailedCount);

        var roleCount = await context.UserRoles.CountAsync(ur => ur.UserId == dj.Id);
        Assert.Equal(0, roleCount);

        var login = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "manip@test.com", "validpassphrase123"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Register_ResponseJson_ExposesOnlyPublicProperties()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(raw);
        var propertyNames = doc.RootElement.EnumerateObject()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "artistName", "createdAt", "email", "id" }, propertyNames);

        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lockout", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("estado", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("normalizedEmail", raw, StringComparison.OrdinalIgnoreCase);
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
    public async Task Login_NullEmail_Returns400ValidationProblem()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(null!, "validpassphrase123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("Email", problem.Errors.Keys);
    }

    [Fact]
    public async Task Login_OmittedEmailField_Returns400ValidationProblem()
    {
        var content = new StringContent(
            """{"password":"validpassphrase123"}""",
            Encoding.UTF8,
            "application/json");

        var response = await Client.PostAsync("/api/auth/login", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Email", problem.Errors.Keys);
    }

    [Fact]
    public async Task Login_InvalidEmailFormat_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("not-an-email", "validpassphrase123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Email", problem.Errors.Keys);
    }

    [Fact]
    public async Task Login_NullPassword_Returns400ValidationProblem()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("dj@test.com", null!));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Password", problem.Errors.Keys);
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
    public async Task Login_AllFailureModes_ReturnSameGenericProblemDetails()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var wrongPassword = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "wrongpassword123"));
        var unknownUser = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "unknown@test.com", "anypassphrase123"));

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var dj = await context.Users.FirstAsync(u => u.Email == "dj@test.com");
            dj.Estado = "suspendido";
            await context.SaveChangesAsync();
        }

        var suspendedUser = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, suspendedUser.StatusCode);

        ProblemDetails? first = null;
        foreach (var response in new[] { wrongPassword, unknownUser, suspendedUser })
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
            Assert.False(string.IsNullOrWhiteSpace(problem.Detail));

            if (first is null)
            {
                first = problem;
            }
            else
            {
                Assert.Equal(first.Title, problem.Title);
                Assert.Equal(first.Detail, problem.Detail);
            }
        }

        var unknownUserBody = await unknownUser.Content.ReadAsStringAsync();
        Assert.DoesNotContain("unknown@test.com", unknownUserBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wrongpassword123", unknownUserBody, StringComparison.OrdinalIgnoreCase);
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
    public async Task Login_LockoutAfter5FailedAttempts_CorrectPasswordRejected_AndPersisted()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        // 4 failed attempts: below the threshold (MaxFailedAccessAttempts = 5), no lockout yet
        for (int i = 0; i < 4; i++)
        {
            var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
                "dj@test.com", "wrongpassword123"));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        await using (var beforeThreshold = await _factory.CreateDbContextAsync())
        {
            var djBefore = await beforeThreshold.Users.FirstAsync(u => u.Email == "dj@test.com");
            Assert.Equal(4, djBefore.AccessFailedCount);
            Assert.Null(djBefore.LockoutEnd);
        }

        // The 5th attempt crosses the threshold and engages the lockout
        var fifthAttempt = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "wrongpassword123"));
        Assert.Equal(HttpStatusCode.Unauthorized, fifthAttempt.StatusCode);

        // The correct password must still be rejected while locked out
        var correctPassword = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        Assert.Equal(HttpStatusCode.Unauthorized, correctPassword.StatusCode);

        // The lockout is persisted by Identity. In .NET 10, AccessFailedAsync resets
        // AccessFailedCount to 0 when the lockout engages; LockoutEnd carries the state.
        var timeProvider = _factory.Services.GetRequiredService<TimeProvider>();
        await using var context = await _factory.CreateDbContextAsync();
        var dj = await context.Users.FirstAsync(u => u.Email == "dj@test.com");
        Assert.True(dj.LockoutEnabled, "LockoutEnabled deberia ser true");
        Assert.NotNull(dj.LockoutEnd);
        Assert.True(dj.LockoutEnd!.Value > timeProvider.GetUtcNow(),
            $"LockoutEnd={dj.LockoutEnd:o} pero ahora={timeProvider.GetUtcNow():o}");
        Assert.Equal(0, dj.AccessFailedCount);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsProfile()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var tokens = await LoginAndGetTokensAsync();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await Client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(body);
        Assert.Equal("dj@test.com", body.Email);
        Assert.Equal("DJ Test", body.ArtistName);
        Assert.NotEqual(Guid.Empty, body.Id);
    }

    [Fact]
    public async Task Me_ResponseJson_ExposesOnlyPublicProperties()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));
        var tokens = await LoginAndGetTokensAsync();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await Client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(raw);
        var propertyNames = doc.RootElement.EnumerateObject()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[] { "artistName", "email", "id" }, propertyNames);

        Assert.DoesNotContain("estado", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lockout", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("createdAt", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401ProblemDetails()
    {
        var response = await Client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("WWW-Authenticate"));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
    }

    [Fact]
    public async Task Me_WithInvalidToken_Returns401ProblemDetails()
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.token.here");

        var response = await Client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("invalid.token.here", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Me_WithExpiredToken_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var tokens = await LoginAndGetTokensAsync();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        // Advance time by 16 minutes (token expires in 15)
        var fakeTimeProvider = _factory.Services.GetRequiredService<TimeProvider>() as FakeTimeProvider;
        fakeTimeProvider!.Advance(TimeSpan.FromMinutes(16));

        var response = await Client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_AccountSuspendedAfterLogin_Returns403ProblemDetails()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var tokens = await LoginAndGetTokensAsync();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        // Suspend the account after login
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var dj = await context.Users.FirstAsync(u => u.Email == "dj@test.com");
            dj.Estado = "suspendido";
            await context.SaveChangesAsync();
        }

        var response = await Client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status403Forbidden, problem.Status);
    }

    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewTokenPair()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var tokens = await LoginAndGetTokensAsync();

        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var newTokens = await refreshResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(newTokens);
        Assert.NotNull(newTokens.AccessToken);
        Assert.NotNull(newTokens.RefreshToken);
        Assert.Equal("Bearer", newTokens.TokenType);
    }

    [Fact]
    public async Task Refresh_NullOrEmptyToken_Returns400ValidationProblem()
    {
        var nullResponse = await Client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(null!));
        Assert.Equal(HttpStatusCode.BadRequest, nullResponse.StatusCode);
        Assert.Equal("application/problem+json", nullResponse.Content.Headers.ContentType?.MediaType);
        var nullProblem = await nullResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(nullProblem);
        Assert.Contains("RefreshToken", nullProblem.Errors.Keys);

        var emptyResponse = await Client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);
        var emptyProblem = await emptyResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(emptyProblem);
        Assert.Contains("RefreshToken", emptyProblem.Errors.Keys);
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401ProblemDetails()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest("invalid-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var tokens = await LoginAndGetTokensAsync();

        // Advance time by 8 days (refresh token expires in 7)
        var fakeTimeProvider = _factory.Services.GetRequiredService<TimeProvider>() as FakeTimeProvider;
        fakeTimeProvider!.Advance(TimeSpan.FromDays(8));

        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ExactlyAtExpirationBoundary_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var tokens = await LoginAndGetTokensAsync();

        // Advance exactly to the 7-day expiration instant
        var fakeTimeProvider = _factory.Services.GetRequiredService<TimeProvider>() as FakeTimeProvider;
        fakeTimeProvider!.Advance(TimeSpan.FromDays(7));

        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Refresh_AccessTokenAsRefreshToken_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var tokens = await LoginAndGetTokensAsync();

        // Try to use access token as refresh token
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.AccessToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ChangedSecurityStamp_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var tokens = await LoginAndGetTokensAsync();

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var dj = await context.Users.FirstAsync(u => u.Email == "dj@test.com");
            dj.SecurityStamp = Guid.NewGuid().ToString();
            await context.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Refresh_TicketWithoutSecurityStampClaim_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        Guid userId;
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var dj = await context.Users.FirstAsync(u => u.Email == "dj@test.com");
            userId = dj.Id;
        }

        var timeProvider = _factory.Services.GetRequiredService<TimeProvider>();
        var bearerOptions = _factory.Services
            .GetRequiredService<IOptionsMonitor<BearerTokenOptions>>()
            .Get(IdentityConstants.BearerScheme);

        var identity = new ClaimsIdentity(IdentityConstants.BearerScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        var principal = new ClaimsPrincipal(identity);

        var now = timeProvider.GetUtcNow();
        var ticket = new AuthenticationTicket(
            principal,
            new AuthenticationProperties
            {
                IssuedUtc = now,
                ExpiresUtc = now.Add(TimeSpan.FromDays(7))
            },
            IdentityConstants.BearerScheme);

        var craftedToken = bearerOptions.RefreshTokenProtector.Protect(ticket);

        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(craftedToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Refresh_SuspendedUser_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var tokens = await LoginAndGetTokensAsync();

        // Suspend the account
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var dj = await context.Users.FirstAsync(u => u.Email == "dj@test.com");
            dj.Estado = "suspendido";
            await context.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Responses_DoNotContainPrivateFields()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var tokens = await LoginAndGetTokensAsync();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

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
