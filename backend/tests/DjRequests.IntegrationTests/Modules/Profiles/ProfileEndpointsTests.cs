using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DjRequests.Api.Modules.Accounts.Dtos;
using DjRequests.Api.Modules.Guests.Dtos;
using DjRequests.Api.Modules.Profiles.Dtos;
using DjRequests.Api.Modules.Rooms.Dtos;
using DjRequests.Api.Persistence;
using DjRequests.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Profiles;

[Collection(IntegrationCollection.Name)]
public class ProfileEndpointsTests : IAsyncLifetime
{
    private const string Password = "validpassphrase123";
    private readonly PostgresFixture _postgres;
    private readonly string _storageDir = Path.Combine(
        Path.GetTempPath(),
        "dj-requests-tests",
        Guid.NewGuid().ToString("N"));
    private WebAppFactory _factory = null!;
    private HttpClient _client = null!;

    public ProfileEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_storageDir);

        _factory = new WebAppFactory(_postgres, new Dictionary<string, string?>
        {
            ["FileStorage:Path"] = _storageDir,
            ["FileStorage:MaxFileSizeBytes"] = (5 * 1024 * 1024).ToString()
        });
        await _factory.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();

        try
        {
            Directory.Delete(_storageDir, recursive: true);
        }
        catch
        {
        }
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

    private FakeTimeProvider FakeTime =>
        (FakeTimeProvider)_factory.Services.GetRequiredService<TimeProvider>();

    private static string FormatIso(DateTimeOffset value) => value.ToString("O");

    private static byte[] CreatePng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }

    private static MultipartFormDataContent BuildPhotoForm(
        byte[] bytes,
        string fileName = "photo.png",
        string contentType = "image/png")
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, "file", fileName);
        return form;
    }

    private async Task RegisterAndLoginAsync(string email = "dj@test.com", string artistName = "DJ Test")
    {
        var registerResponse = await Client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, Password, artistName));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task<RoomResponse> CreateRoomAsync(string name = "Sala Test")
    {
        var now = FakeTime.UtcNow;
        var response = await Client.PostAsJsonAsync("/api/rooms",
            new CreateRoomRequest(name, FormatIso(now.AddHours(1)), FormatIso(now.AddHours(4)), null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.NotNull(room);
        return room;
    }

    private async Task<string> CreateGuestSessionAsync(string code)
    {
        var response = await Client.PostAsJsonAsync("/api/guest-sessions",
            new CreateGuestSessionRequest(code));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<GuestSessionResponse>();
        Assert.NotNull(session);
        Assert.NotNull(session.GuestToken);
        return session.GuestToken;
    }

    private static HttpRequestMessage GuestGetDj(Guid roomId, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/guest/rooms/{roomId}/dj");
        if (token is not null)
        {
            request.Headers.Add("X-Guest-Token", token);
        }
        return request;
    }

    #region GET /api/dj/profile

    [Fact]
    public async Task GetMine_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/dj/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetMine_NoProfileYet_ReturnsDefaultsWithArtistName()
    {
        await RegisterAndLoginAsync(artistName: "DJ Inicial");

        var response = await Client.GetAsync("/api/dj/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(body);
        Assert.Equal("DJ Inicial", body.ArtistName);
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

    #endregion

    #region PATCH /api/dj/profile

    [Fact]
    public async Task Update_ValidData_PersistsProfileAndArtistName()
    {
        await RegisterAndLoginAsync(artistName: "DJ Original");
        var startYear = FakeTime.UtcNow.Year - 5;

        var request = new UpdateProfileRequest(
            ArtistName: "  DJ Renombrado  ",
            Description: "  Música electrónica y latina  ",
            StartYear: startYear,
            SocialLinks: new SocialLinksRequest(
                "https://www.instagram.com/djrenombrado",
                "https://www.facebook.com/djrenombrado",
                "https://www.tiktok.com/@djrenombrado",
                "https://www.youtube.com/@djrenombrado",
                "https://soundcloud.com/djrenombrado"));

        var response = await Client.PatchAsJsonAsync("/api/dj/profile", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(body);
        Assert.Equal("DJ Renombrado", body.ArtistName);
        Assert.Equal("Música electrónica y latina", body.Description);
        Assert.Equal(startYear, body.StartYear);
        Assert.Equal("https://www.instagram.com/djrenombrado", body.SocialLinks.Instagram);
        Assert.Equal("https://www.facebook.com/djrenombrado", body.SocialLinks.Facebook);
        Assert.Equal("https://www.tiktok.com/@djrenombrado", body.SocialLinks.Tiktok);
        Assert.Equal("https://www.youtube.com/@djrenombrado", body.SocialLinks.Youtube);
        Assert.Equal("https://soundcloud.com/djrenombrado", body.SocialLinks.Soundcloud);

        await using var context = await _factory.CreateDbContextAsync();
        var dj = await context.Users.SingleAsync(u => u.Email == "dj@test.com");
        Assert.Equal("DJ Renombrado", dj.NombreArtistico);

        var profile = await context.PerfilesDj.SingleAsync(p => p.DjId == dj.Id);
        Assert.Equal("Música electrónica y latina", profile.Descripcion);
        Assert.Equal(startYear, profile.AnioInicio);

        var me = await Client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(meBody);
        Assert.Equal("DJ Renombrado", meBody.ArtistName);
    }

    [Fact]
    public async Task Update_NullOptionalFields_ClearsThemButKeepsArtistName()
    {
        await RegisterAndLoginAsync(artistName: "DJ Estable");

        await Client.PatchAsJsonAsync("/api/dj/profile", new UpdateProfileRequest(
            "DJ Temporal",
            "Descripción temporal",
            FakeTime.UtcNow.Year - 2,
            new SocialLinksRequest(
                "https://www.instagram.com/temporal",
                null, null, null, null)));

        var response = await Client.PatchAsJsonAsync("/api/dj/profile",
            new UpdateProfileRequest(null, null, null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(body);
        Assert.Equal("DJ Temporal", body.ArtistName);
        Assert.Null(body.Description);
        Assert.Null(body.StartYear);
        Assert.Null(body.SocialLinks.Instagram);
    }

    [Fact]
    public async Task Update_OmittedFields_PreservesExistingValues()
    {
        await RegisterAndLoginAsync(artistName: "DJ Original");
        var startYear = FakeTime.UtcNow.Year - 4;

        var initial = await Client.PatchAsJsonAsync("/api/dj/profile", new UpdateProfileRequest(
            "DJ Original",
            "Descripción existente",
            startYear,
            new SocialLinksRequest(
                "https://www.instagram.com/original",
                "https://www.facebook.com/original",
                null,
                null,
                null)));
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);

        using var content = new StringContent(
            """{"artistName":"DJ Parcial","socialLinks":{"instagram":"https://www.instagram.com/parcial"}}""",
            Encoding.UTF8,
            "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/dj/profile")
        {
            Content = content
        };

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(body);
        Assert.Equal("DJ Parcial", body.ArtistName);
        Assert.Equal("Descripción existente", body.Description);
        Assert.Equal(startYear, body.StartYear);
        Assert.Equal("https://www.instagram.com/parcial", body.SocialLinks.Instagram);
        Assert.Equal("https://www.facebook.com/original", body.SocialLinks.Facebook);
    }

    [Fact]
    public async Task Update_ConcurrentFirstWrites_CreateSingleProfile()
    {
        await RegisterAndLoginAsync();

        var first = Client.PatchAsJsonAsync("/api/dj/profile", new UpdateProfileRequest(
            null,
            "Primera descripción",
            null,
            null));
        var second = Client.PatchAsJsonAsync("/api/dj/profile", new UpdateProfileRequest(
            null,
            "Segunda descripción",
            null,
            null));

        var responses = await Task.WhenAll(first, second);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        await using var context = await _factory.CreateDbContextAsync();
        Assert.Equal(1, await context.PerfilesDj.CountAsync());
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(2101)]
    public async Task Update_InvalidStartYear_Returns400(int year)
    {
        await RegisterAndLoginAsync();

        var response = await Client.PatchAsJsonAsync("/api/dj/profile",
            new UpdateProfileRequest(null, null, year, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("StartYear", problem.Errors.Keys);
    }

    [Fact]
    public async Task Update_FutureStartYear_Returns400()
    {
        await RegisterAndLoginAsync();

        var response = await Client.PatchAsJsonAsync("/api/dj/profile",
            new UpdateProfileRequest(null, null, FakeTime.UtcNow.Year + 1, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("StartYear", problem.Errors.Keys);
    }

    [Fact]
    public async Task Update_DescriptionTooLong_Returns400()
    {
        await RegisterAndLoginAsync();

        var response = await Client.PatchAsJsonAsync("/api/dj/profile",
            new UpdateProfileRequest(null, new string('a', 1501), null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Description", problem.Errors.Keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Update_EmptyArtistName_Returns400(string artistName)
    {
        await RegisterAndLoginAsync();

        var response = await Client.PatchAsJsonAsync("/api/dj/profile",
            new UpdateProfileRequest(artistName, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("ArtistName", problem.Errors.Keys);
    }

    [Theory]
    [InlineData("http://www.instagram.com/dj")]
    [InlineData("https://evil.example/dj")]
    [InlineData("https://user@www.instagram.com/dj")]
    [InlineData("https://www.instagram.com:8443/dj")]
    public async Task Update_InvalidInstagramLink_Returns400(string link)
    {
        await RegisterAndLoginAsync();

        var response = await Client.PatchAsJsonAsync("/api/dj/profile",
            new UpdateProfileRequest(null, null, null,
                new SocialLinksRequest(link, null, null, null, null)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Instagram", problem.Errors.Keys);
    }

    [Fact]
    public async Task Update_InvalidYoutubeLink_Returns400()
    {
        await RegisterAndLoginAsync();

        var response = await Client.PatchAsJsonAsync("/api/dj/profile",
            new UpdateProfileRequest(null, null, null,
                new SocialLinksRequest(null, null, null, "https://vimeo.com/dj", null)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Youtube", problem.Errors.Keys);
    }

    #endregion

    #region POST/DELETE /api/dj/profile/photo

    [Fact]
    public async Task UploadPhoto_ValidPng_ReturnsUrlAndServesItAnonymously()
    {
        await RegisterAndLoginAsync();

        using var form = BuildPhotoForm(CreatePng(64, 64));
        var response = await Client.PostAsync("/api/dj/profile/photo", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(body);
        Assert.NotNull(body.PhotoUrl);
        Assert.StartsWith("/api/profile-photos/", body.PhotoUrl);

        var fileName = body.PhotoUrl!.Split('/').Last();
        Assert.Matches("^[a-f0-9]{32}\\.png$", fileName);
        Assert.True(File.Exists(Path.Combine(_storageDir, fileName)));

        using var anonymous = _factory.CreateClient();
        var photoResponse = await anonymous.GetAsync(body.PhotoUrl);
        Assert.Equal(HttpStatusCode.OK, photoResponse.StatusCode);
        Assert.Equal("image/png", photoResponse.Content.Headers.ContentType?.MediaType);
        Assert.True((await photoResponse.Content.ReadAsByteArrayAsync()).Length > 0);
        Assert.True(photoResponse.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task UploadPhoto_LargeImage_IsResizedToMaxDimension()
    {
        await RegisterAndLoginAsync();

        using var form = BuildPhotoForm(CreatePng(3000, 1000));
        var response = await Client.PostAsync("/api/dj/profile/photo", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(body);
        Assert.NotNull(body.PhotoUrl);

        var fileName = body.PhotoUrl!.Split('/').Last();
        using var stored = await Image.LoadAsync(Path.Combine(_storageDir, fileName));
        Assert.True(stored.Width <= 2048);
        Assert.True(stored.Height <= 2048);
        Assert.Equal(2048, stored.Width);
    }

    [Fact]
    public async Task UploadPhoto_ReplacingPhoto_DeletesPreviousFile()
    {
        await RegisterAndLoginAsync();

        using (var firstForm = BuildPhotoForm(CreatePng(32, 32)))
        {
            var first = await Client.PostAsync("/api/dj/profile/photo", firstForm);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        var firstBody = await (await Client.GetAsync("/api/dj/profile"))
            .Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(firstBody);
        var firstFile = Path.Combine(_storageDir, firstBody.PhotoUrl!.Split('/').Last());
        Assert.True(File.Exists(firstFile));

        using (var secondForm = BuildPhotoForm(CreatePng(48, 48)))
        {
            var second = await Client.PostAsync("/api/dj/profile/photo", secondForm);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        var secondBody = await (await Client.GetAsync("/api/dj/profile"))
            .Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(secondBody);
        var secondFile = Path.Combine(_storageDir, secondBody.PhotoUrl!.Split('/').Last());
        Assert.True(File.Exists(secondFile));
        Assert.False(File.Exists(firstFile));
        Assert.NotEqual(firstFile, secondFile);
    }

    [Fact]
    public async Task DeletePhoto_RemovesFileAndClearsUrl()
    {
        await RegisterAndLoginAsync();

        using (var form = BuildPhotoForm(CreatePng(16, 16)))
        {
            var upload = await Client.PostAsync("/api/dj/profile/photo", form);
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        }

        var withPhoto = await (await Client.GetAsync("/api/dj/profile"))
            .Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(withPhoto);
        var file = Path.Combine(_storageDir, withPhoto.PhotoUrl!.Split('/').Last());
        Assert.True(File.Exists(file));

        var delete = await Client.DeleteAsync("/api/dj/profile/photo");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);
        var body = await delete.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(body);
        Assert.Null(body.PhotoUrl);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task DeletePhoto_WhenNoPhoto_Returns200()
    {
        await RegisterAndLoginAsync();

        var response = await Client.DeleteAsync("/api/dj/profile/photo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(body);
        Assert.Null(body.PhotoUrl);
    }

    [Fact]
    public async Task UploadPhoto_NotAnImage_Returns400()
    {
        await RegisterAndLoginAsync();

        using var form = BuildPhotoForm(Encoding.UTF8.GetBytes("no soy una imagen"));
        var response = await Client.PostAsync("/api/dj/profile/photo", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("File", problem.Errors.Keys);
    }

    [Fact]
    public async Task UploadPhoto_TruncatedPng_Returns400()
    {
        await RegisterAndLoginAsync();
        var valid = CreatePng(64, 64);
        var truncated = valid[..Math.Min(64, valid.Length)];

        using var form = BuildPhotoForm(truncated);
        var response = await Client.PostAsync("/api/dj/profile/photo", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("File", problem.Errors.Keys);
    }

    [Fact]
    public async Task UploadPhoto_TooLarge_Returns400()
    {
        await RegisterAndLoginAsync();

        using var form = BuildPhotoForm(new byte[5 * 1024 * 1024 + 1]);
        var response = await Client.PostAsync("/api/dj/profile/photo", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("File", problem.Errors.Keys);
    }

    [Fact]
    public async Task UploadPhoto_WithoutFile_Returns400()
    {
        await RegisterAndLoginAsync();

        using var form = new MultipartFormDataContent();
        var response = await Client.PostAsync("/api/dj/profile/photo", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("File", problem.Errors.Keys);
    }

    [Fact]
    public async Task UploadPhoto_WrongFieldName_Returns400()
    {
        await RegisterAndLoginAsync();

        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(CreatePng(16, 16));
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(content, "photo", "photo.png");

        var response = await Client.PostAsync("/api/dj/profile/photo", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("File", problem.Errors.Keys);
    }

    [Fact]
    public async Task UploadPhoto_EmptyFile_Returns400()
    {
        await RegisterAndLoginAsync();

        using var form = BuildPhotoForm(Array.Empty<byte>());
        var response = await Client.PostAsync("/api/dj/profile/photo", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("File", problem.Errors.Keys);
    }

    [Fact]
    public async Task UploadPhoto_InvalidReplacement_KeepsPreviousPhoto()
    {
        await RegisterAndLoginAsync();

        using (var form = BuildPhotoForm(CreatePng(16, 16)))
        {
            var upload = await Client.PostAsync("/api/dj/profile/photo", form);
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        }

        var original = await (await Client.GetAsync("/api/dj/profile"))
            .Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(original);
        var originalFileName = original.PhotoUrl!.Split('/').Last();
        var originalFile = Path.Combine(_storageDir, originalFileName);
        Assert.True(File.Exists(originalFile));

        using (var invalidForm = BuildPhotoForm(Encoding.UTF8.GetBytes("no soy una imagen"), "bad.png"))
        {
            var invalid = await Client.PostAsync("/api/dj/profile/photo", invalidForm);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }

        var after = await (await Client.GetAsync("/api/dj/profile"))
            .Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(after);
        Assert.Equal(original.PhotoUrl, after.PhotoUrl);
        Assert.True(File.Exists(originalFile));

        using var anonymous = _factory.CreateClient();
        var photoResponse = await anonymous.GetAsync(after.PhotoUrl);
        Assert.Equal(HttpStatusCode.OK, photoResponse.StatusCode);
    }

    [Fact]
    public async Task GetPhoto_UnknownFile_Returns404()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/profile-photos/{Guid.NewGuid():N}.jpg");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPhoto_UnreferencedFile_Returns404()
    {
        var fileName = $"{Guid.NewGuid():N}.png";
        await File.WriteAllBytesAsync(Path.Combine(_storageDir, fileName), CreatePng(8, 8));
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/profile-photos/{fileName}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPhoto_InvalidName_Returns404()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/profile-photos/no-es-un-archivo-valido.jpg");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region GET /api/guest/rooms/{roomId}/dj

    [Fact]
    public async Task GuestDjProfile_AfterPhotoUpload_ExposesSamePhotoUrl()
    {
        await RegisterAndLoginAsync();

        using (var form = BuildPhotoForm(CreatePng(24, 24)))
        {
            var upload = await Client.PostAsync("/api/dj/profile/photo", form);
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        }

        var uploadBody = await (await Client.GetAsync("/api/dj/profile"))
            .Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(uploadBody);
        Assert.NotNull(uploadBody.PhotoUrl);

        var room = await CreateRoomAsync();
        var token = await CreateGuestSessionAsync(room.Codigo);

        using var guestClient = _factory.CreateClient();
        var guestResponse = await guestClient.SendAsync(GuestGetDj(room.Id, token));

        Assert.Equal(HttpStatusCode.OK, guestResponse.StatusCode);
        var guestBody = await guestResponse.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(guestBody);
        Assert.Equal(uploadBody.PhotoUrl, guestBody.PhotoUrl);

        var photoResponse = await guestClient.GetAsync(guestBody.PhotoUrl);
        Assert.Equal(HttpStatusCode.OK, photoResponse.StatusCode);
        Assert.Equal("image/png", photoResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GuestDjProfile_WithValidSession_ReturnsProfileWithoutPrivateFields()
    {
        await RegisterAndLoginAsync(artistName: "DJ Invitado");
        var startYear = FakeTime.UtcNow.Year - 3;

        var update = await Client.PatchAsJsonAsync("/api/dj/profile", new UpdateProfileRequest(
            null,
            "Descripción pública",
            startYear,
            new SocialLinksRequest("https://www.instagram.com/djinvitado", null, null, null, null)));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var room = await CreateRoomAsync();
        var token = await CreateGuestSessionAsync(room.Codigo);

        using var guestClient = _factory.CreateClient();
        var response = await guestClient.SendAsync(GuestGetDj(room.Id, token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<ProfileDto>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(body);
        Assert.Equal("DJ Invitado", body.ArtistName);
        Assert.Equal("Descripción pública", body.Description);
        Assert.Equal(startYear, body.StartYear);
        Assert.Equal("https://www.instagram.com/djinvitado", body.SocialLinks.Instagram);

        using var doc = JsonDocument.Parse(raw);
        var properties = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "artistName", "description", "photoUrl", "socialLinks", "startYear" }, properties);

        Assert.DoesNotContain("dj@test.com", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuestDjProfile_WithoutToken_Returns401()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        using var guestClient = _factory.CreateClient();
        var response = await guestClient.SendAsync(GuestGetDj(room.Id, token: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GuestDjProfile_InvalidToken_Returns401()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();

        using var guestClient = _factory.CreateClient();
        var response = await guestClient.SendAsync(GuestGetDj(room.Id, "not-a-valid-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GuestDjProfile_UnknownRoom_Returns404()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();
        var token = await CreateGuestSessionAsync(room.Codigo);

        using var guestClient = _factory.CreateClient();
        var response = await guestClient.SendAsync(GuestGetDj(Guid.NewGuid(), token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GuestDjProfile_TokenForAnotherRoom_Returns404()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync("Sala Uno");
        var token = await CreateGuestSessionAsync(room.Codigo);
        var otherRoom = await CreateRoomAsync("Sala Dos");

        using var guestClient = _factory.CreateClient();
        var response = await guestClient.SendAsync(GuestGetDj(otherRoom.Id, token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GuestDjProfile_SuspendedDj_Returns404()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();
        var token = await CreateGuestSessionAsync(room.Codigo);

        await using (var context = await _factory.CreateDbContextAsync())
        {
            var dj = await context.Users.SingleAsync(u => u.Email == "dj@test.com");
            dj.Estado = "suspendido";
            await context.SaveChangesAsync();
        }

        using var guestClient = _factory.CreateClient();
        var response = await guestClient.SendAsync(GuestGetDj(room.Id, token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GuestDjProfile_RateLimit_IsPartitionedByValidSession()
    {
        await RegisterAndLoginAsync();
        var room = await CreateRoomAsync();
        var firstToken = await CreateGuestSessionAsync(room.Codigo);
        var secondToken = await CreateGuestSessionAsync(room.Codigo);

        using var guestClient = _factory.CreateClient();
        for (var index = 0; index < 120; index++)
        {
            using var response = await guestClient.SendAsync(GuestGetDj(room.Id, firstToken));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        using var otherSessionResponse = await guestClient.SendAsync(GuestGetDj(room.Id, secondToken));

        Assert.Equal(HttpStatusCode.OK, otherSessionResponse.StatusCode);
    }

    #endregion
}
