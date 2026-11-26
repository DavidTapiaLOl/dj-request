using DjRequests.Api.Modules.Administration.Authentication;
using DjRequests.Api.Modules.Administration.Dtos;
using DjRequests.Api.Modules.Administration.Services;
using DjRequests.Api.Modules.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DjRequests.Api.Modules.Administration.Endpoints;

public static class AdminDjEndpoints
{
    private const string NotFoundType = "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5";

    public static void MapAdminDjEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/djs")
            .WithTags("AdminDjs")
            .RequireAuthorization(AdminAuthenticationDefaults.Policy);

        group.MapGet("", ListAsync)
            .WithName("AdminListDjs")
            .Produces<AdminDjPageResponse>(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("AdminGetDj")
            .Produces<AdminDjDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListAsync(
        AdminDjService service,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? estado = null)
    {
        if (!PaginationValidation.TryValidate(page, pageSize, out var paginationError))
        {
            return ValidationProblem("page", paginationError);
        }

        string? normalizedSearch = null;
        if (!string.IsNullOrWhiteSpace(search))
        {
            normalizedSearch = search.Trim();
            if (normalizedSearch.Length > AdminDjService.MaxSearchLength)
            {
                return ValidationProblem(
                    "search",
                    $"La búsqueda no puede superar los {AdminDjService.MaxSearchLength} caracteres.");
            }
        }

        string? normalizedEstado = null;
        if (!string.IsNullOrWhiteSpace(estado))
        {
            normalizedEstado = estado.Trim().ToLowerInvariant();
            if (!AdminDjService.IsValidEstado(normalizedEstado))
            {
                return ValidationProblem(
                    "estado",
                    $"Estado inválido. Estados permitidos: {string.Join(", ", AdminDjService.ValidStates)}.");
            }
        }

        var pageResponse = await service.ListAsync(
            normalizedSearch,
            normalizedEstado,
            page,
            pageSize,
            cancellationToken);

        return NoStoreResults.AddNoStore(Results.Ok(pageResponse));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        AdminDjService service,
        CancellationToken cancellationToken)
    {
        var dj = await service.GetAsync(id, cancellationToken);

        if (dj is null)
        {
            return NoStoreResults.AddNoStore(Results.Problem(
                detail: "DJ no encontrado.",
                statusCode: StatusCodes.Status404NotFound,
                title: "DJ no encontrado",
                type: NotFoundType));
        }

        return NoStoreResults.AddNoStore(Results.Ok(dj));
    }

    private static IResult ValidationProblem(string field, string message) =>
        NoStoreResults.AddNoStore(Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = [message]
        }));
}
