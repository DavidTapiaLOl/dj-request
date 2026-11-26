using System.Security.Claims;
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
    private const string ConflictType = "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10";

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

        group.MapPatch("/{id:guid}/state", SetStateAsync)
            .WithName("AdminSetDjState")
            .RequireAuthorization(AdminAuthenticationDefaults.Policy)
            .Produces<AdminDjStateResponse>(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
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

    private static async Task<IResult> SetStateAsync(
        Guid id,
        AdminDjStateRequest request,
        AdminDjService service,
        ClaimsPrincipal user,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var estado = request.Estado?.Trim().ToLowerInvariant();
        var expectedEstado = request.ExpectedEstado?.Trim().ToLowerInvariant();

        if (estado is null || !AdminDjService.IsValidEstado(estado))
        {
            errors["estado"] =
            [
                $"Estado inválido. Estados permitidos: {string.Join(", ", AdminDjService.ValidStates)}."
            ];
        }

        if (expectedEstado is null || !AdminDjService.IsValidEstado(expectedEstado))
        {
            errors["expectedEstado"] =
            [
                $"Estado esperado inválido. Estados permitidos: {string.Join(", ", AdminDjService.ValidStates)}."
            ];
        }

        if (errors.Count > 0)
        {
            return ValidationProblem(errors);
        }

        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var administratorId))
        {
            return NoStoreResults.AddNoStore(Results.Unauthorized());
        }

        var result = await service.SetStateAsync(
            id,
            estado!,
            expectedEstado!,
            administratorId,
            timeProvider,
            cancellationToken);

        switch (result.Status)
        {
            case AdminDjStateUpdateStatus.NotFound:
                return NoStoreResults.AddNoStore(Results.Problem(
                    detail: "DJ no encontrado.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "DJ no encontrado",
                    type: NotFoundType));

            case AdminDjStateUpdateStatus.Conflict:
                return NoStoreResults.AddNoStore(Results.Problem(
                    detail: "El estado actual del DJ no coincide con expectedEstado.",
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Conflicto de estado",
                    type: ConflictType));

            default:
                return NoStoreResults.AddNoStore(
                    Results.Ok(new AdminDjStateResponse(result.Id, result.Estado)));
        }
    }

    private static IResult ValidationProblem(IDictionary<string, string[]> errors) =>
        NoStoreResults.AddNoStore(Results.ValidationProblem(errors));

    private static IResult ValidationProblem(string field, string message) =>
        NoStoreResults.AddNoStore(Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = [message]
        }));
}
