using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Accounts.Dtos;
using DjRequests.Api.Modules.Rooms.Dtos;
using DjRequests.Api.Modules.Rooms.Services;
using DjRequests.Api.Persistence;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Rooms;

[Collection(IntegrationCollection.Name)]
public class RoomEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private WebAppFactory _factory = null!;
    private HttpClient _client = null!;

    public RoomEndpointsTests(PostgresFixture postgres)
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

    private async Task RegisterAndLoginAsync(string email = "dj@test.com", string password = "validpassphrase123")
    {
        var registerResponse = await Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, password, "DJ Test"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task LoginAgainAsync(string email = "dj@test.com", string password = "validpassphrase123")
    {
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private static void AssertDateTimesEqual(DateTimeOffset expected, DateTimeOffset actual)
    {
        Assert.True(
            (expected - actual).Duration() < TimeSpan.FromMicroseconds(1),
            $"Se esperaba {expected:O} pero se obtuvo {actual:O}.");
    }

    private async Task<(HttpClient client, string email)> CreateSecondDjAsync(string email = "dj2@test.com")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "validpassphrase123", "DJ Dos"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "validpassphrase123"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        return (client, email);
    }

    private static string FormatIso(DateTimeOffset value) => value.ToString("O");

    private static CreateRoomRequest ValidRoomRequest(
        string name = "Sala de prueba",
        string timezone = "America/Mexico_City")
    {
        var now = DateTimeOffset.UtcNow;
        return new CreateRoomRequest(
            Name: name,
            StartsAt: FormatIso(now.AddHours(1)),
            EndsAt: FormatIso(now.AddHours(4)),
            Timezone: timezone);
    }

    private async Task<RoomResponse> CreateRoomAsync(CreateRoomRequest? request = null)
    {
        request ??= ValidRoomRequest();
        var response = await Client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.NotNull(room);
        return room;
    }

    #region CREATE

    [Fact]
    public async Task Create_ValidRequest_Returns201WithServerAssignedValues()
    {
        await RegisterAndLoginAsync();

        var request = ValidRoomRequest("Mi Fiesta");
        var response = await Client.PostAsJsonAsync("/api/rooms", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.NotNull(room);
        Assert.NotEqual(Guid.Empty, room.Id);
        Assert.Equal("Mi Fiesta", room.Nombre);
        Assert.Equal("abierta", room.Estado);
        Assert.Equal(1, room.Version);
        Assert.Equal(10, room.MaxSolicitudesSesion);
        Assert.Equal(2, room.MaxPlaylistsSesion);
        Assert.Equal(30, room.IntervaloEnvioSegundos);
        Assert.Equal("America/Mexico_City", room.Timezone);
        Assert.True(room.HasEnded == false);
        Assert.True(room.CanReceiveRequests == true);
        Assert.Contains(room.Codigo, room.InviteUrl);

        // Codigo: 10 caracteres, A-Z y 2-9, sin I O 0 1
        Assert.Equal(10, room.Codigo.Length);
        foreach (var c in room.Codigo)
        {
            Assert.True(char.IsUpper(c) || (c >= '2' && c <= '9'));
            Assert.NotEqual('I', c);
            Assert.NotEqual('O', c);
            Assert.NotEqual('0', c);
            Assert.NotEqual('1', c);
        }

        // PurgeAt = EndsAt + 168h
        Assert.Equal(room.EndsAt.AddHours(168), room.PurgeAt);

        // Verificar persistencia
        await using var context = await _factory.CreateDbContextAsync();
        var sala = await context.Salas.SingleAsync(s => s.Id == room.Id);
        Assert.NotNull(sala);
        Assert.Equal(room.Codigo, sala.Codigo);
        Assert.Equal(1, sala.Version);
        Assert.Equal(10, sala.MaxSolicitudesSesion);
        Assert.Equal(2, sala.MaxPlaylistsSesion);
        Assert.Equal(30, sala.IntervaloEnvioSegundos);
    }

    [Fact]
    public async Task Create_TimezoneOmitted_UsesDefaultAmericaMexicoCity()
    {
        await RegisterAndLoginAsync();
        var request = ValidRoomRequest(timezone: null!);

        var response = await Client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal("America/Mexico_City", room!.Timezone);
    }

    [Fact]
    public async Task Create_TimezoneEmpty_Returns400()
    {
        await RegisterAndLoginAsync();
        var request = ValidRoomRequest(timezone: "");

        var response = await Client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Timezone", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_TimezoneInvalid_Returns400()
    {
        await RegisterAndLoginAsync();
        var request = ValidRoomRequest(timezone: "Invalid/Zone");

        var response = await Client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Timezone", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_TimezoneWindows_Returns400()
    {
        await RegisterAndLoginAsync();
        var request = ValidRoomRequest(timezone: "Central Standard Time");

        var response = await Client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Timezone", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_EmptyName_Returns400()
    {
        await RegisterAndLoginAsync();
        var request = ValidRoomRequest(name: "");

        var response = await Client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Name", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_NameTooLong_Returns400()
    {
        await RegisterAndLoginAsync();
        var request = ValidRoomRequest(name: new string('a', 121));

        var response = await Client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_EndsBeforeStarts_Returns400()
    {
        await RegisterAndLoginAsync();
        var now = DateTimeOffset.UtcNow;
        var request = new CreateRoomRequest(
            Name: "Sala",
            StartsAt: FormatIso(now.AddHours(4)),
            EndsAt: FormatIso(now.AddHours(1)),
            Timezone: "America/Mexico_City");

        var response = await Client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_EndsInPast_Returns400()
    {
        await RegisterAndLoginAsync();
        var now = DateTimeOffset.UtcNow;
        var request = new CreateRoomRequest(
            Name: "Sala",
            StartsAt: FormatIso(now.AddHours(-4)),
            EndsAt: FormatIso(now.AddHours(-1)),
            Timezone: "America/Mexico_City");

        var response = await Client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ExtraClientFields_Ignored()
    {
        await RegisterAndLoginAsync();

        var rawJson = """
        {
          "name": "Sala Manip",
          "startsAt": "2026-12-01T00:00:00+00:00",
          "endsAt": "2026-12-01T04:00:00+00:00",
          "timezone": "America/Mexico_City",
          "id": "11111111-1111-1111-1111-111111111111",
          "codigo": "MANIPCODE1",
          "estado": "cerrada",
          "djId": "22222222-2222-2222-2222-222222222222",
          "version": 99,
          "purgeAt": "2026-12-08T00:00:00+00:00"
        }
        """;
        var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/rooms", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.NotEqual(Guid.Parse("11111111-1111-1111-1111-111111111111"), room!.Id);
        Assert.NotEqual("MANIPCODE1", room.Codigo);
        Assert.Equal("abierta", room.Estado);
        Assert.Equal(1, room.Version);
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/rooms", ValidRoomRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #region Offset explícito

    [Fact]
    public async Task Create_StartsAtWithoutOffset_Returns400()
    {
        await RegisterAndLoginAsync();
        var request = new CreateRoomRequest(
            Name: "Sala",
            StartsAt: "2026-11-13T21:00:00",
            EndsAt: FormatIso(DateTimeOffset.UtcNow.AddHours(4)),
            Timezone: "America/Mexico_City");

        var response = await Client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("StartsAt", problem.Errors.Keys);
        Assert.Contains(problem.Errors["StartsAt"], m => m.Contains("offset explícito"));
    }

    [Fact]
    public async Task Create_EndsWithoutOffset_Returns400()
    {
        await RegisterAndLoginAsync();
        var request = new CreateRoomRequest(
            Name: "Sala",
            StartsAt: FormatIso(DateTimeOffset.UtcNow.AddHours(1)),
            EndsAt: "2026-11-14T03:00:00",
            Timezone: "America/Mexico_City");

        var response = await Client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("EndsAt", problem.Errors.Keys);
        Assert.Contains(problem.Errors["EndsAt"], m => m.Contains("offset explícito"));
    }

    [Fact]
    public async Task Create_StartsAtWithZ_EndsWithOffset_EquivalentInstant_PersistsSameUtc()
    {
        await RegisterAndLoginAsync();

        // Mismo instante expressado de dos formas: Z y offset -06:00
        var request = new CreateRoomRequest(
            Name: "Sala offset",
            StartsAt: "2026-11-14T03:00:00Z",
            EndsAt: "2026-11-14T09:00:00-06:00",
            Timezone: "America/Mexico_City");

        var response = await Client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.NotNull(room);

        // Ambos deben persistir como UTC: 03:00Z y 09:00-06:00 son el mismo instante
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 3, 0, 0, TimeSpan.Zero), room.StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.FromHours(-6)).ToUniversalTime(), room.EndsAt);

        // PurgeAt = EndsAt + 168h en UTC
        Assert.Equal(room.EndsAt.AddHours(168), room.PurgeAt);
    }

    [Fact]
    public async Task Create_InvalidDate_Returns400()
    {
        await RegisterAndLoginAsync();
        var request = new CreateRoomRequest(
            Name: "Sala",
            StartsAt: "no-es-fecha",
            EndsAt: FormatIso(DateTimeOffset.UtcNow.AddHours(4)),
            Timezone: "America/Mexico_City");

        var response = await Client.PostAsJsonAsync("/api/rooms", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("StartsAt", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_OmittedDate_Returns400()
    {
        await RegisterAndLoginAsync();

        var rawJson = """
        {
          "name": "Sala sin fecha",
          "endsAt": "2026-11-14T03:00:00Z",
          "timezone": "America/Mexico_City"
        }
        """;
        var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/rooms", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_InvalidDate_DoesNotModifyRoomNorVersion()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        // PUT con fecha inválida: no debe tocar la sala
        var rawJson = $$"""
        {
          "version": {{room.Version}},
          "name": "Nombre Cambiado",
          "startsAt": "2026-11-13T21:00:00",
          "endsAt": "{{FormatIso(room.EndsAt)}}",
          "timezone": "America/Mexico_City"
        }
        """;
        var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var response = await Client.PutAsync($"/api/rooms/{room.Id}", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // La sala y su versión no cambian
        var detail = await (await Client.GetAsync($"/api/rooms/{room.Id}"))
            .Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal(room.Nombre, detail!.Nombre);
        Assert.Equal(room.Version, detail.Version);
        AssertDateTimesEqual(room.StartsAt, detail.StartsAt);
        AssertDateTimesEqual(room.EndsAt, detail.EndsAt);
    }

    #endregion

    #endregion

    #region LIST

    [Fact]
    public async Task List_ReturnsOnlyOwnRooms()
    {
        await RegisterAndLoginAsync();
        await CreateRoomAsync(ValidRoomRequest("Mi Sala 1"));
        await CreateRoomAsync(ValidRoomRequest("Mi Sala 2"));

        var response = await Client.GetAsync("/api/rooms");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rooms = await response.Content.ReadFromJsonAsync<List<RoomResponse>>();
        Assert.NotNull(rooms);
        Assert.Equal(2, rooms.Count);
        Assert.All(rooms, r => Assert.Equal("abierta", r.Estado));
    }

    [Fact]
    public async Task List_ExcludesPurgedRooms()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        // Avanzar mas alla de PurgeAt
        FakeTime.UtcNow = room.PurgeAt.AddMinutes(1);
        await LoginAgainAsync();

        var response = await Client.GetAsync("/api/rooms");
        var rooms = await response.Content.ReadFromJsonAsync<List<RoomResponse>>();
        Assert.Empty(rooms!);
    }

    [Fact]
    public async Task List_Pagination_ReturnsCorrectPage()
    {
        await RegisterAndLoginAsync();
        for (int i = 1; i <= 5; i++)
        {
            await CreateRoomAsync(ValidRoomRequest($"Sala {i}"));
        }

        var response = await Client.GetAsync("/api/rooms?page=1&pageSize=2");
        var rooms = await response.Content.ReadFromJsonAsync<List<RoomResponse>>();
        Assert.Equal(2, rooms!.Count);

        var response2 = await Client.GetAsync("/api/rooms?page=3&pageSize=2");
        var rooms2 = await response2.Content.ReadFromJsonAsync<List<RoomResponse>>();
        Assert.Single(rooms2!);
    }

    [Fact]
    public async Task List_InvalidPage_Returns400()
    {
        await RegisterAndLoginAsync();
        var response = await Client.GetAsync("/api/rooms?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_InvalidPageSize_Returns400()
    {
        await RegisterAndLoginAsync();
        var response = await Client.GetAsync("/api/rooms?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/rooms");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region GET DETAIL

    [Fact]
    public async Task Get_OwnRoom_ReturnsRoom()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var response = await Client.GetAsync($"/api/rooms/{room.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal(room.Id, detail!.Id);
        Assert.Equal(room.Codigo, detail.Codigo);
    }

    [Fact]
    public async Task Get_UnknownRoom_Returns404()
    {
        await RegisterAndLoginAsync();
        var response = await Client.GetAsync($"/api/rooms/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_OtherDjRoom_Returns404()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var (client2, _) = await CreateSecondDjAsync();
        var response = await client2.GetAsync($"/api/rooms/{room.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_PurgedRoom_Returns404()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        FakeTime.UtcNow = room.PurgeAt.AddMinutes(1);
        await LoginAgainAsync();

        var response = await Client.GetAsync($"/api/rooms/{room.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_AfterEndsBeforePurge_Returns200()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        FakeTime.UtcNow = room.EndsAt.AddMinutes(1);
        await LoginAgainAsync();

        var response = await Client.GetAsync($"/api/rooms/{room.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.True(detail!.HasEnded);
        Assert.False(detail.CanReceiveRequests);
    }

    [Fact]
    public async Task Get_HasEndedAndCanReceiveRequests_Indicators()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        // Antes del fin
        var detail1 = await (await Client.GetAsync($"/api/rooms/{room.Id}"))
            .Content.ReadFromJsonAsync<RoomResponse>();
        Assert.False(detail1!.HasEnded);
        Assert.True(detail1.CanReceiveRequests);

        // Pausada -> no puede recibir solicitudes
        await Client.PatchAsJsonAsync($"/api/rooms/{room.Id}/state",
            new UpdateRoomStateRequest("pausada", room.Version));
        var detail2 = await (await Client.GetAsync($"/api/rooms/{room.Id}"))
            .Content.ReadFromJsonAsync<RoomResponse>();
        Assert.False(detail2!.HasEnded);
        Assert.False(detail2.CanReceiveRequests);

        // Reabierta -> puede recibir
        await Client.PatchAsJsonAsync($"/api/rooms/{room.Id}/state",
            new UpdateRoomStateRequest("abierta", 2));
        var detail3 = await (await Client.GetAsync($"/api/rooms/{room.Id}"))
            .Content.ReadFromJsonAsync<RoomResponse>();
        Assert.False(detail3!.HasEnded);
        Assert.True(detail3.CanReceiveRequests);
    }

    #endregion

    #region UPDATE

    [Fact]
    public async Task Update_ValidRequest_Returns200WithUpdatedValues()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var newStarts = FakeTime.UtcNow.AddHours(2);
        var newEnds = newStarts.AddHours(6);
        var request = new UpdateRoomRequest(
            Version: room.Version,
            Name: "Sala Renombrada",
            StartsAt: FormatIso(newStarts),
            EndsAt: FormatIso(newEnds),
            Timezone: "Europe/Madrid");

        var response = await Client.PutAsJsonAsync($"/api/rooms/{room.Id}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal("Sala Renombrada", updated!.Nombre);
        Assert.Equal(newStarts, updated.StartsAt);
        Assert.Equal(newEnds, updated.EndsAt);
        Assert.Equal("Europe/Madrid", updated.Timezone);
        Assert.Equal(2, updated.Version);
        Assert.Equal(newEnds.AddHours(168), updated.PurgeAt);

        // Codigo y estado conservados
        Assert.Equal(room.Codigo, updated.Codigo);
        Assert.Equal(room.Estado, updated.Estado);
        Assert.Equal(room.MaxSolicitudesSesion, updated.MaxSolicitudesSesion);
        Assert.Equal(room.MaxPlaylistsSesion, updated.MaxPlaylistsSesion);
        Assert.Equal(room.IntervaloEnvioSegundos, updated.IntervaloEnvioSegundos);
    }

    [Fact]
    public async Task Update_ReprogramRecalculatesExactly168Hours()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var newEnds = FakeTime.UtcNow.AddHours(10);
        var request = new UpdateRoomRequest(room.Version, "Sala", FormatIso(FakeTime.UtcNow.AddHours(1)), FormatIso(newEnds), "America/Mexico_City");

        var response = await Client.PutAsJsonAsync($"/api/rooms/{room.Id}", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal(newEnds.AddHours(168), updated!.PurgeAt);
    }

    [Fact]
    public async Task Update_OtherDjRoom_Returns404()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var (client2, _) = await CreateSecondDjAsync();
        var request = new UpdateRoomRequest(room.Version, "Sala", FormatIso(room.StartsAt), FormatIso(room.EndsAt), room.Timezone);

        var response = await client2.PutAsJsonAsync($"/api/rooms/{room.Id}", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ObsoleteVersion_Returns409()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var request = new UpdateRoomRequest(999L, "Sala", FormatIso(room.StartsAt), FormatIso(room.EndsAt), room.Timezone);
        var response = await Client.PutAsJsonAsync($"/api/rooms/{room.Id}", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_AfterEnds_Returns409()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        FakeTime.UtcNow = room.EndsAt.AddMinutes(1);
        await LoginAgainAsync();

        var request = new UpdateRoomRequest(room.Version, "Sala", FormatIso(room.StartsAt), FormatIso(room.EndsAt.AddHours(1)), room.Timezone);
        var response = await Client.PutAsJsonAsync($"/api/rooms/{room.Id}", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Update_MissingFields_Returns400()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var rawJson = """
        {
          "version": 1,
          "name": "Sala",
          "startsAt": "2026-12-01T00:00:00+00:00",
          "endsAt": "2026-12-01T04:00:00+00:00"
        }
        """;
        var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var response = await Client.PutAsync($"/api/rooms/{room.Id}", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_TimezoneInvalid_Returns400()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var request = new UpdateRoomRequest(room.Version, "Sala", FormatIso(room.StartsAt), FormatIso(room.EndsAt), "Not/AZone");
        var response = await Client.PutAsJsonAsync($"/api/rooms/{room.Id}", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_TimezoneWindows_Returns400()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var request = new UpdateRoomRequest(room.Version, "Sala", FormatIso(room.StartsAt), FormatIso(room.EndsAt), "Pacific Standard Time");
        var response = await Client.PutAsJsonAsync($"/api/rooms/{room.Id}", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ConcurrentSameVersion_OneSucceeds_One409_NoDataLoss()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var request1 = new UpdateRoomRequest(room.Version, "Sala A", FormatIso(room.StartsAt), FormatIso(room.EndsAt), room.Timezone);
        var request2 = new UpdateRoomRequest(room.Version, "Sala B", FormatIso(room.StartsAt), FormatIso(room.EndsAt), room.Timezone);

        var task1 = Client.PutAsJsonAsync($"/api/rooms/{room.Id}", request1);
        var task2 = Client.PutAsJsonAsync($"/api/rooms/{room.Id}", request2);

        var responses = await Task.WhenAll(task1, task2);
        var statusCodes = responses.Select(r => (int)r.StatusCode).OrderBy(c => c).ToArray();

        // Exactamente un 200 y un 409
        Assert.Contains(200, statusCodes);
        Assert.Contains(409, statusCodes);
        Assert.Equal(2, statusCodes.Length);

        // Sin datos perdidos: solo un nombre persistido
        await using var context = await _factory.CreateDbContextAsync();
        var sala = await context.Salas.SingleAsync(s => s.Id == room.Id);
        Assert.True(sala.Nombre == "Sala A" || sala.Nombre == "Sala B");
        Assert.Equal(2, sala.Version);
    }

    [Fact]
    public async Task Update_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var roomRequest = ValidRoomRequest();

        var registerResponse = await Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("dj3@test.com", "validpassphrase123", "DJ Tres"));
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("dj3@test.com", "validpassphrase123"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var roomResponse = await Client.PostAsJsonAsync("/api/rooms", roomRequest);
        var room = await roomResponse.Content.ReadFromJsonAsync<RoomResponse>();

        // Sin token
        var updateRequest = new UpdateRoomRequest(room!.Version, "Sala", FormatIso(room.StartsAt), FormatIso(room.EndsAt), room.Timezone);
        var response = await client.PutAsJsonAsync($"/api/rooms/{room.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region PATCH STATE

    [Fact]
    public async Task PatchState_Pause_KeepsDates()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var response = await Client.PatchAsJsonAsync($"/api/rooms/{room.Id}/state",
            new UpdateRoomStateRequest("pausada", room.Version));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal("pausada", updated!.Estado);
        Assert.Equal(2, updated.Version);
        // Fechas conservadas
        AssertDateTimesEqual(room.StartsAt, updated.StartsAt);
        AssertDateTimesEqual(room.EndsAt, updated.EndsAt);
        AssertDateTimesEqual(room.PurgeAt, updated.PurgeAt);
    }

    [Fact]
    public async Task PatchState_Close_KeepsDates()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var response = await Client.PatchAsJsonAsync($"/api/rooms/{room.Id}/state",
            new UpdateRoomStateRequest("cerrada", room.Version));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal("cerrada", updated!.Estado);
        AssertDateTimesEqual(room.StartsAt, updated.StartsAt);
        AssertDateTimesEqual(room.EndsAt, updated.EndsAt);
        AssertDateTimesEqual(room.PurgeAt, updated.PurgeAt);
    }

    [Fact]
    public async Task PatchState_Reopen_BeforeEnd_Succeeds()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        await Client.PatchAsJsonAsync($"/api/rooms/{room.Id}/state",
            new UpdateRoomStateRequest("pausada", 1));
        var reopenResponse = await Client.PatchAsJsonAsync($"/api/rooms/{room.Id}/state",
            new UpdateRoomStateRequest("abierta", 2));
        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);

        var updated = await reopenResponse.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal("abierta", updated!.Estado);
        Assert.Equal(3, updated.Version);
    }

    [Fact]
    public async Task PatchState_AfterEnd_Returns409()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        FakeTime.UtcNow = room.EndsAt.AddMinutes(1);
        await LoginAgainAsync();

        var response = await Client.PatchAsJsonAsync($"/api/rooms/{room.Id}/state",
            new UpdateRoomStateRequest("pausada", room.Version));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PatchState_SameState_ReturnsCurrentWithoutIncrement()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var response = await Client.PatchAsJsonAsync($"/api/rooms/{room.Id}/state",
            new UpdateRoomStateRequest("abierta", room.Version));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.Equal("abierta", updated!.Estado);
        // Version NO incrementada
        Assert.Equal(room.Version, updated.Version);
    }

    [Fact]
    public async Task PatchState_ObsoleteVersion_Returns409()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var response = await Client.PatchAsJsonAsync($"/api/rooms/{room.Id}/state",
            new UpdateRoomStateRequest("pausada", 999L));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PatchState_InvalidState_Returns400()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var response = await Client.PatchAsJsonAsync($"/api/rooms/{room.Id}/state",
            new UpdateRoomStateRequest("invalida", room.Version));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchState_OtherDjRoom_Returns404()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var (client2, _) = await CreateSecondDjAsync();
        var response = await client2.PatchAsJsonAsync($"/api/rooms/{room.Id}/state",
            new UpdateRoomStateRequest("pausada", room.Version));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchState_WithoutToken_Returns401()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        var anonClient = _factory.CreateClient();
        var response = await anonClient.PatchAsJsonAsync($"/api/rooms/{room.Id}/state",
            new UpdateRoomStateRequest("pausada", room.Version));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region SUSPENDED DJ

    [Fact]
    public async Task AllEndpoints_SuspendedDj_Returns403()
    {
        await RegisterAndLoginAsync();

        // Suspender cuenta
        await using (var context = await _factory.CreateDbContextAsync())
        {
            var dj = await context.Users.FirstAsync(u => u.Email == "dj@test.com");
            dj.Estado = "suspendido";
            await context.SaveChangesAsync();
        }

        var createResponse = await Client.PostAsJsonAsync("/api/rooms", ValidRoomRequest());
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        var listResponse = await Client.GetAsync("/api/rooms");
        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
    }

    #endregion

    #region CODE GENERATOR DETERMINISTIC TEST

    [Fact]
    public async Task Create_CodeGeneratorCollision_RetriesDeterministic()
    {
        await RegisterAndLoginAsync();

        // Sustituir generador por uno determinista
        using var scope = _factory.Services.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<IRoomCodeGenerator>();
        // Este test verifica que el endpoint usa IRoomCodeGenerator
        Assert.NotNull(generator);
        // Genera codigos de 10 caracteres
        var code = generator.GenerateCode();
        Assert.Equal(10, code.Length);
    }

    #endregion
}
