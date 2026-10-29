using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DjRequests.Api.Modules.Accounts.Dtos;
using DjRequests.Api.Modules.Rooms.Dtos;
using DjRequests.IntegrationTests.Fixtures;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Rooms;

[Collection(IntegrationCollection.Name)]
public class RoomLanInviteUrlTests : IAsyncLifetime
{
    private const string Password = "validpassphrase123";
    private readonly PostgresFixture _postgres;
    private WebAppFactory _factory = null!;
    private HttpClient _client = null!;

    public RoomLanInviteUrlTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_postgres, new Dictionary<string, string?>
        {
            ["Frontend:BaseUrl"] = "http://192.168.68.107:4200",
            ["Frontend:AllowHttpLan"] = "true",
        });
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Detail_ReturnsInviteUrlWithConfiguredLanBaseUrl()
    {
        var register = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("lan@test.com", Password, "DJ LAN"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("lan@test.com", Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var now = DateTimeOffset.UtcNow;
        var create = await _client.PostAsJsonAsync("/api/rooms", new CreateRoomRequest(
            "Sala LAN",
            now.AddHours(1).ToString("O"),
            now.AddHours(3).ToString("O"),
            "America/Mexico_City"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.NotNull(created);

        var detail = await _client.GetAsync($"/api/rooms/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var room = await detail.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.NotNull(room);
        Assert.Equal($"http://192.168.68.107:4200/join/{created.Codigo}", room.InviteUrl);
    }
}
