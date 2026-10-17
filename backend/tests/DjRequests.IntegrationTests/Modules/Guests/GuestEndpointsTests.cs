using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Accounts.Dtos;
using DjRequests.Api.Modules.Guests.Dtos;
using DjRequests.Api.Modules.Rooms.Dtos;
using DjRequests.Api.Persistence;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Guests;

[Collection(IntegrationCollection.Name)]
public class GuestEndpointsTests : IAsyncLifetime
{
    private const string Password = "validpassphrase123";
    private readonly PostgresFixture _postgres;
    private WebAppFactory _factory = null!;
    private HttpClient _client = null!;

    public GuestEndpointsTests(PostgresFixture postgres)
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

    private FakeTimeProvider FakeTime => (FakeTimeProvider)_factory.Services.GetRequiredService<TimeProvider>();

    private static string FormatIso(DateTimeOffset value) => value.ToString("O");

    private static (string Token, string Hash) MakeToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return (token, hash);
    }

    #region Helpers

    private async Task RegisterAndLoginAsync(HttpClient client, string email = "dj@test.com")
    {
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, Password, "DJ Test"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task LoginAgainAsync(HttpClient client, string email = "dj@test.com")
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task<RoomResponse> CreateRoomAsync(HttpClient client, DateTimeOffset now, string name = "Sala Test")
    {
        var request = new CreateRoomRequest(name, FormatIso(now.AddHours(1)), FormatIso(now.AddHours(4)), null);
        var response = await client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.NotNull(room);
        return room;
    }

    private async Task<(Guid RoomId, string Code)> CreateRoomAndGetCodeAsync()
    {
        await RegisterAndLoginAsync(Client);
        var room = await CreateRoomAsync(Client, FakeTime.UtcNow);
        return (room.Id, room.Codigo);
    }

    private async Task<string> CreateSessionAsync(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest(code));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<GuestSessionResponse>();
        Assert.NotNull(session);
        return session.GuestToken;
    }

    private static HttpRequestMessage GuestGet(Guid roomId, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/guest/rooms/{roomId}");
        if (token is not null)
        {
            request.Headers.Add("X-Guest-Token", token);
        }
        return request;
    }

    #endregion

    #region POST /api/guest/sessions

    [Fact]
    public async Task CreateSession_ValidCode_BeforeEvent_Returns201WithToken()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();

        var response = await Client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest(code));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<GuestSessionResponse>();
        Assert.NotNull(session);
        Assert.Equal(43, session.GuestToken!.Length);
        Assert.NotNull(session.Room);
        Assert.Equal(roomId, session.Room.Id);
        Assert.Equal("Sala Test", session.Room.Name);
        Assert.Equal("abierta", session.Room.State);
        Assert.True(session.Room.CanReceiveRequests);
        Assert.Equal(10, session.Room.MaxSolicitudesSesion);
        Assert.Equal(2, session.Room.MaxPlaylistsSesion);
        Assert.Equal(30, session.Room.IntervaloEnvioSegundos);
    }

    [Fact]
    public async Task CreateSession_DuringEvent_Returns201()
    {
        var (_, code) = await CreateRoomAndGetCodeAsync();

        FakeTime.UtcNow = FakeTime.UtcNow.AddHours(2);

        var response = await Client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest(code));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateSession_CodeTrimmedAndUppercased_Returns201()
    {
        var (_, code) = await CreateRoomAndGetCodeAsync();

        var request = new CreateGuestSessionRequest("  " + code.ToLowerInvariant() + "  ");
        var response = await Client.PostAsJsonAsync("/api/guest/sessions", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SHORT")]
    [InlineData("TOOLONGCODE123")]
    [InlineData("INVALIDCHARS!")]
    [InlineData("CODEWITHIO1")]
    public async Task CreateSession_InvalidCode_Returns400(string code)
    {
        var response = await Client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest(code));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSession_NullCode_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest(null!));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSession_UnknownCode_Returns404()
    {
        var response = await Client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest("ZZZZZZZZ29"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateSession_RoomPaused_Returns409()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();

        await Client.PatchAsJsonAsync($"/api/rooms/{roomId}/state", new UpdateRoomStateRequest("pausada", 1));

        var response = await Client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest(code));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateSession_RoomClosed_Returns409()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();

        await Client.PatchAsJsonAsync($"/api/rooms/{roomId}/state", new UpdateRoomStateRequest("cerrada", 1));

        var response = await Client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest(code));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateSession_RoomEnded_Returns404()
    {
        var (_, code) = await CreateRoomAndGetCodeAsync();

        FakeTime.UtcNow = FakeTime.UtcNow.AddHours(5);

        var response = await Client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest(code));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateSession_DjSuspended_Returns404()
    {
        var (_, code) = await CreateRoomAndGetCodeAsync();

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var dj = await context.Users.FirstAsync();
            dj.Estado = "suspendido";
            await context.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest(code));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateSession_TwoSessionsSameRoom_DifferentTokens()
    {
        var (_, code) = await CreateRoomAndGetCodeAsync();

        var token1 = await CreateSessionAsync(Client, code);
        var token2 = await CreateSessionAsync(Client, code);

        Assert.NotEqual(token1, token2);

        await using var context = await _factory.CreateDbContextAsync();
        var count = await context.SesionesInvitado.CountAsync(s => s.Sala.Codigo == code);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CreateSession_OnlyHashPersisted_NoOriginalTokenInDb()
    {
        var (_, code) = await CreateRoomAndGetCodeAsync();

        var token = await CreateSessionAsync(Client, code);

        await using var context = await _factory.CreateDbContextAsync();
        var sesion = await context.SesionesInvitado.FirstAsync(s => s.Sala.Codigo == code);

        Assert.Equal(64, sesion.TokenHash.Length);
        Assert.NotEqual(token, sesion.TokenHash);
        Assert.DoesNotContain(token, sesion.TokenHash);
    }

    [Fact]
    public async Task CreateSession_ClientFieldsIgnored_ServerValuesReturned()
    {
        var (_, code) = await CreateRoomAndGetCodeAsync();

        var raw = $@"{{ ""code"": ""{code}"", ""guestToken"": ""client-should-not-set"", ""expiresAt"": ""2000-01-01T00:00:00Z"" }}";
        var content = new StringContent(raw, System.Text.Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/guest/sessions", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<GuestSessionResponse>();
        Assert.NotNull(session);
        Assert.NotEqual("client-should-not-set", session.GuestToken);
        Assert.NotEqual(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), session.ExpiresAt);
    }

    [Fact]
    public async Task CreateSession_TokenCollision_RetriesAndSucceeds()
    {
        var colliding = MakeToken();
        var free = MakeToken();

        var factory = new WebAppFactory(_postgres)
        {
            CustomGuestTokenGenerator = new DeterministicGuestTokenGenerator(
                (colliding.Token, colliding.Hash),
                (free.Token, free.Hash))
        };
        await factory.InitializeAsync();

        try
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            await RegisterAndLoginAsync(client, "collision@test.com");
            var fakeTime = (FakeTimeProvider)factory.Services.GetRequiredService<TimeProvider>();
            var room = await CreateRoomAsync(client, fakeTime.UtcNow);

            await using (var seed = await factory.CreateDbContextAsync())
            {
                var now = DateTimeOffset.UtcNow;
                seed.SesionesInvitado.Add(new SesionInvitado
                {
                    Id = Guid.NewGuid(),
                    SalaId = room.Id,
                    TokenHash = colliding.Hash,
                    CreatedAt = now,
                    ExpiresAt = now.AddHours(4)
                });
                await seed.SaveChangesAsync();
            }

            var response = await client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest(room.Codigo));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var session = await response.Content.ReadFromJsonAsync<GuestSessionResponse>();
            Assert.Equal(free.Token, session!.GuestToken);
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }

    #endregion

    #region GET /api/guest/rooms/{roomId}

    [Fact]
    public async Task GetRoom_ValidToken_Returns200WithRoomDto()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestGet(roomId, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var room = await response.Content.ReadFromJsonAsync<GuestRoomResponse>();
        Assert.NotNull(room);
        Assert.Equal(roomId, room.Room.Id);
        Assert.Equal("Sala Test", room.Room.Name);
        Assert.True(room.ExpiresAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task GetRoom_NoToken_Returns401()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();

        var response = await Client.SendAsync(GuestGet(roomId, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_MalformedToken_Returns401()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();

        var response = await Client.SendAsync(GuestGet(roomId, "not-a-valid-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_TokenWrongLength_Returns401()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();

        var response = await Client.SendAsync(GuestGet(roomId, "short"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_UnknownToken_Returns401()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();
        var (unknown, _) = MakeToken();

        var response = await Client.SendAsync(GuestGet(roomId, unknown));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_TokenFromOtherRoom_Returns404()
    {
        var (_, code1) = await CreateRoomAndGetCodeAsync();
        var token1 = await CreateSessionAsync(Client, code1);

        var room2 = await CreateRoomAsync(Client, FakeTime.UtcNow, "Sala Dos");

        var response = await Client.SendAsync(GuestGet(room2.Id, token1));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_NonExistentRoom_Returns404()
    {
        var (_, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestGet(Guid.NewGuid(), token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_RevokedToken_Returns401()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateSessionAsync(Client, code);

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var session = await context.SesionesInvitado.FirstAsync(s => s.Sala.Codigo == code);
            session.RevokedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync();
        }

        var response = await Client.SendAsync(GuestGet(roomId, token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_ExpiredToken_Returns401()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateSessionAsync(Client, code);

        FakeTime.UtcNow = FakeTime.UtcNow.AddHours(5);

        var response = await Client.SendAsync(GuestGet(roomId, token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_ShortenedEvent_Returns404()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateSessionAsync(Client, code);

        await Client.PutAsJsonAsync($"/api/rooms/{roomId}", new UpdateRoomRequest(
            1, "Sala Acortada", FormatIso(FakeTime.UtcNow.AddHours(1)), FormatIso(FakeTime.UtcNow.AddHours(2)), null));

        FakeTime.UtcNow = FakeTime.UtcNow.AddHours(3);

        var response = await Client.SendAsync(GuestGet(roomId, token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_ExtendedEvent_KeepsOriginalExpiresAt()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateSessionAsync(Client, code);

        DateTimeOffset originalExpiresAt;
        await using (var seed = await _factory.CreateDbContextAsync())
        {
            var session = await seed.SesionesInvitado.FirstAsync(s => s.Sala.Codigo == code);
            originalExpiresAt = session.ExpiresAt;
        }

        await Client.PutAsJsonAsync($"/api/rooms/{roomId}", new UpdateRoomRequest(
            1, "Sala Extendida", FormatIso(FakeTime.UtcNow.AddHours(1)), FormatIso(FakeTime.UtcNow.AddHours(10)), null));

        var response = await Client.SendAsync(GuestGet(roomId, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var room = await response.Content.ReadFromJsonAsync<GuestRoomResponse>();
        Assert.Equal(originalExpiresAt, room!.ExpiresAt);
    }

    [Fact]
    public async Task GetRoom_SessionExpiresBeforeEvent_Returns401()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateSessionAsync(Client, code);

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var session = await context.SesionesInvitado.FirstAsync(s => s.Sala.Codigo == code);
            session.ExpiresAt = FakeTime.UtcNow.AddHours(1);
            await context.SaveChangesAsync();
        }

        FakeTime.UtcNow = FakeTime.UtcNow.AddHours(2);

        var response = await Client.SendAsync(GuestGet(roomId, token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_PausedRoom_Returns200WithCanReceiveRequestsFalse()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateSessionAsync(Client, code);

        await Client.PatchAsJsonAsync($"/api/rooms/{roomId}/state", new UpdateRoomStateRequest("pausada", 1));

        var response = await Client.SendAsync(GuestGet(roomId, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var room = await response.Content.ReadFromJsonAsync<GuestRoomResponse>();
        Assert.False(room!.Room.CanReceiveRequests);
    }

    [Fact]
    public async Task GetRoom_DjSuspended_Returns404()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateSessionAsync(Client, code);

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var dj = await context.Users.FirstAsync();
            dj.Estado = "suspendido";
            await context.SaveChangesAsync();
        }

        var response = await Client.SendAsync(GuestGet(roomId, token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_NeverReturnsGuestToken()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestGet(roomId, token));

        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token, json);
        Assert.DoesNotContain("guestToken", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRoom_DjBearerToken_NotAccepted()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/guest/rooms/{roomId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            Client.DefaultRequestHeaders.Authorization!.Parameter);
        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GuestToken_NotValidOnDjEndpoints()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateSessionAsync(Client, code);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/rooms/{roomId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Rate limiting

    private sealed record DedicatedSetup(WebAppFactory Factory, HttpClient Client, Guid RoomId, string Code);

    private async Task<DedicatedSetup> CreateDedicatedSetupAsync(string email)
    {
        var factory = new WebAppFactory(_postgres);
        await factory.InitializeAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        await RegisterAndLoginAsync(client, email);

        var fakeTime = (FakeTimeProvider)factory.Services.GetRequiredService<TimeProvider>();
        var room = await CreateRoomAsync(client, fakeTime.UtcNow);

        return new DedicatedSetup(factory, client, room.Id, room.Codigo);
    }

    [Fact]
    public async Task CreateSession_RateLimitExceeded_Returns429()
    {
        var setup = await CreateDedicatedSetupAsync("rl-post@test.com");

        try
        {
            for (int i = 0; i < 60; i++)
            {
                var response = await setup.Client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest(setup.Code));
                Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
                response.Dispose();
            }

            var limited = await setup.Client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest(setup.Code));
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            Assert.True(limited.Headers.TryGetValues("Retry-After", out _));
        }
        finally
        {
            await setup.Factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetRoom_RateLimitExceeded_Returns429()
    {
        var setup = await CreateDedicatedSetupAsync("rl-get@test.com");
        var token = await CreateSessionAsync(setup.Client, setup.Code);

        try
        {
            for (int i = 0; i < 120; i++)
            {
                var response = await setup.Client.SendAsync(GuestGet(setup.RoomId, token));
                Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
                response.Dispose();
            }

            var limited = await setup.Client.SendAsync(GuestGet(setup.RoomId, token));
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            Assert.True(limited.Headers.TryGetValues("Retry-After", out _));
        }
        finally
        {
            await setup.Factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetRoom_InvalidTokenRateLimitedByIp()
    {
        var setup = await CreateDedicatedSetupAsync("rl-invalid@test.com");

        try
        {
            for (int i = 0; i < 120; i++)
            {
                var response = await setup.Client.SendAsync(GuestGet(setup.RoomId, "invalid-token-" + i));
                Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
                response.Dispose();
            }

            var limited = await setup.Client.SendAsync(GuestGet(setup.RoomId, "another-invalid"));
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        }
        finally
        {
            await setup.Factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task XForwardedFor_DoesNotBypassGuestRateLimit()
    {
        var setup = await CreateDedicatedSetupAsync("rl-xfwd@test.com");

        try
        {
            for (int i = 0; i < 120; i++)
            {
                var request = GuestGet(setup.RoomId, "invalid-" + i);
                request.Headers.Add("X-Forwarded-For", "198.51.100.77");
                var response = await setup.Client.SendAsync(request);
                Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
                response.Dispose();
            }

            var limitedRequest = GuestGet(setup.RoomId, "another-invalid");
            limitedRequest.Headers.Add("X-Forwarded-For", "198.51.100.77");
            var limited = await setup.Client.SendAsync(limitedRequest);
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        }
        finally
        {
            await setup.Factory.DisposeAsync();
        }
    }

    #endregion

    #region Revalidación post-lock (concurrencia)

    [Fact]
    public async Task CreateSession_EventEndsWhileWaitingForLock_Returns404()
    {
        var factory = new WebAppFactory(_postgres) { EnableGuestLockPause = true };
        await factory.InitializeAsync();

        try
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var lockPause = factory.GuestLockPause!;
            var fakeTime = (FakeTimeProvider)factory.Services.GetRequiredService<TimeProvider>();

            await RegisterAndLoginAsync(client, "lock@test.com");
            var room = await CreateRoomAsync(client, fakeTime.UtcNow);
            var now0 = fakeTime.UtcNow;

            var postTask = client.PostAsJsonAsync("/api/guest/sessions", new CreateGuestSessionRequest(room.Codigo));

            await lockPause.Paused.WaitAsync(TimeSpan.FromSeconds(10));
            fakeTime.UtcNow = now0.AddHours(5);
            lockPause.Resume();

            var response = await postTask;
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await factory.DisposeAsync();
        }
    }

    #endregion
}