using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DjRequests.Api.Modules.Accounts.Dtos;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Accounts;


[Collection(IntegrationCollection.Name)]
public class RateLimitingTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private WebAppFactory _factory = null!;
    private HttpClient _client = null!;

    public RateLimitingTests(PostgresFixture postgres)
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

    private HttpClient CreateClient(string? ip = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (ip is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Client-IP", ip);
        }
        return client;
    }

    [Fact]
    public async Task Register_RateLimit_Exceeds5PerMinute_Returns429()
    {
        for (int i = 0; i < 5; i++)
        {
            var response = await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
                $"dj{i}@test.com", "validpassphrase123", $"DJ {i}"));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var limitedResponse = await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj6@test.com", "validpassphrase123", "DJ 6"));

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);

        Assert.True(limitedResponse.Headers.TryGetValues("Retry-After", out var values));
        var retryAfter = values.FirstOrDefault();
        Assert.NotNull(retryAfter);
        Assert.True(int.TryParse(retryAfter, out var seconds));
        Assert.True(seconds > 0 && seconds <= 60);
    }

    [Fact]
    public async Task Login_RateLimit_Exceeds10PerMinute_Returns429()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        for (int i = 0; i < 10; i++)
        {
            var response = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
                "dj@test.com", "validpassphrase123"));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var limitedResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        Assert.True(limitedResponse.Headers.TryGetValues("Retry-After", out _));
    }

    [Fact]
    public async Task Refresh_RateLimit_Exceeds20PerMinute_Returns429()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        for (int i = 0; i < 20; i++)
        {
            var response = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(
                tokens!.RefreshToken));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var limitedResponse = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(
            tokens!.RefreshToken));

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        Assert.True(limitedResponse.Headers.TryGetValues("Retry-After", out _));
    }

    [Fact]
    public async Task Register_PerIPLimit_IPA_Blocked_IPB_KeepsFullQuota()
    {
        using var clientA = CreateClient("203.0.113.10");
        using var clientB = CreateClient("203.0.113.20");

        for (int i = 0; i < 5; i++)
        {
            var response = await clientA.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
                $"ipA{i}@test.com", "validpassphrase123", $"DJ A{i}"));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var blockedA = await clientA.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "ipA5@test.com", "validpassphrase123", "DJ A5"));

        Assert.Equal(HttpStatusCode.TooManyRequests, blockedA.StatusCode);
        Assert.Equal("application/problem+json", blockedA.Content.Headers.ContentType?.MediaType);
        Assert.True(blockedA.Headers.TryGetValues("Retry-After", out _));
        var problem = await blockedA.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status429TooManyRequests, problem.Status);

        for (int i = 0; i < 5; i++)
        {
            var response = await clientB.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
                $"ipB{i}@test.com", "validpassphrase123", $"DJ B{i}"));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var blockedB = await clientB.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "ipB5@test.com", "validpassphrase123", "DJ B5"));
        Assert.Equal(HttpStatusCode.TooManyRequests, blockedB.StatusCode);
    }

    [Fact]
    public async Task Login_PerIPLimit_IPA_Blocked_IPB_KeepsFullQuota()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        using var clientA = CreateClient("203.0.113.11");
        using var clientB = CreateClient("203.0.113.21");

        for (int i = 0; i < 10; i++)
        {
            var response = await clientA.PostAsJsonAsync("/api/auth/login", new LoginRequest(
                "dj@test.com", "validpassphrase123"));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var blockedA = await clientA.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        Assert.Equal(HttpStatusCode.TooManyRequests, blockedA.StatusCode);

        for (int i = 0; i < 10; i++)
        {
            var response = await clientB.PostAsJsonAsync("/api/auth/login", new LoginRequest(
                "dj@test.com", "validpassphrase123"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var blockedB = await clientB.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        Assert.Equal(HttpStatusCode.TooManyRequests, blockedB.StatusCode);
    }

    [Fact]
    public async Task Refresh_PerIPLimit_IndependentFromLoginLimit()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "dj@test.com", "validpassphrase123", "DJ Test"));

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);

        using var clientA = CreateClient("203.0.113.12");

        for (int i = 0; i < 10; i++)
        {
            var response = await clientA.PostAsJsonAsync("/api/auth/login", new LoginRequest(
                "dj@test.com", "validpassphrase123"));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var blockedLogin = await clientA.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "dj@test.com", "validpassphrase123"));
        Assert.Equal(HttpStatusCode.TooManyRequests, blockedLogin.StatusCode);

        for (int i = 0; i < 20; i++)
        {
            var response = await clientA.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(
                tokens.RefreshToken));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var blockedRefresh = await clientA.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(
            tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.TooManyRequests, blockedRefresh.StatusCode);
    }

    [Fact]
    public async Task XForwardedFor_DoesNotBypassLimit()
    {
        using var clientA = CreateClient("203.0.113.13");

        for (int i = 0; i < 5; i++)
        {
            var response = await clientA.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
                $"xffA{i}@test.com", "validpassphrase123", $"DJ X{i}"));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new RegisterRequest(
                "xffA5@test.com", "validpassphrase123", "DJ X5"))
        };
        request.Headers.Add("X-Forwarded-For", "198.51.100.77");

        var responseWithXff = await clientA.SendAsync(request);

        Assert.Equal(HttpStatusCode.TooManyRequests, responseWithXff.StatusCode);
    }

    [Fact]
    public async Task Fallback_UnknownIP_AlsoRateLimited()
    {
        using var client = CreateClient("none");

        for (int i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
                $"unknown{i}@test.com", "validpassphrase123", $"DJ U{i}"));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var limitedResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "unknown5@test.com", "validpassphrase123", "DJ U5"));

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        Assert.Equal("application/problem+json", limitedResponse.Content.Headers.ContentType?.MediaType);
        Assert.True(limitedResponse.Headers.TryGetValues("Retry-After", out _));
    }
}
