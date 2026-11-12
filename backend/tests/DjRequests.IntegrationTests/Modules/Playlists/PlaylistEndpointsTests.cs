using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Accounts.Dtos;
using DjRequests.Api.Modules.Guests.Dtos;
using DjRequests.Api.Modules.Playlists.Dtos;
using DjRequests.Api.Modules.Requests.Dtos;
using DjRequests.Api.Modules.Rooms.Dtos;
using DjRequests.Api.Persistence;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Playlists;

[Collection(IntegrationCollection.Name)]
public class PlaylistEndpointsTests : IAsyncLifetime
{
    private const string Password = "validpassphrase123";
    private readonly PostgresFixture _postgres;
    private WebAppFactory _factory = null!;
    private HttpClient _client = null!;

    public PlaylistEndpointsTests(PostgresFixture postgres)
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

    // Tras avanzar el reloj ficticio mas de 15 minutos el bearer del DJ expira;
    // los tests que simulan horas deben renovar la sesion antes de llamar a la API.
    private async Task LoginAsync(HttpClient client, string email = "dj@test.com")
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task SetMaxPlaylistsAsync(Guid roomId, int maxPlaylists)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();
        var sala = await db.Salas.FindAsync(roomId);
        sala!.MaxPlaylistsSesion = maxPlaylists;
        await db.SaveChangesAsync();
    }

    private static HttpRequestMessage GuestPostPlaylist(Guid roomId, string? token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/guest/rooms/{roomId}/playlists")
        {
            Content = JsonContent.Create(body)
        };
        if (token is not null)
        {
            request.Headers.Add("X-Guest-Token", token);
        }
        return request;
    }

    private static HttpRequestMessage GuestPostSolicitud(Guid roomId, string? token, object body)
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

    private static HttpRequestMessage GuestGetPlaylists(Guid roomId, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/guest/rooms/{roomId}/playlists");
        if (token is not null)
        {
            request.Headers.Add("X-Guest-Token", token);
        }
        return request;
    }

    private static HttpRequestMessage DjGetPlaylists(Guid roomId, int page = 1, int pageSize = 20)
    {
        return new HttpRequestMessage(HttpMethod.Get, $"/api/rooms/{roomId}/playlists?page={page}&pageSize={pageSize}");
    }

    private static CreatePlaylistRequest MakePlaylist(
        string? nombre = "Playlist Test",
        string? enlace = "https://example.com/playlist",
        Guid? idempotencyKey = null)
    {
        return new CreatePlaylistRequest(nombre, enlace, idempotencyKey ?? Guid.NewGuid());
    }

    private async Task<Guid> CreatePlaylistAsync(
        Guid roomId,
        string token,
        string nombre = "Playlist",
        string enlace = "https://example.com/list",
        bool advanceInterval = true)
    {
        if (advanceInterval)
        {
            FakeTime.Advance(TimeSpan.FromSeconds(31));
        }
        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist(nombre, enlace)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var playlist = await response.Content.ReadFromJsonAsync<PlaylistResponse>();
        Assert.NotNull(playlist);
        return playlist.Id;
    }

    #endregion

    #region POST - Envio valido

    [Fact]
    public async Task Create_ValidPlaylist_Returns201WithPlaylist()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist("Fiesta", "https://open.spotify.com/playlist/abc")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Cache-Control", out var cacheValues));
        Assert.Contains("no-store", cacheValues);

        var playlist = await response.Content.ReadFromJsonAsync<PlaylistResponse>();
        Assert.NotNull(playlist);
        Assert.Equal("Fiesta", playlist.Nombre);
        Assert.Equal("https://open.spotify.com/playlist/abc", playlist.Enlace);
        Assert.NotEqual(Guid.Empty, playlist.Id);

        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sesion", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("salaId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WithoutNombre_Returns201WithNullNombre()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist(null, "https://example.com/only-link")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var playlist = await response.Content.ReadFromJsonAsync<PlaylistResponse>();
        Assert.NotNull(playlist);
        Assert.Null(playlist.Nombre);
    }

    [Fact]
    public async Task Create_NormalizesNombreAndEnlace()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist("   Mix de noche   ", "   https://example.com/mix   ")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var playlist = await response.Content.ReadFromJsonAsync<PlaylistResponse>();
        Assert.NotNull(playlist);
        Assert.Equal("Mix de noche", playlist.Nombre);
        Assert.Equal("https://example.com/mix", playlist.Enlace);
    }

    [Fact]
    public async Task Create_BlankNombre_BecomesNull()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist("    ", "https://example.com/blank")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var playlist = await response.Content.ReadFromJsonAsync<PlaylistResponse>();
        Assert.NotNull(playlist);
        Assert.Null(playlist.Nombre);
    }

    [Fact]
    public async Task Create_PersistsInDatabase()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist("Persistida", "https://example.com/persist")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PlaylistResponse>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();
        var stored = await db.PlaylistsCompartidas
            .Include(p => p.SesionInvitado)
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == created!.Id);
        Assert.NotNull(stored);
        Assert.Equal(roomId, stored.SalaId);
        Assert.Equal(created!.Id, stored.Id);
        Assert.False(string.IsNullOrEmpty(stored.SesionInvitado.TokenHash));
    }

    #endregion

    #region POST - Validaciones

    [Fact]
    public async Task Create_EmptyEnlace_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist("Mix", "   ")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_NullEnlace_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist("Mix", null)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidEnlaceScheme_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist("Mix", "not-a-url")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_FtpEnlace_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist("Mix", "ftp://example.com/playlist")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_EnlaceWithCredentials_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist("Mix", "https://user:pass@example.com/playlist")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_EnlaceWithoutHost_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist("Mix", "https://")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_TooLongEnlace_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var longLink = "https://example.com/" + new string('a', 2048);
        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist("Mix", longLink)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_TooLongNombre_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist(new string('n', 201), "https://example.com/long")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingIdempotencyKey_Returns400()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            new { nombre = "Sin clave", enlace = "https://example.com/nokey" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region POST - Token invalido

    [Fact]
    public async Task Create_NoToken_Returns401()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, null, MakePlaylist()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Cache-Control", out var cacheValues));
        Assert.Contains("no-store", cacheValues);
    }

    [Fact]
    public async Task Create_MalformedToken_Returns401()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, "invalid-token", MakePlaylist()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_TokenFromWrongRoom_Returns404()
    {
        var (roomId1, code1) = await CreateRoomAndGetCodeAsync();
        var token1 = await CreateGuestSessionAsync(Client, code1);

        var room2 = await CreateRoomAsync(Client, FakeTime.UtcNow, "Otra Sala");

        var response = await Client.SendAsync(GuestPostPlaylist(room2.Id, token1, MakePlaylist()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region POST - Sala no disponible

    [Fact]
    public async Task Create_RoomPaused_Returns409()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var room = await Client.GetFromJsonAsync<RoomResponse>($"/api/rooms/{roomId}");
        var pauseResponse = await Client.PatchAsJsonAsync($"/api/rooms/{roomId}/state",
            new { state = "pausada", version = room!.Version });
        Assert.Equal(HttpStatusCode.OK, pauseResponse.StatusCode);

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist()));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_RoomEnded_Returns404()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var room = await Client.GetFromJsonAsync<RoomResponse>($"/api/rooms/{roomId}");
        var updateResponse = await Client.PutAsJsonAsync($"/api/rooms/{roomId}", new UpdateRoomRequest(
            room!.Version, "Sala Test", FormatIso(FakeTime.UtcNow.AddHours(1)), FormatIso(FakeTime.UtcNow.AddHours(2)), null));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        FakeTime.Advance(TimeSpan.FromHours(3));

        var response = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region POST - Idempotencia

    [Fact]
    public async Task Create_IdempotentRetry_SamePayload_Returns200WithOriginal()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var idemKey = Guid.NewGuid();
        var response1 = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist("Playlist Idem", "https://example.com/idem", idemKey)));
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);
        var playlist1 = await response1.Content.ReadFromJsonAsync<PlaylistResponse>();

        var response2 = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist("Playlist Idem", "https://example.com/idem", idemKey)));
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var playlist2 = await response2.Content.ReadFromJsonAsync<PlaylistResponse>();

        Assert.NotNull(playlist1);
        Assert.NotNull(playlist2);
        Assert.Equal(playlist1.Id, playlist2.Id);
    }

    [Fact]
    public async Task Create_IdempotentRetry_DifferentPayload_Returns409()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var idemKey = Guid.NewGuid();
        await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist("Original", "https://example.com/original", idemKey)));

        var response2 = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist("Diferente", "https://example.com/original", idemKey)));

        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    [Fact]
    public async Task Create_IdempotentRetry_WithFullQuota_ReplaysWithoutConsumingQuota()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();
            var sala = await db.Salas.FindAsync(roomId);
            sala!.MaxPlaylistsSesion = 1;
            await db.SaveChangesAsync();
        }

        var idemKey = Guid.NewGuid();
        var response1 = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist("Unica", "https://example.com/only", idemKey)));
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);

        // Segunda distinta: cuota agotada.
        var response2 = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist("Otra", "https://example.com/other")));
        Assert.Equal(HttpStatusCode.TooManyRequests, response2.StatusCode);

        // Reintento de la primera con misma clave: replay sin cuota ni intervalo.
        var response3 = await Client.SendAsync(GuestPostPlaylist(roomId, token,
            MakePlaylist("Unica", "https://example.com/only", idemKey)));
        Assert.Equal(HttpStatusCode.OK, response3.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();
            var count = await db.PlaylistsCompartidas.CountAsync(p => p.SalaId == roomId);
            Assert.Equal(1, count);
        }
    }

    #endregion

    #region Limites - cuota e intervalo compartido

    [Fact]
    public async Task Create_QuotaExceeded_Returns429()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();
            var sala = await db.Salas.FindAsync(roomId);
            sala!.MaxPlaylistsSesion = 2;
            await db.SaveChangesAsync();
        }

        // Crear 2 playlists (cuota OK, respetando el intervalo)
        var r1 = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist("Una")));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        FakeTime.Advance(TimeSpan.FromSeconds(31));
        var r2 = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist("Dos")));
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);

        // Tercera debe fallar por cuota
        var r3 = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist("Tres")));
        Assert.Equal(HttpStatusCode.TooManyRequests, r3.StatusCode);

        var problem = await r3.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("playlists", problem.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_IntervalTooShort_Returns429()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var r1 = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist("Primera")));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        // Segunda inmediata debe fallar (intervalo 30s compartido por defecto)
        var r2 = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist("Segunda")));
        Assert.Equal(HttpStatusCode.TooManyRequests, r2.StatusCode);
        Assert.True(r2.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        Assert.Equal("application/problem+json", r2.Content.Headers.ContentType?.MediaType);

        // Avanzar tiempo y reintentar
        FakeTime.Advance(TimeSpan.FromSeconds(31));
        var r3 = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist("Tercera")));
        Assert.Equal(HttpStatusCode.Created, r3.StatusCode);
    }

    [Fact]
    public async Task Create_ImmediatelyAfterSong_Returns429SharedInterval()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var songResponse = await Client.SendAsync(GuestPostSolicitud(roomId, token,
            new CreateSolicitudRequest("Cancion", null, null, null, Guid.NewGuid())));
        Assert.Equal(HttpStatusCode.Created, songResponse.StatusCode);

        // La playlist enviada justo despues debe respetar el mismo intervalo.
        var playlistResponse = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist()));
        Assert.Equal(HttpStatusCode.TooManyRequests, playlistResponse.StatusCode);
        Assert.True(playlistResponse.Headers.RetryAfter?.Delta > TimeSpan.Zero);

        FakeTime.Advance(TimeSpan.FromSeconds(31));
        var retry = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist()));
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Fact]
    public async Task Create_SongImmediatelyAfterPlaylist_Returns429SharedInterval()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var playlistResponse = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist()));
        Assert.Equal(HttpStatusCode.Created, playlistResponse.StatusCode);

        var songResponse = await Client.SendAsync(GuestPostSolicitud(roomId, token,
            new CreateSolicitudRequest("Cancion", null, null, null, Guid.NewGuid())));
        Assert.Equal(HttpStatusCode.TooManyRequests, songResponse.StatusCode);
        Assert.True(songResponse.Headers.RetryAfter?.Delta > TimeSpan.Zero);

        FakeTime.Advance(TimeSpan.FromSeconds(31));
        var retry = await Client.SendAsync(GuestPostSolicitud(roomId, token,
            new CreateSolicitudRequest("Cancion", null, null, null, Guid.NewGuid())));
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Fact]
    public async Task Create_ConcurrentDifferentKeys_ExactlyOneSucceeds()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        var task1 = Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist("Paralela A")));
        var task2 = Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist("Paralela B")));
        var responses = await Task.WhenAll(task1, task2);

        var statuses = responses.Select(r => r.StatusCode).OrderBy(s => s).ToArray();
        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.TooManyRequests }, statuses);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DjRequestsDbContext>();
        var count = await db.PlaylistsCompartidas.CountAsync(p => p.SalaId == roomId);
        Assert.Equal(1, count);
    }

    #endregion

    #region GET - Listado del DJ

    [Fact]
    public async Task DJ_List_ReturnsPlaylistsNewestFirst()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);
        await SetMaxPlaylistsAsync(roomId, 10);

        for (int i = 0; i < 3; i++)
        {
            if (i > 0) FakeTime.Advance(TimeSpan.FromSeconds(31));
            var resp = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist($"Playlist {i}")));
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        }

        var response = await Client.SendAsync(DjGetPlaylists(roomId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PlaylistPageResponse>();
        Assert.NotNull(page);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.Items.Count);
        Assert.False(page.HasMore);
        Assert.Equal("Playlist 2", page.Items[0].Nombre);
        Assert.Equal("Playlist 0", page.Items[2].Nombre);
    }

    [Fact]
    public async Task DJ_List_Paginates()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);
        await SetMaxPlaylistsAsync(roomId, 10);

        for (int i = 0; i < 3; i++)
        {
            if (i > 0) FakeTime.Advance(TimeSpan.FromSeconds(31));
            var resp = await Client.SendAsync(GuestPostPlaylist(roomId, token, MakePlaylist($"Lista {i}")));
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        }

        var page1Response = await Client.SendAsync(DjGetPlaylists(roomId, page: 1, pageSize: 2));
        var page1 = await page1Response.Content.ReadFromJsonAsync<PlaylistPageResponse>();
        Assert.NotNull(page1);
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.HasMore);

        var page2Response = await Client.SendAsync(DjGetPlaylists(roomId, page: 2, pageSize: 2));
        var page2 = await page2Response.Content.ReadFromJsonAsync<PlaylistPageResponse>();
        Assert.NotNull(page2);
        Assert.Single(page2.Items);
        Assert.False(page2.HasMore);
    }

    [Fact]
    public async Task DJ_List_WithoutAuth_Returns401()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();

        var anonymous = _factory.CreateClient();
        var response = await anonymous.SendAsync(DjGetPlaylists(roomId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DJ_List_OtherDjRoom_Returns404()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();

        var client2 = _factory.CreateClient();
        await RegisterAndLoginAsync(client2, "dj2@test.com");

        var response = await client2.SendAsync(DjGetPlaylists(roomId));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DJ_List_ExtremelyLargePage_Returns400()
    {
        var (roomId, _) = await CreateRoomAndGetCodeAsync();

        var response = await Client.SendAsync(DjGetPlaylists(roomId, page: int.MaxValue, pageSize: 100));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DJ_List_AfterRoomEnds_Returns200()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        await CreatePlaylistAsync(roomId, token, "Antes del fin", "https://example.com/before", advanceInterval: false);

        // La sala termino (+4h) pero purgeAt sigue vigente (+168h despues del fin).
        FakeTime.Advance(TimeSpan.FromHours(5));
        await LoginAsync(Client);

        var response = await Client.SendAsync(DjGetPlaylists(roomId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PlaylistPageResponse>();
        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Equal("Antes del fin", page.Items[0].Nombre);
    }

    [Fact]
    public async Task DJ_List_AfterPurgeAt_Returns404()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        await CreatePlaylistAsync(roomId, token, "Caducada", "https://example.com/expired", advanceInterval: false);

        // Fin +4h y purgeAt = fin + 168h: superar la ventana de retencion.
        FakeTime.Advance(TimeSpan.FromHours(175));
        await LoginAsync(Client);

        var response = await Client.SendAsync(DjGetPlaylists(roomId));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DJ_List_ResponseDoesNotExposeSessionData()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        await CreatePlaylistAsync(roomId, token, "Privada", "https://example.com/private");

        var response = await Client.SendAsync(DjGetPlaylists(roomId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("sesion", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("salaId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("idempotency", json, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Privacidad - invitados no listan playlists

    [Fact]
    public async Task Guest_HasNoPlaylistListingEndpoint()
    {
        var (roomId, code) = await CreateRoomAndGetCodeAsync();
        var token = await CreateGuestSessionAsync(Client, code);

        await CreatePlaylistAsync(roomId, token, "Solo DJ", "https://example.com/only-dj");

        var response = await Client.SendAsync(GuestGetPlaylists(roomId, token));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Solo DJ", body);
        Assert.DoesNotContain("https://example.com/only-dj", body);
    }

    #endregion
}
