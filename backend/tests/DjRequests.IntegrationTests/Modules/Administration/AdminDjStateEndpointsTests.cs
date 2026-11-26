using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Accounts.Dtos;
using DjRequests.Api.Modules.Administration.Dtos;
using DjRequests.Api.Modules.Administration.Provisioning;
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
public class AdminDjStateEndpointsTests : IAsyncLifetime
{
    private const string AdminEmail = "admin-estado@test.com";
    private const string AdminPassword = "AdminEstado123!";
    private const string DjPassword = "DjEstado123456!";
    private static readonly DateTimeOffset ControlledNow =
        new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _postgres;
    private WebAppFactory _factory = null!;
    private HttpClient _client = null!;

    public AdminDjStateEndpointsTests(PostgresFixture postgres)
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
        await provisioning.ProvisionAsync("Admin Estado", AdminEmail, AdminPassword);
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

    private async Task<Guid> GetAdminIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AdminAccount>>();
        var admin = await userManager.FindByEmailAsync(AdminEmail);
        Assert.NotNull(admin);
        return admin.Id;
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

    private async Task<HttpResponseMessage> LoginDjRawAsync(string email) =>
        await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, DjPassword));

    private async Task<TokenResponse> LoginDjAsync(string email)
    {
        using var response = await LoginDjRawAsync(email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);
        return tokens;
    }

    private async Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(refreshToken));

    private HttpClient CreateClientWithBearer(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<RoomResponse> CreateRoomAsync(string djAccessToken, DateTimeOffset endsAt)
    {
        using var client = CreateClientWithBearer(djAccessToken);
        var now = FakeTime.GetUtcNow();

        using var response = await client.PostAsJsonAsync(
            "/api/rooms",
            new CreateRoomRequest(
                "Sala Estado",
                now.AddMinutes(1).ToString("O"),
                endsAt.ToString("O"),
                null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.NotNull(room);
        return room;
    }

    private async Task<(HttpStatusCode Status, string? Token)> CreateGuestSessionAsync(string code)
    {
        using var response = await Client.PostAsJsonAsync(
            "/api/guest-sessions",
            new CreateGuestSessionRequest(code));

        if (response.StatusCode != HttpStatusCode.Created)
        {
            return (response.StatusCode, null);
        }

        var body = await response.Content.ReadFromJsonAsync<GuestSessionResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.GuestToken);
        return (response.StatusCode, body.GuestToken);
    }

    private async Task<HttpResponseMessage> PatchStateAsync(
        Guid djId,
        string? estado,
        string? expectedEstado,
        string? adminAccessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/admin/djs/{djId}/state")
        {
            Content = JsonContent.Create(new AdminDjStateRequest(estado, expectedEstado))
        };

        if (adminAccessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAccessToken);
        }

        return await Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetRoomsAsync(string djAccessToken)
    {
        using var client = CreateClientWithBearer(djAccessToken);
        return await client.GetAsync("/api/rooms");
    }

    private async Task<HttpResponseMessage> GetGuestRoomAsync(Guid roomId, string guestToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/guest/rooms/{roomId}");
        request.Headers.Add("X-Guest-Token", guestToken);
        return await Client.SendAsync(request);
    }

    private async Task<DjAccount> ReadDjAsync(Guid djId)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.Users.AsNoTracking().SingleAsync(d => d.Id == djId);
    }

    private async Task<Sala> ReadRoomAsync(Guid roomId)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.Salas.AsNoTracking().SingleAsync(s => s.Id == roomId);
    }

    private async Task<List<SesionInvitado>> ReadSessionsAsync(Guid djId)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.SesionesInvitado
            .AsNoTracking()
            .Where(s => context.Salas.Any(sala => sala.Id == s.SalaId && sala.DjId == djId))
            .ToListAsync();
    }

    private async Task<int> CountUnrevokedSessionsAsync(Guid djId)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.SesionesInvitado
            .AsNoTracking()
            .CountAsync(s => s.RevokedAt == null
                && context.Salas.Any(sala => sala.Id == s.SalaId && sala.DjId == djId));
    }

    private async Task<List<AuditoriaAdmin>> ReadAuditsAsync()
    {
        await using var context = await _factory.CreateAdminDbContextAsync();
        return await context.AdminAudits
            .AsNoTracking()
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToListAsync();
    }

    private async Task CreateAuditFailureTriggerAsync()
    {
        await using var context = await _factory.CreateAdminDbContextAsync();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE OR REPLACE FUNCTION fail_admin_audit() RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'audit failure';
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER fail_admin_audit_trigger
            BEFORE INSERT ON auditoria_admin
            FOR EACH ROW EXECUTE FUNCTION fail_admin_audit();
            """);
    }

    private static void AssertNoStore(HttpResponseMessage response) =>
        Assert.True(response.Headers.CacheControl?.NoStore, "Falta Cache-Control: no-store.");

    [Fact]
    public async Task SetState_WithoutValidAdmin_ReturnsUnauthorizedOrForbiddenWithNoStore()
    {
        await CreateAdminAsync();
        var adminId = await GetAdminIdAsync();
        var djId = await CreateDjDirectAsync("permisos-dj@test.com", "DJ Permisos");
        var djTokens = await LoginDjAsync("permisos-dj@test.com");
        var room = await CreateRoomAsync(djTokens.AccessToken, FakeTime.GetUtcNow().AddHours(2));
        var (_, guestToken) = await CreateGuestSessionAsync(room.Codigo);
        Assert.NotNull(guestToken);

        var requests = new (string? Token, HttpStatusCode Expected)[]
        {
            (null, HttpStatusCode.Unauthorized),
            ("invalid.token.here", HttpStatusCode.Unauthorized),
            (djTokens.AccessToken, HttpStatusCode.Unauthorized),
            (guestToken, HttpStatusCode.Unauthorized)
        };

        foreach (var (token, expected) in requests)
        {
            using var response = await PatchStateAsync(djId, "suspendido", "activo", token);
            Assert.Equal(expected, response.StatusCode);
            AssertNoStore(response);
        }

        var adminToken = await LoginAdminAsync();
        await DeactivateAdminAsync();

        using var inactiveResponse = await PatchStateAsync(djId, "suspendido", "activo", adminToken);
        Assert.Equal(HttpStatusCode.Forbidden, inactiveResponse.StatusCode);
        AssertNoStore(inactiveResponse);
        Assert.Equal("activo", (await ReadDjAsync(djId)).Estado);
        Assert.Empty(await ReadAuditsAsync());
        Assert.NotEqual(Guid.Empty, adminId);
    }

    [Fact]
    public async Task SetState_InvalidOrMissingStates_Returns400WithNoStore()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();
        var djId = await CreateDjDirectAsync("validacion-dj@test.com", "DJ Validación");

        var cases = new (string? Estado, string? Expected)[]
        {
            (null, "activo"),
            ("suspendido", null),
            ("", "activo"),
            ("activo", ""),
            ("desconocido", "activo"),
            ("suspendido", "desconocido")
        };

        foreach (var (estado, expected) in cases)
        {
            using var response = await PatchStateAsync(djId, estado, expected, token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            AssertNoStore(response);
        }

        Assert.Equal("activo", (await ReadDjAsync(djId)).Estado);
        Assert.Empty(await ReadAuditsAsync());
    }

    [Fact]
    public async Task SetState_UnknownDj_Returns404WithNoStore()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();

        using var response = await PatchStateAsync(Guid.NewGuid(), "suspendido", "activo", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertNoStore(response);
        Assert.Empty(await ReadAuditsAsync());
    }

    [Fact]
    public async Task SetState_ExpectedEstadoMismatch_Returns409WithoutChanges()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();
        var djId = await CreateDjDirectAsync("conflicto-dj@test.com", "DJ Conflicto");
        var before = await ReadDjAsync(djId);

        using var response = await PatchStateAsync(djId, "suspendido", "suspendido", token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        AssertNoStore(response);

        var after = await ReadDjAsync(djId);
        Assert.Equal("activo", after.Estado);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.Empty(await ReadAuditsAsync());
    }

    [Fact]
    public async Task SetState_Suspend_ChangesStateStampRevokesSessionsAndAudits()
    {
        await CreateAdminAsync();
        var adminId = await GetAdminIdAsync();
        var token = await LoginAdminAsync();
        var djId = await CreateDjDirectAsync("suspender-dj@test.com", "DJ Suspender");
        var djTokens = await LoginDjAsync("suspender-dj@test.com");
        var room = await CreateRoomAsync(djTokens.AccessToken, FakeTime.GetUtcNow().AddHours(4));
        var (status, guestToken) = await CreateGuestSessionAsync(room.Codigo);
        Assert.Equal(HttpStatusCode.Created, status);
        Assert.NotNull(guestToken);

        var beforeDj = await ReadDjAsync(djId);
        var beforeRoom = await ReadRoomAsync(room.Id);

        using var response = await PatchStateAsync(djId, "suspendido", "activo", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("securityStamp", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);

        var body = await response.Content.ReadFromJsonAsync<AdminDjStateResponse>();
        Assert.NotNull(body);
        Assert.Equal(djId, body.Id);
        Assert.Equal("suspendido", body.Estado);

        var afterDj = await ReadDjAsync(djId);
        Assert.Equal("suspendido", afterDj.Estado);
        Assert.NotEqual(beforeDj.SecurityStamp, afterDj.SecurityStamp);
        Assert.Equal(FakeTime.GetUtcNow(), afterDj.UpdatedAt);

        var afterRoom = await ReadRoomAsync(room.Id);
        Assert.Equal(beforeRoom.Estado, afterRoom.Estado);
        Assert.Equal(beforeRoom.PurgeAt, afterRoom.PurgeAt);

        var sessions = await ReadSessionsAsync(djId);
        Assert.Single(sessions);
        Assert.All(sessions, s => Assert.Equal(FakeTime.GetUtcNow(), s.RevokedAt));
        Assert.Equal(0, await CountUnrevokedSessionsAsync(djId));

        var audit = Assert.Single(await ReadAuditsAsync());
        Assert.Equal(adminId, audit.AdministratorId);
        Assert.Equal("DJ_Suspendido", audit.Action);
        Assert.Equal("DJ", audit.ResourceType);
        Assert.Equal(djId, audit.ResourceId);
        Assert.Equal("Exitoso", audit.Result);
        Assert.Equal(FakeTime.GetUtcNow(), audit.CreatedAt);
    }

    [Fact]
    public async Task SuspendedDj_CannotLoginRefreshOrUseBearerAndGuestSessionsAreBlocked()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();
        var djId = await CreateDjDirectAsync("bloqueo-dj@test.com", "DJ Bloqueo");
        var djTokens = await LoginDjAsync("bloqueo-dj@test.com");
        var room = await CreateRoomAsync(djTokens.AccessToken, FakeTime.GetUtcNow().AddHours(4));
        var (_, guestToken) = await CreateGuestSessionAsync(room.Codigo);
        Assert.NotNull(guestToken);

        using (var patch = await PatchStateAsync(djId, "suspendido", "activo", token))
        {
            Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        }

        using var login = await LoginDjRawAsync("bloqueo-dj@test.com");
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        using var refresh = await RefreshAsync(djTokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        using var protectedRequest = await GetRoomsAsync(djTokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, protectedRequest.StatusCode);

        using var guestRequest = await GetGuestRoomAsync(room.Id, guestToken);
        Assert.Equal(HttpStatusCode.Unauthorized, guestRequest.StatusCode);

        var (createStatus, _) = await CreateGuestSessionAsync(room.Codigo);
        Assert.Equal(HttpStatusCode.NotFound, createStatus);
    }

    [Fact]
    public async Task Reactivate_RequiresNewLoginKeepsOldCredentialsInvalidAndAllowsEligibleRoomsOnly()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();
        var djId = await CreateDjDirectAsync("reactivar-dj@test.com", "DJ Reactivar");
        var oldTokens = await LoginDjAsync("reactivar-dj@test.com");
        var now = FakeTime.GetUtcNow();
        var openRoom = await CreateRoomAsync(oldTokens.AccessToken, now.AddHours(4));
        var endedRoom = await CreateRoomAsync(oldTokens.AccessToken, now.AddMinutes(2));
        var (_, oldGuestToken) = await CreateGuestSessionAsync(openRoom.Codigo);
        Assert.NotNull(oldGuestToken);

        using (var suspend = await PatchStateAsync(djId, "suspendido", "activo", token))
        {
            Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        }

        using (var reactivate = await PatchStateAsync(djId, "activo", "suspendido", token))
        {
            Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        }

        using (var oldBearer = await GetRoomsAsync(oldTokens.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, oldBearer.StatusCode);
        }

        using (var oldRefresh = await RefreshAsync(oldTokens.RefreshToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, oldRefresh.StatusCode);
        }

        using (var oldGuest = await GetGuestRoomAsync(openRoom.Id, oldGuestToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, oldGuest.StatusCode);
        }

        var newTokens = await LoginDjAsync("reactivar-dj@test.com");
        using (var newBearer = await GetRoomsAsync(newTokens.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, newBearer.StatusCode);
        }

        FakeTime.Advance(TimeSpan.FromMinutes(3));

        var (openStatus, _) = await CreateGuestSessionAsync(openRoom.Codigo);
        Assert.Equal(HttpStatusCode.Created, openStatus);

        var (endedStatus, _) = await CreateGuestSessionAsync(endedRoom.Codigo);
        Assert.Equal(HttpStatusCode.NotFound, endedStatus);

        var audits = await ReadAuditsAsync();
        Assert.Equal(2, audits.Count);
        Assert.Contains(audits, a => a.Action == "DJ_Suspendido");
        Assert.Contains(audits, a => a.Action == "DJ_Reactivado");
    }

    [Fact]
    public async Task SetState_SameEstado_IsNoOpWithoutAuditDuplicate()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();
        var djId = await CreateDjDirectAsync("sin-cambio-dj@test.com", "DJ Sin Cambio");
        var before = await ReadDjAsync(djId);

        using var response = await PatchStateAsync(djId, "activo", "activo", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoStore(response);

        var body = await response.Content.ReadFromJsonAsync<AdminDjStateResponse>();
        Assert.NotNull(body);
        Assert.Equal(djId, body.Id);
        Assert.Equal("activo", body.Estado);

        var after = await ReadDjAsync(djId);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Empty(await ReadAuditsAsync());
    }

    [Fact]
    public async Task SetState_TwoConcurrentChangesFromSameExpected_Produce200And409()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();
        var djId = await CreateDjDirectAsync("concurrente-dj@test.com", "DJ Concurrente");

        var responses = await Task.WhenAll(
            PatchStateAsync(djId, "suspendido", "activo", token),
            PatchStateAsync(djId, "suspendido", "activo", token));

        try
        {
            var statuses = responses.Select(r => r.StatusCode).OrderBy(s => s).ToArray();
            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, statuses);
            Assert.All(responses, AssertNoStore);
            Assert.Equal("suspendido", (await ReadDjAsync(djId)).Estado);
            Assert.Single(await ReadAuditsAsync());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task SetState_RaceWithGuestSessionCreation_LeavesNoValidSession()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();
        var djId = await CreateDjDirectAsync("carrera-dj@test.com", "DJ Carrera");
        var djTokens = await LoginDjAsync("carrera-dj@test.com");
        var room = await CreateRoomAsync(djTokens.AccessToken, FakeTime.GetUtcNow().AddHours(4));

        var patchTask = PatchStateAsync(djId, "suspendido", "activo", token);
        var sessionTask = CreateGuestSessionAsync(room.Codigo);

        using var patch = await patchTask;
        var (sessionStatus, guestToken) = await sessionTask;

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.True(sessionStatus is HttpStatusCode.Created or HttpStatusCode.NotFound);

        Assert.Equal(0, await CountUnrevokedSessionsAsync(djId));

        if (guestToken is not null)
        {
            using var guestRequest = await GetGuestRoomAsync(room.Id, guestToken);
            Assert.Equal(HttpStatusCode.Unauthorized, guestRequest.StatusCode);
        }
    }

    [Fact]
    public async Task SetState_AuditFailure_RollsBackStateStampAndRevocations()
    {
        await CreateAdminAsync();
        var token = await LoginAdminAsync();
        var djId = await CreateDjDirectAsync("rollback-dj@test.com", "DJ Rollback");
        var djTokens = await LoginDjAsync("rollback-dj@test.com");
        var room = await CreateRoomAsync(djTokens.AccessToken, FakeTime.GetUtcNow().AddHours(4));
        await CreateGuestSessionAsync(room.Codigo);

        var before = await ReadDjAsync(djId);
        Assert.Equal(1, await CountUnrevokedSessionsAsync(djId));
        await CreateAuditFailureTriggerAsync();

        using var response = await PatchStateAsync(djId, "suspendido", "activo", token);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertNoStore(response);

        var after = await ReadDjAsync(djId);
        Assert.Equal("activo", after.Estado);
        Assert.Equal(before.SecurityStamp, after.SecurityStamp);
        Assert.Equal(1, await CountUnrevokedSessionsAsync(djId));
        Assert.Empty(await ReadAuditsAsync());
    }
}
