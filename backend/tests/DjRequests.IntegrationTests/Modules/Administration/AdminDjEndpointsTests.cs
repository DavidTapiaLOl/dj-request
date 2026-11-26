using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Accounts.Dtos;
using DjRequests.Api.Modules.Administration.Dtos;
using DjRequests.Api.Modules.Administration.Provisioning;
using DjRequests.Api.Modules.Administration.Services;
using DjRequests.Api.Modules.Guests.Dtos;
using DjRequests.Api.Modules.Rooms.Dtos;
using DjRequests.Api.Persistence;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Administration;

[Collection(IntegrationCollection.Name)]
public class AdminDjEndpointsTests : IAsyncLifetime
{
    private const string AdminEmail = "admin@test.com";
    private const string AdminPassword = "AdminPass123!";
    private const string DjPassword = "DjPass123456!";
    private static readonly DateTimeOffset ControlledNow =
        new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;
    private WebAppFactory _factory = null!;
    private HttpClient _client = null!;

    public AdminDjEndpointsTests(PostgresFixture postgres)
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

    private async Task CreateAdminAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<AdminProvisioningService>();
        await provisioning.ProvisionAsync("Admin Principal", AdminEmail, AdminPassword);
    }

    private async Task<string> LoginAdminAsync()
    {
        using var response = await Client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new AdminLoginRequest(AdminEmail, AdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task<Guid> RegisterDjAsync(string email, string artistName)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, DjPassword, artistName));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();
        return await context.Users
            .Where(d => d.Email == email)
            .Select(d => d.Id)
            .SingleAsync();
    }

    private async Task<Guid> CreateDjDirectAsync(string email, string artistName)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<DjAccount>>();
        var now = FakeTime.GetUtcNow();
        var dj = new DjAccount
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            Estado = "activo",
            NombreArtistico = artistName,
            CreatedAt = now,
            UpdatedAt = now
        };

        var result = await userManager.CreateAsync(dj, DjPassword);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return dj.Id;
    }

    private async Task<string> LoginDjAsync(string email)
    {
        using var response = await Client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, DjPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task SetDjStateAsync(Guid djId, string estado)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();
        var dj = await context.Users.SingleAsync(d => d.Id == djId);
        dj.Estado = estado;
        await context.SaveChangesAsync();
    }

    private async Task CreateProfileAsync(
        Guid djId,
        string description,
        int startYear,
        string photoFileName)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();

        context.PerfilesDj.Add(new DjProfile
        {
            Id = Guid.NewGuid(),
            DjId = djId,
            Descripcion = description,
            AnioInicio = startYear,
            FotoNombreArchivo = photoFileName,
            RedInstagram = "https://instagram.com/djdetalle",
            RedFacebook = "https://facebook.com/djdetalle",
            RedTiktok = "https://tiktok.com/@djdetalle",
            RedYoutube = "https://youtube.com/@djdetalle",
            RedSoundcloud = "https://soundcloud.com/djdetalle",
            CreatedAt = FakeTime.GetUtcNow(),
            UpdatedAt = FakeTime.GetUtcNow()
        });

        await context.SaveChangesAsync();
    }

    private async Task DeactivateAdminAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AdminAccount>>();
        var admin = await userManager.FindByEmailAsync(AdminEmail);
        Assert.NotNull(admin);

        admin.IsActive = false;
        var result = await userManager.UpdateAsync(admin);
        Assert.True(result.Succeeded);
    }

    private async Task<HttpResponseMessage> GetAsync(string url, string? accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await Client.SendAsync(request);
    }

    private async Task<string> CreateValidGuestTokenAsync(string djEmail)
    {
        using var djClient = _factory.CreateClient();
        djClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var register = await djClient.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(djEmail, DjPassword, "DJ Sala"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await djClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(djEmail, DjPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var tokens = await login.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);
        djClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var now = FakeTime.GetUtcNow();
        var room = await djClient.PostAsJsonAsync(
            "/api/rooms",
            new CreateRoomRequest(
                "Sala Invitado",
                now.AddHours(1).ToString("O"),
                now.AddHours(4).ToString("O"),
                null));
        Assert.Equal(HttpStatusCode.Created, room.StatusCode);

        var roomBody = await room.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.NotNull(roomBody);

        var session = await djClient.PostAsJsonAsync(
            "/api/guest-sessions",
            new CreateGuestSessionRequest(roomBody.Codigo));
        Assert.Equal(HttpStatusCode.Created, session.StatusCode);

        var sessionBody = await session.Content.ReadFromJsonAsync<GuestSessionResponse>();
        Assert.NotNull(sessionBody);
        Assert.NotNull(sessionBody.GuestToken);
        return sessionBody.GuestToken;
    }

    private async Task<HttpResponseMessage> GetWithGuestTokenAsync(string url, string guestToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Guest-Token", guestToken);
        return await Client.SendAsync(request);
    }

    private static void AssertNoStore(HttpResponseMessage response) =>
        Assert.True(response.Headers.CacheControl?.NoStore, "Falta Cache-Control: no-store.");

    private static async Task<(Guid[] Ids, bool HasMore)> ReadIdsAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var ids = root.GetProperty("items").EnumerateArray()
            .Select(item => Guid.Parse(item.GetProperty("id").GetString()!))
            .ToArray();
        return (ids, root.GetProperty("hasMore").GetBoolean());
    }

    [Fact]
    public async Task List_AdminToken_ReturnsPagedDjsOrderedByCreation()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();

        await RegisterDjAsync("order-a@test.com", "Alfa");
        FakeTime.Advance(TimeSpan.FromMinutes(1));
        await RegisterDjAsync("order-b@test.com", "Beta");
        FakeTime.Advance(TimeSpan.FromMinutes(1));
        await RegisterDjAsync("order-c@test.com", "Gamma");

        using var firstResponse = await GetAsync("/api/admin/djs?page=1&pageSize=2", token);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        AssertNoStore(firstResponse);

        var firstRaw = await firstResponse.Content.ReadAsStringAsync();
        using var firstDocument = JsonDocument.Parse(firstRaw);
        var firstRoot = firstDocument.RootElement;

        Assert.Equal(1, firstRoot.GetProperty("page").GetInt32());
        Assert.Equal(2, firstRoot.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, firstRoot.GetProperty("totalCount").GetInt32());
        Assert.True(firstRoot.GetProperty("hasMore").GetBoolean());

        var firstItems = firstRoot.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, firstItems.Length);
        Assert.Equal("Gamma", firstItems[0].GetProperty("artistName").GetString());
        Assert.Equal("Beta", firstItems[1].GetProperty("artistName").GetString());
        Assert.Equal("order-c@test.com", firstItems[0].GetProperty("email").GetString());
        Assert.Equal("activo", firstItems[0].GetProperty("estado").GetString());

        Assert.DoesNotContain("password", firstRaw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", firstRaw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lockout", firstRaw, StringComparison.OrdinalIgnoreCase);

        using var secondResponse = await GetAsync("/api/admin/djs?page=2&pageSize=2", token);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        AssertNoStore(secondResponse);

        using var secondDocument = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        var secondRoot = secondDocument.RootElement;
        Assert.False(secondRoot.GetProperty("hasMore").GetBoolean());

        var secondItems = secondRoot.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(secondItems);
        Assert.Equal("Alfa", secondItems[0].GetProperty("artistName").GetString());
    }

    [Fact]
    public async Task List_SearchAndStateFilters_AreAppliedCaseInsensitively()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();

        var activeId = await RegisterDjAsync("filter-a@test.com", "Luna Azul");
        var suspendedId = await RegisterDjAsync("filter-b@test.com", "Luna Roja");
        await SetDjStateAsync(suspendedId, "suspendido");

        using var searchResponse = await GetAsync("/api/admin/djs?search=LUNA", token);
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        AssertNoStore(searchResponse);

        using var searchDocument = JsonDocument.Parse(await searchResponse.Content.ReadAsStringAsync());
        Assert.Equal(2, searchDocument.RootElement.GetProperty("totalCount").GetInt32());

        using var stateResponse = await GetAsync("/api/admin/djs?estado=SUSPENDIDO", token);
        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);
        AssertNoStore(stateResponse);

        using var stateDocument = JsonDocument.Parse(await stateResponse.Content.ReadAsStringAsync());
        var stateItems = stateDocument.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(stateItems);
        Assert.Equal(suspendedId, Guid.Parse(stateItems[0].GetProperty("id").GetString()!));

        using var combinedResponse = await GetAsync("/api/admin/djs?search=roja&estado=activo", token);
        Assert.Equal(HttpStatusCode.OK, combinedResponse.StatusCode);
        AssertNoStore(combinedResponse);

        using var combinedDocument = JsonDocument.Parse(await combinedResponse.Content.ReadAsStringAsync());
        Assert.Equal(0, combinedDocument.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Empty(combinedDocument.RootElement.GetProperty("items").EnumerateArray());

        Assert.NotEqual(Guid.Empty, activeId);
    }

    [Fact]
    public async Task List_InvalidQuery_Returns400WithNoStore()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();

        var queries = new[]
        {
            "page=0",
            "pageSize=0",
            "pageSize=101",
            "pageSize=abc",
            $"search={new string('a', AdminDjService.MaxSearchLength + 1)}",
            "estado=invalido"
        };

        foreach (var query in queries)
        {
            using var response = await GetAsync($"/api/admin/djs?{query}", token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            AssertNoStore(response);
        }
    }

    [Fact]
    public async Task Get_Detail_ReturnsProfileDataWithoutIdentityFields()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();

        var djId = await RegisterDjAsync("detail@test.com", "DJ Detalle");
        const string photoFileName = "foto-detalle.jpg";
        await CreateProfileAsync(djId, "Perfil publico", 2010, photoFileName);

        using var response = await GetAsync($"/api/admin/djs/{djId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);

        var raw = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<AdminDjDto>(
            raw,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(body);
        Assert.Equal(djId, body.Id);
        Assert.Equal("detail@test.com", body.Email);
        Assert.Equal("DJ Detalle", body.ArtistName);
        Assert.Equal("activo", body.Estado);
        Assert.Equal("Perfil publico", body.Description);
        Assert.Equal(2010, body.StartYear);
        Assert.Equal($"/api/profile-photos/{photoFileName}", body.PhotoUrl);
        Assert.Equal("https://instagram.com/djdetalle", body.SocialLinks.Instagram);
        Assert.Equal("https://facebook.com/djdetalle", body.SocialLinks.Facebook);
        Assert.Equal("https://tiktok.com/@djdetalle", body.SocialLinks.Tiktok);
        Assert.Equal("https://youtube.com/@djdetalle", body.SocialLinks.Youtube);
        Assert.Equal("https://soundcloud.com/djdetalle", body.SocialLinks.Soundcloud);

        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("normalizedEmail", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lockout", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("roles", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_DjWithoutProfile_ReturnsNullProfileFields()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();
        var djId = await RegisterDjAsync("sin-perfil@test.com", "Sin Perfil");

        using var response = await GetAsync($"/api/admin/djs/{djId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);

        var body = await response.Content.ReadFromJsonAsync<AdminDjDto>();
        Assert.NotNull(body);
        Assert.Equal(djId, body.Id);
        Assert.Null(body.Description);
        Assert.Null(body.StartYear);
        Assert.Null(body.PhotoUrl);
        Assert.NotNull(body.SocialLinks);
        Assert.Null(body.SocialLinks.Instagram);
        Assert.Null(body.SocialLinks.Facebook);
        Assert.Null(body.SocialLinks.Tiktok);
        Assert.Null(body.SocialLinks.Youtube);
        Assert.Null(body.SocialLinks.Soundcloud);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404WithNoStore()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();

        using var response = await GetAsync($"/api/admin/djs/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertNoStore(response);
    }

    [Fact]
    public async Task ListAndGet_WithoutValidAdminToken_Return401WithNoStore()
    {
        await CreateAdminAsync();
        var djId = await RegisterDjAsync("token-dj@test.com", "DJ Token");
        var djToken = await LoginDjAsync("token-dj@test.com");

        var requests = new (string Url, string? Token)[]
        {
            ("/api/admin/djs", null),
            ("/api/admin/djs", "invalid.token.here"),
            ("/api/admin/djs", djToken),
            ($"/api/admin/djs/{djId}", djToken)
        };

        foreach (var (url, token) in requests)
        {
            using var response = await GetAsync(url, token);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertNoStore(response);
        }
    }

    [Fact]
    public async Task ListAndGet_InactiveAdmin_Return403WithNoStore()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();
        var djId = await RegisterDjAsync("inactivo-dj@test.com", "DJ Inactivo");

        await DeactivateAdminAsync();

        foreach (var url in new[] { "/api/admin/djs", $"/api/admin/djs/{djId}" })
        {
            using var response = await GetAsync(url, token);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            AssertNoStore(response);
        }
    }

    [Fact]
    public async Task List_SearchByEmail_IsCaseInsensitive()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();

        var expectedId = await RegisterDjAsync("Luna.Email@test.com", "Sin Nombre");
        await RegisterDjAsync("otro@test.com", "Otro DJ");

        using var response = await GetAsync(
            "/api/admin/djs?search=luna.email@TEST.COM",
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt32());

        var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal(expectedId, Guid.Parse(items[0].GetProperty("id").GetString()!));
    }

    [Fact]
    public async Task List_SearchWithOuterSpaces_IsTrimmed()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();

        var expectedId = await RegisterDjAsync("trim@test.com", "Luna Recortada");
        await RegisterDjAsync("otro-trim@test.com", "Otro DJ");

        using var response = await GetAsync(
            "/api/admin/djs?search=%20%20LUNA%20%20",
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt32());

        var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal(expectedId, Guid.Parse(items[0].GetProperty("id").GetString()!));
    }

    [Fact]
    public async Task List_SearchWithLikeWildcards_IsLiteral()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();

        var percentId = await CreateDjDirectAsync("percent@test.com", "Descuento 100%");
        await CreateDjDirectAsync("percent-other@test.com", "Descuento 100 descuento");
        var underscoreId = await CreateDjDirectAsync("underscore@test.com", "DJ a_b");
        await CreateDjDirectAsync("underscore-other@test.com", "DJ aXb");
        var escapeId = await CreateDjDirectAsync("escape@test.com", @"DJ a\b");
        await CreateDjDirectAsync("escape-other@test.com", "DJ ab");

        var searches = new (string Search, Guid ExpectedId)[]
        {
            ("100%", percentId),
            ("a_b", underscoreId),
            (@"a\b", escapeId)
        };

        foreach (var (search, expectedId) in searches)
        {
            using var response = await GetAsync(
                $"/api/admin/djs?search={Uri.EscapeDataString(search)}",
                token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertNoStore(response);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt32());

            var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
            Assert.Single(items);
            Assert.Equal(expectedId, Guid.Parse(items[0].GetProperty("id").GetString()!));
        }
    }

    [Fact]
    public async Task List_PageIntMaxValueWithPageSize100_Returns400WithNoStore()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();

        using var response = await GetAsync(
            $"/api/admin/djs?page={int.MaxValue}&pageSize=100",
            token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertNoStore(response);
    }

    [Fact]
    public async Task List_SameCreatedAt_OrdersByIdWithoutDuplicatePages()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();

        var ids = new List<Guid>();
        for (var index = 0; index < 4; index++)
        {
            ids.Add(await CreateDjDirectAsync($"same-{index}@test.com", $"Mismo {index}"));
        }

        var expected = ids.OrderBy(id => id).ToArray();

        using var firstResponse = await GetAsync("/api/admin/djs?page=1&pageSize=2", token);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        AssertNoStore(firstResponse);

        var first = await ReadIdsAsync(firstResponse);
        Assert.Equal(expected.Take(2), first.Ids);
        Assert.True(first.HasMore);

        using var secondResponse = await GetAsync("/api/admin/djs?page=2&pageSize=2", token);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        AssertNoStore(secondResponse);

        var second = await ReadIdsAsync(secondResponse);
        Assert.Equal(expected.Skip(2), second.Ids);
        Assert.False(second.HasMore);
        Assert.Equal(4, first.Ids.Concat(second.Ids).Distinct().Count());
    }

    [Fact]
    public async Task ListAndGet_WithRealGuestToken_Return401WithNoStore()
    {
        await CreateAdminAsync();
        var djId = await RegisterDjAsync("admin-target@test.com", "DJ Objetivo");
        var guestToken = await CreateValidGuestTokenAsync("sala-dj@test.com");

        foreach (var url in new[] { "/api/admin/djs", $"/api/admin/djs/{djId}" })
        {
            using var response = await GetWithGuestTokenAsync(url, guestToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            AssertNoStore(response);
        }
    }
}
