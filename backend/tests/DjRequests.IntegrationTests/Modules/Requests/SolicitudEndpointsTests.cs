using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Accounts.Dtos;
using DjRequests.Api.Modules.Guests.Dtos;
using DjRequests.Api.Modules.Requests.Dtos;
using DjRequests.Api.Modules.Rooms.Dtos;
using DjRequests.Api.Persistence;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Requests;

[Collection(IntegrationCollection.Name)]
public class SolicitudEndpointsTests : IAsyncLifetime
{
    private const string Password = "validpassphrase123";
    private readonly PostgresFixture _postgres;
    private WebAppFactory _factory = null!;
    private HttpClient _client = null!;

    public SolicitudEndpointsTests(PostgresFixture postgres)
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

    private async Task<string> CreateGuestSessionAsync(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync("/api/guest-sessions", new CreateGuestSessionRequest(code));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<GuestSessionResponse>();
        Assert.NotNull(session);
        return session.GuestToken;
    }

    private static HttpRequestMessage GuestPost(Guid roomId, string? token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/guest/rooms/{roomId}/requests")
        {
            Content = JsonContent.Create(body)
        };
        if (token is not null)
        {
            request.Headers.Add("X-Guest-Token", token);
        }
        return request;
    }

    private static HttpRequestMessage GuestGetList(Guid roomId, string? token, int page = 1, int pageSize = 20)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/guest/rooms/{roomId}/requests?page={page}&pageSize={pageSize}");
        if (token is not null)
        {
            request.Headers.Add("X-Guest-Token", token);
        }
        return request;
    }

    private static HttpRequestMessage DjGetList(Guid roomId, string? estado = null, int page = 1, int pageSize = 20)
    {
        var qs = $"?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(estado)) qs += $"&estado={estado}";
        return new HttpRequestMessage(HttpMethod.Get, $"/api/rooms/{roomId}/requests{qs}");
    }

    private static HttpRequestMessage DjPatch(Guid roomId, Guid solicitudId, object body)
    {
        return new HttpRequestMessage(HttpMethod.Patch, $"/api/rooms/{roomId}/requests/{solicitudId}")
        {
            Content = JsonContent.Create(body)
        };
    }

    private static CreateSolicitudRequest MakeSolicitud(
        string nombre = "Canción Test",
        string? artista = "Artista Test",
        short? generoId = null,
        string? enlace = null,
        Guid? idempotencyKey = null)
    {
        return new CreateSolicitudRequest(nombre, artista, generoId, enlace, idempotencyKey ?? Guid.NewGuid());
    }

    #endregion

    #region POST - Envío válido

    [Fact]
    public async Task Create_ValidRequest_Returns201WithSolicitud()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var body = MakeSolicitud("Bailando", "Enrique Iglesias", 3, "https://example.com/song");
        var response = await Client.SendAsync(GuestPost(roomId, token, body));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var solicitud = await response.Content.ReadFromJsonAsync<SolicitudResponse>();
        Assert.NotNull(solicitud);
        Assert.Equal("Bailando", solicitud.Nombre);
        Assert.Equal("Enrique Iglesias", solicitud.Artista);
        Assert.Equal((short?)3, solicitud.GeneroId);
        Assert.Equal("https://example.com/song", solicitud.Enlace);
        Assert.Equal("pendiente", solicitud.Estado);
        Assert.Equal(1, solicitud.Version);
    }

    [Fact]
    public async Task Create_WithoutOptionalFields_Returns201()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var body = MakeSolicitud("Solo nombre", null, null, null);
        var response = await Client.SendAsync(GuestPost(roomId, token, body));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var solicitud = await response.Content.ReadFromJsonAsync<SolicitudResponse>();
        Assert.NotNull(solicitud);
        Assert.Null(solicitud.Artista);
        Assert.Null(solicitud.Enlace);
    }

    [Fact]
    public async Task Create_PersistsInDatabase()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var body = MakeSolicitud("Test Persistencia");
        var response = await Client.SendAsync(GuestPost(roomId, token, body));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();
        var stored = await db.Solicitudes.FirstOrDefaultAsync(s => s.Nombre == "Test Persistencia");
        Assert.NotNull(stored);
        Assert.Equal(roomId, stored.SalaId);
        Assert.Equal("pendiente", stored.Estado);
    }

    #endregion

    #region POST - Idempotencia

    [Fact]
    public async Task Create_IdempotentRetry_SamePayload_Returns200WithOriginal()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var idemKey = Guid.NewGuid();
        var body1 = MakeSolicitud("Canción Idem", "Artista", null, null, idemKey);
        var response1 = await Client.SendAsync(GuestPost(roomId, token, body1));
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);
        var solicitud1 = await response1.Content.ReadFromJsonAsync<SolicitudResponse>();

        var body2 = MakeSolicitud("Canción Idem", "Artista", null, null, idemKey);
        var response2 = await Client.SendAsync(GuestPost(roomId, token, body2));
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var solicitud2 = await response2.Content.ReadFromJsonAsync<SolicitudResponse>();

        Assert.NotNull(solicitud1);
        Assert.NotNull(solicitud2);
        Assert.Equal(solicitud1.Id, solicitud2.Id);
    }

    [Fact]
    public async Task Create_IdempotentRetry_DifferentPayload_Returns409()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var idemKey = Guid.NewGuid();
        var body1 = MakeSolicitud("Original", null, null, null, idemKey);
        await Client.SendAsync(GuestPost(roomId, token, body1));

        var body2 = MakeSolicitud("Diferente", null, null, null, idemKey);
        var response2 = await Client.SendAsync(GuestPost(roomId, token, body2));

        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    #endregion

    #region POST - Token inválido

    [Fact]
    public async Task Create_NoToken_Returns401()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();

        var body = MakeSolicitud();
        var response = await Client.SendAsync(GuestPost(roomId, null, body));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_MalformedToken_Returns401()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();

        var body = MakeSolicitud();
        var response = await Client.SendAsync(GuestPost(roomId, "invalid-token", body));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_TokenFromWrongRoom_Returns404()
    {
        var (roomId1, code1) = await CreateRoomAndGetCodeAsync();
        var token1 = await CreateGuestSessionAsync(Client, code1);

        // Crear otra sala
        var room2 = await CreateRoomAsync(Client, FakeTime.UtcNow, "Otra Sala");

        var body = MakeSolicitud();
        var response = await Client.SendAsync(GuestPost(room2.Id, token1, body));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region POST - Sala no disponible

    [Fact]
    public async Task Create_RoomPaused_Returns409()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        // Pausar sala
        var room = await Client.GetFromJsonAsync<RoomResponse>($"/api/rooms/{roomId}");
        var pauseResponse = await Client.PatchAsJsonAsync($"/api/rooms/{roomId}/state",
            new { state = "pausada", version = room!.Version });
        Assert.Equal(HttpStatusCode.OK, pauseResponse.StatusCode);

        var body = MakeSolicitud();
        var response = await Client.SendAsync(GuestPost(roomId, token, body));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_RoomEnded_Returns404()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        // Acortar la sala: la sesión sigue vigente hasta el fin original (+4h),
        // pero la sala termina en +2h.
        var room = await Client.GetFromJsonAsync<RoomResponse>($"/api/rooms/{roomId}");
        var updateResponse = await Client.PutAsJsonAsync($"/api/rooms/{roomId}", new UpdateRoomRequest(
            room!.Version, "Sala Test", FormatIso(FakeTime.UtcNow.AddHours(1)), FormatIso(FakeTime.UtcNow.AddHours(2)), null));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        FakeTime.Advance(TimeSpan.FromHours(3));

        var body = MakeSolicitud();
        var response = await Client.SendAsync(GuestPost(roomId, token, body));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region POST - Validaciones

    [Fact]
    public async Task Create_EmptyNombre_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var body = MakeSolicitud("", null, null, null);
        var response = await Client.SendAsync(GuestPost(roomId, token, body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidEnlace_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var body = MakeSolicitud("Test", null, null, "not-a-url");
        var response = await Client.SendAsync(GuestPost(roomId, token, body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidGeneroId_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var body = MakeSolicitud("Test", null, 9999, null);
        var response = await Client.SendAsync(GuestPost(roomId, token, body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region GET - Listar

    [Fact]
    public async Task DJ_List_ReturnsPaginatedSolicitudes()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        // Crear 3 solicitudes (respetando el intervalo de 30s entre envíos)
        for (int i = 0; i < 3; i++)
        {
            if (i > 0) FakeTime.Advance(TimeSpan.FromSeconds(31));
            var body = MakeSolicitud($"Canción {i}");
            var resp = await Client.SendAsync(GuestPost(roomId, token, body));
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        }

        var response = await Client.SendAsync(DjGetList(roomId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<SolicitudPageResponse>();
        Assert.NotNull(page);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.Items.Count);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task DJ_List_FiltersByEstado()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        // Crear 2 solicitudes
        var body1 = MakeSolicitud("Canción A");
        var r1 = await Client.SendAsync(GuestPost(roomId, token, body1));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        FakeTime.Advance(TimeSpan.FromSeconds(31));
        var body2 = MakeSolicitud("Canción B");
        var r2 = await Client.SendAsync(GuestPost(roomId, token, body2));
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);

        var s1 = await r1.Content.ReadFromJsonAsync<SolicitudResponse>();
        var s2 = await r2.Content.ReadFromJsonAsync<SolicitudResponse>();

        // Aceptar la primera
        var patch = await Client.SendAsync(DjPatch(roomId, s1!.Id, new { estado = "aceptada", version = s1.Version }));
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        // Filtrar pendientes
        var response = await Client.SendAsync(DjGetList(roomId, "pendiente"));
        var page = await response.Content.ReadFromJsonAsync<SolicitudPageResponse>();
        Assert.Single(page!.Items);
        Assert.Equal("Canción B", page.Items[0].Nombre);

        // Filtrar aceptadas
        var response2 = await Client.SendAsync(DjGetList(roomId, "aceptada"));
        var page2 = await response2.Content.ReadFromJsonAsync<SolicitudPageResponse>();
        Assert.Single(page2!.Items);
        Assert.Equal("Canción A", page2.Items[0].Nombre);
    }

    [Fact]
    public async Task DJ_List_OtherDjRoom_Returns404()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();

        // Registrar otro DJ
        var client2 = _factory.CreateClient();
        await RegisterAndLoginAsync(client2, "dj2@test.com");

        var response = await client2.SendAsync(DjGetList(roomId));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Guest_List_ReturnsAllSongsOfSameRoom_And404ForOtherRoom()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token1 = await CreateGuestSessionAsync(Client, code);
        var token2 = await CreateGuestSessionAsync(Client, code);

        // Dos invitados distintos de la misma sala envían una canción cada uno.
        var r1 = await Client.SendAsync(GuestPost(roomId, token1, MakeSolicitud("Invitado A")));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        FakeTime.Advance(TimeSpan.FromSeconds(31));
        var r2 = await Client.SendAsync(GuestPost(roomId, token2, MakeSolicitud("Invitado B")));
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);

        // Cualquier invitado de la sala ve todas las canciones de la sala.
        var response = await Client.SendAsync(GuestGetList(roomId, token1));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<SolicitudPageResponse>();
        Assert.NotNull(page);
        Assert.Equal(2, page.TotalCount);
        Assert.Contains(page.Items, s => s.Nombre == "Invitado A");
        Assert.Contains(page.Items, s => s.Nombre == "Invitado B");

        // El token de esta sala no puede consultar el contenido de otra sala.
        var room2 = await CreateRoomAsync(Client, FakeTime.UtcNow, "Otra Sala");
        var otherRoomResponse = await Client.SendAsync(GuestGetList(room2.Id, token1));
        Assert.Equal(HttpStatusCode.NotFound, otherRoomResponse.StatusCode);
        var body = await otherRoomResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Invitado A", body);
        Assert.DoesNotContain("Invitado B", body);
    }

    [Fact]
    public async Task Guest_List_ExtremelyLargePage_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestGetList(roomId, token, page: int.MaxValue, pageSize: 100));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DJ_List_ExtremelyLargePage_Returns400()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();

        var response = await Client.SendAsync(DjGetList(roomId, page: int.MaxValue, pageSize: 100));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region PATCH - Transiciones

    [Fact]
    public async Task DJ_Update_PendingToAccepted_Returns200()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var createResp = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud("Para aceptar")));
        var solicitud = await createResp.Content.ReadFromJsonAsync<SolicitudResponse>();

        var response = await Client.SendAsync(DjPatch(roomId, solicitud!.Id, new { estado = "aceptada", version = solicitud.Version }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<SolicitudResponse>();
        Assert.Equal("aceptada", updated!.Estado);
        Assert.Equal(solicitud.Version + 1, updated.Version);
    }

    [Fact]
    public async Task DJ_Update_PendingToRejected_Returns200()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var createResp = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud("Para rechazar")));
        var solicitud = await createResp.Content.ReadFromJsonAsync<SolicitudResponse>();

        var response = await Client.SendAsync(DjPatch(roomId, solicitud!.Id, new { estado = "rechazada", version = solicitud.Version }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<SolicitudResponse>();
        Assert.Equal("rechazada", updated!.Estado);
    }

    [Fact]
    public async Task DJ_Update_AcceptedToPlayed_Returns200()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var createResp = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud("Para reproducir")));
        var solicitud = await createResp.Content.ReadFromJsonAsync<SolicitudResponse>();

        // Aceptar
        var acceptResp = await Client.SendAsync(DjPatch(roomId, solicitud!.Id, new { estado = "aceptada", version = solicitud.Version }));
        var accepted = await acceptResp.Content.ReadFromJsonAsync<SolicitudResponse>();

        // Marcar reproducida
        var response = await Client.SendAsync(DjPatch(roomId, accepted!.Id, new { estado = "reproducida", version = accepted.Version }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<SolicitudResponse>();
        Assert.Equal("reproducida", updated!.Estado);
    }

    [Fact]
    public async Task DJ_Update_PlayedToAny_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var createResp = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud("Ya reproducida")));
        var solicitud = await createResp.Content.ReadFromJsonAsync<SolicitudResponse>();

        // Aceptar -> Reproducir
        await Client.SendAsync(DjPatch(roomId, solicitud!.Id, new { estado = "aceptada", version = solicitud.Version }));
        var acceptResp = await Client.GetFromJsonAsync<SolicitudPageResponse>($"/api/rooms/{roomId}/requests");
        var current = acceptResp!.Items.First(s => s.Nombre == "Ya reproducida");
        await Client.SendAsync(DjPatch(roomId, current.Id, new { estado = "reproducida", version = current.Version }));

        // Intentar cambiar desde reproducida
        var latestResp = await Client.GetFromJsonAsync<SolicitudPageResponse>($"/api/rooms/{roomId}/requests");
        var latest = latestResp!.Items.First(s => s.Nombre == "Ya reproducida");
        var response = await Client.SendAsync(DjPatch(roomId, latest.Id, new { estado = "aceptada", version = latest.Version }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DJ_Update_StaleVersion_Returns409()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var createResp = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud("Versión mala")));
        var solicitud = await createResp.Content.ReadFromJsonAsync<SolicitudResponse>();

        var response = await Client.SendAsync(DjPatch(roomId, solicitud!.Id, new { estado = "aceptada", version = 999 }));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DJ_Update_NonPositiveVersion_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var createResp = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud("Versión inválida")));
        var solicitud = await createResp.Content.ReadFromJsonAsync<SolicitudResponse>();

        var response = await Client.SendAsync(DjPatch(roomId, solicitud!.Id, new { estado = "aceptada", version = 0 }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DJ_Update_OtherDjRoom_Returns404()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var createResp = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud()));
        var solicitud = await createResp.Content.ReadFromJsonAsync<SolicitudResponse>();

        var client2 = _factory.CreateClient();
        await RegisterAndLoginAsync(client2, "dj2@test.com");

        var response = await client2.SendAsync(DjPatch(roomId, solicitud!.Id, new { estado = "aceptada", version = solicitud.Version }));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DJ_Update_InvalidEstado_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var createResp = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud()));
        var solicitud = await createResp.Content.ReadFromJsonAsync<SolicitudResponse>();

        var response = await Client.SendAsync(DjPatch(roomId, solicitud!.Id, new { estado = "invalido", version = solicitud.Version }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Límites

    [Fact]
    public async Task Create_QuotaExceeded_Returns429()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        // Crear sala con MaxSolicitudesSesion = 2
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();
            var sala = await db.Salas.FindAsync(roomId);
            sala!.MaxSolicitudesSesion = 2;
            await db.SaveChangesAsync();
        }

        // Crear 2 solicitudes (cuota OK, respetando el intervalo)
        var r1 = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud("Una")));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        FakeTime.Advance(TimeSpan.FromSeconds(31));
        var r2 = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud("Dos")));
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);

        // Tercera debe fallar
        var r3 = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud("Tres")));
        Assert.Equal(HttpStatusCode.TooManyRequests, r3.StatusCode);
    }

    [Fact]
    public async Task Create_IntervalTooShort_Returns429()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        // Primera solicitud OK
        var r1 = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud("Primera")));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        // Segunda inmediata debe fallar (intervalo 30s por defecto)
        var r2 = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud("Segunda")));
        Assert.Equal(HttpStatusCode.TooManyRequests, r2.StatusCode);
        Assert.True(r2.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal("application/problem+json", r2.Content.Headers.ContentType?.MediaType);

        // Avanzar tiempo y reintentar
        FakeTime.Advance(TimeSpan.FromSeconds(31));
        var r3 = await Client.SendAsync(GuestPost(roomId, token, MakeSolicitud("Tercera")));
        Assert.Equal(HttpStatusCode.Created, r3.StatusCode);
    }

    #endregion

    #region Revalidación post-bloqueo (concurrencia)

    private sealed record SeededSession(string Token, string Hash, Guid Id);

    private static async Task<SeededSession> SeedSessionAsync(
        WebAppFactory factory,
        Guid salaId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        var (token, hash) = MakeToken();
        var id = Guid.NewGuid();

        await using var db = await factory.CreateDbContextAsync();
        db.SesionesInvitado.Add(new SesionInvitado
        {
            Id = id,
            SalaId = salaId,
            TokenHash = hash,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            RevokedAt = null,
            UltimoEnvioAt = null
        });
        await db.SaveChangesAsync();

        return new SeededSession(token, hash, id);
    }

    private static async Task<Guid> SeedSolicitudAsync(
        WebAppFactory factory,
        Guid salaId,
        Guid sesionId,
        string nombre,
        DateTimeOffset now)
    {
        var id = Guid.NewGuid();

        await using var db = await factory.CreateDbContextAsync();
        db.Solicitudes.Add(new Solicitud
        {
            Id = id,
            SalaId = salaId,
            SesionId = sesionId,
            Nombre = nombre,
            Estado = "pendiente",
            IdempotencyKey = Guid.NewGuid(),
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        return id;
    }

    [Fact]
    public async Task Create_SalaFinalizadaMientrasEsperaBloqueo_Returns409()
    {
        await using var factory = new WebAppFactory(_postgres) { EnableRoomLockPause = true };
        await factory.InitializeAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var fakeTime = (FakeTimeProvider)factory.Services.GetRequiredService<TimeProvider>();

        await RegisterAndLoginAsync(client, "lock-room-end@test.com");
        var room = await CreateRoomAsync(client, fakeTime.UtcNow);
        var session = await SeedSessionAsync(factory, room.Id, fakeTime.UtcNow, room.EndsAt);

        var lockPause = factory.RoomLockPause!;
        var now0 = fakeTime.UtcNow;

        var postTask = client.SendAsync(GuestPost(room.Id, session.Token, MakeSolicitud()));

        await lockPause.Paused.WaitAsync(TimeSpan.FromSeconds(10));
        fakeTime.UtcNow = now0.AddHours(5);
        lockPause.Resume();

        var response = await postTask;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_SalaPausadaMientrasEsperaBloqueo_Returns409()
    {
        await using var factory = new WebAppFactory(_postgres) { EnableRoomLockPause = true };
        await factory.InitializeAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var fakeTime = (FakeTimeProvider)factory.Services.GetRequiredService<TimeProvider>();

        await RegisterAndLoginAsync(client, "lock-room-pause@test.com");
        var room = await CreateRoomAsync(client, fakeTime.UtcNow);
        var session = await SeedSessionAsync(factory, room.Id, fakeTime.UtcNow, room.EndsAt);

        var lockPause = factory.RoomLockPause!;

        var postTask = client.SendAsync(GuestPost(room.Id, session.Token, MakeSolicitud()));

        await lockPause.Paused.WaitAsync(TimeSpan.FromSeconds(10));

        // El FOR UPDATE aún no se ejecutó: otro comando puede pausar la sala.
        var current = await client.GetFromJsonAsync<RoomResponse>($"/api/rooms/{room.Id}");
        var pauseResponse = await client.PatchAsJsonAsync($"/api/rooms/{room.Id}/state",
            new { state = "pausada", version = current!.Version });
        Assert.Equal(HttpStatusCode.OK, pauseResponse.StatusCode);

        lockPause.Resume();

        var response = await postTask;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_SesionExpiradaMientrasEsperaBloqueoSesion_Returns401()
    {
        await using var factory = new WebAppFactory(_postgres) { EnableSesionLockPause = true };
        await factory.InitializeAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var fakeTime = (FakeTimeProvider)factory.Services.GetRequiredService<TimeProvider>();

        await RegisterAndLoginAsync(client, "lock-session-exp@test.com");
        var now0 = fakeTime.UtcNow;
        var room = await CreateRoomAsync(client, now0);
        var session = await SeedSessionAsync(factory, room.Id, now0, now0.AddSeconds(10));

        var lockPause = factory.SesionLockPause!;

        var postTask = client.SendAsync(GuestPost(room.Id, session.Token, MakeSolicitud()));

        await lockPause.Paused.WaitAsync(TimeSpan.FromSeconds(10));
        fakeTime.UtcNow = now0.AddSeconds(20);
        lockPause.Resume();

        var response = await postTask;
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_SesionRevocadaMientrasEsperaBloqueoSesion_Returns401()
    {
        await using var factory = new WebAppFactory(_postgres) { EnableSesionLockPause = true };
        await factory.InitializeAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var fakeTime = (FakeTimeProvider)factory.Services.GetRequiredService<TimeProvider>();

        await RegisterAndLoginAsync(client, "lock-session-rev@test.com");
        var room = await CreateRoomAsync(client, fakeTime.UtcNow);
        var session = await SeedSessionAsync(factory, room.Id, fakeTime.UtcNow, room.EndsAt);

        var lockPause = factory.SesionLockPause!;

        var postTask = client.SendAsync(GuestPost(room.Id, session.Token, MakeSolicitud()));

        await lockPause.Paused.WaitAsync(TimeSpan.FromSeconds(10));

        await using (var db = await factory.CreateDbContextAsync())
        {
            var sesion = await db.SesionesInvitado.SingleAsync(s => s.TokenHash == session.Hash);
            sesion.RevokedAt = fakeTime.UtcNow;
            await db.SaveChangesAsync();
        }

        lockPause.Resume();

        var response = await postTask;
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateState_SalaCaducadaMientrasEsperaBloqueo_Returns404()
    {
        await using var factory = new WebAppFactory(_postgres) { EnableRoomLockPause = true };
        await factory.InitializeAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var fakeTime = (FakeTimeProvider)factory.Services.GetRequiredService<TimeProvider>();

        await RegisterAndLoginAsync(client, "lock-update-expired@test.com");
        var now0 = fakeTime.UtcNow;
        var room = await CreateRoomAsync(client, now0);
        var session = await SeedSessionAsync(factory, room.Id, now0, room.EndsAt);
        var solicitudId = await SeedSolicitudAsync(factory, room.Id, session.Id, "Caduca", now0);

        var lockPause = factory.RoomLockPause!;

        var patchTask = client.SendAsync(DjPatch(room.Id, solicitudId, new { estado = "aceptada", version = 1 }));

        await lockPause.Paused.WaitAsync(TimeSpan.FromSeconds(10));
        fakeTime.UtcNow = now0.AddDays(8);
        lockPause.Resume();

        var response = await patchTask;
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion
}
