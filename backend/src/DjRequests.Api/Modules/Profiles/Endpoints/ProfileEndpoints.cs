using System.Security.Claims;
using DjRequests.Api.Modules.Profiles.Dtos;
using DjRequests.Api.Modules.Profiles.Services;
using DjRequests.Api.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DjRequests.Api.Modules.Profiles.Endpoints;

public static class ProfileEndpoints
{
    private const string PhotoNotFoundType = "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5";

    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dj/profile")
            .WithTags("Profiles")
            .RequireAuthorization("ActiveDj");

        group.MapGet("", GetMineAsync)
            .WithName("GetMyProfile")
            .Produces<ProfileDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapPatch("", UpdateAsync)
            .WithName("UpdateMyProfile")
            .Produces<ProfileDto>(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapPost("/photo", UploadPhotoAsync)
            .DisableAntiforgery()
            .WithName("UploadMyProfilePhoto")
            .Produces<ProfileDto>(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapDelete("/photo", DeletePhotoAsync)
            .WithName("DeleteMyProfilePhoto")
            .Produces<ProfileDto>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        app.MapGet("/api/profile-photos/{fileName}", GetPhotoAsync)
            .WithName("GetProfilePhoto")
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
    }

    private static Guid GetDjId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    private static async Task<IResult> GetMineAsync(
        ClaimsPrincipal user,
        IProfileService profiles,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await profiles.GetMineAsync(GetDjId(user), cancellationToken));
        }
        catch (ProfileNotFoundException)
        {
            return ProfileNotFound();
        }
    }

    private static async Task<IResult> UpdateAsync(
        UpdateProfileRequest request,
        ClaimsPrincipal user,
        IProfileService profiles,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await profiles.UpdateAsync(GetDjId(user), request, cancellationToken));
        }
        catch (ProfileValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>(ex.Errors));
        }
        catch (ProfileNotFoundException)
        {
            return ProfileNotFound();
        }
    }

    private static async Task<IResult> UploadPhotoAsync(
        HttpRequest request,
        ClaimsPrincipal user,
        IProfileService profiles,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return FileValidationProblem("Selecciona una imagen para subir.");
        }

        IFormFile? file;
        try
        {
            var form = await request.ReadFormAsync(cancellationToken);
            file = form.Files.GetFile("file");
        }
        catch (InvalidDataException)
        {
            return FileValidationProblem("No se pudo leer el archivo enviado.");
        }
        catch (BadHttpRequestException)
        {
            return FileValidationProblem("No se pudo leer el archivo enviado.");
        }

        if (file is null)
        {
            return FileValidationProblem("Selecciona una imagen para subir.");
        }

        try
        {
            return Results.Ok(await profiles.SetPhotoAsync(GetDjId(user), file, cancellationToken));
        }
        catch (ProfileValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>(ex.Errors));
        }
        catch (ProfileNotFoundException)
        {
            return ProfileNotFound();
        }
    }

    private static async Task<IResult> DeletePhotoAsync(
        ClaimsPrincipal user,
        IProfileService profiles,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await profiles.DeletePhotoAsync(GetDjId(user), cancellationToken));
        }
        catch (ProfileNotFoundException)
        {
            return ProfileNotFound();
        }
    }

    private static async Task<IResult> GetPhotoAsync(
        string fileName,
        HttpContext httpContext,
        IFileStorage storage,
        DjRequestsDbContext context,
        CancellationToken cancellationToken)
    {
        if (!storage.TryGetPhysicalPath(fileName, out var physicalPath)
            || !await context.PerfilesDj.AsNoTracking()
                .AnyAsync(p => p.FotoNombreArchivo == fileName, cancellationToken)
            || !File.Exists(physicalPath))
        {
            return PhotoNotFound();
        }

        var contentType = physicalPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/jpeg";

        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers.XContentTypeOptions = "nosniff";

        try
        {
            var bytes = await File.ReadAllBytesAsync(physicalPath, cancellationToken);
            return Results.File(bytes, contentType);
        }
        catch (FileNotFoundException)
        {
            return PhotoNotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return PhotoNotFound();
        }
    }

    private static IResult FileValidationProblem(string message)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["File"] = [message]
        });
    }

    private static IResult ProfileNotFound()
    {
        return Results.Problem(
            detail: "Perfil no encontrado.",
            statusCode: StatusCodes.Status404NotFound,
            title: "Perfil no encontrado",
            type: PhotoNotFoundType);
    }

    private static IResult PhotoNotFound()
    {
        return Results.Problem(
            detail: "Foto no encontrada.",
            statusCode: StatusCodes.Status404NotFound,
            title: "Foto no encontrada",
            type: PhotoNotFoundType);
    }
}
