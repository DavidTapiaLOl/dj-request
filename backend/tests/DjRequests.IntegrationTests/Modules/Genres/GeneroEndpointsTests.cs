using System.Net;
using System.Net.Http.Json;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Genres.Dtos;
using DjRequests.IntegrationTests.Fixtures;
using Xunit;

namespace DjRequests.IntegrationTests.Modules.Genres;

[Collection(IntegrationCollection.Name)]
public class GeneroEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private WebAppFactory _factory = null!;
    private HttpClient _client = null!;

    public GeneroEndpointsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_postgres);
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Get_ReturnsActiveGenresOrderedByName()
    {
        var response = await _client.GetAsync("/api/generos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var generos = await response.Content.ReadFromJsonAsync<List<GeneroDto>>();
        Assert.NotNull(generos);
        Assert.Equal(11, generos.Count);
        Assert.Contains(generos, genero => genero.Nombre == "Pop");
        Assert.Contains(generos, genero => genero.Nombre == "Otro");
        Assert.All(generos, genero => Assert.False(string.IsNullOrWhiteSpace(genero.Nombre)));
        Assert.Equal(
            generos.OrderBy(genero => genero.Nombre, StringComparer.Ordinal).Select(genero => genero.Id),
            generos.Select(genero => genero.Id));
    }

    [Fact]
    public async Task Get_ExcludesInactiveGenres()
    {
        await using (var db = await _factory.CreateDbContextAsync())
        {
            db.Generos.Add(new Genero { Id = 99, Nombre = "Inactivo", Activo = false });
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/generos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var generos = await response.Content.ReadFromJsonAsync<List<GeneroDto>>();
        Assert.NotNull(generos);
        Assert.DoesNotContain(generos, genero => genero.Id == 99);
    }
}
