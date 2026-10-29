using DjRequests.Api.Modules.Genres.Dtos;
using DjRequests.Api.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DjRequests.Api.Modules.Genres.Endpoints;

public static class GeneroEndpoints
{
    public static void MapGeneroEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/generos")
            .WithTags("Genres");

        group.MapGet("", ListAsync)
            .WithName("ListGeneros")
            .Produces<IReadOnlyList<GeneroDto>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> ListAsync(
        DjRequestsDbContext context,
        CancellationToken cancellationToken)
    {
        var generos = await context.Generos
            .AsNoTracking()
            .Where(g => g.Activo)
            .OrderBy(g => g.Nombre)
            .Select(g => new GeneroDto(g.Id, g.Nombre))
            .ToListAsync(cancellationToken);

        return Results.Ok(generos);
    }
}
