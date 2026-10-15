using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Accounts.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DjRequests.Api.Modules.Accounts.Endpoints;

public static class AuthEndpoints
{
    private const string DuplicateEmailIndex = "IX_djs_normalized_email";
    private const string DuplicateUserNameIndex = "UserNameIndex";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth");


        group.MapPost("/register", RegisterAsync)
            .RequireRateLimiting("register")
            .AllowAnonymous()
            .WithName("Register")
            .Produces<RegisterResponse>(StatusCodes.Status201Created)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .RequireRateLimiting("login")
            .AllowAnonymous()
            .WithName("Login")
            .Produces<TokenResponse>(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .RequireRateLimiting("refresh")
            .AllowAnonymous()
            .WithName("Refresh")
            .Produces<TokenResponse>(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", MeAsync)
            .RequireAuthorization("ActiveDj")
            .WithName("Me")
            .Produces<MeResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<DjAccount> userManager,
        TimeProvider timeProvider)
    {
        var errors = new Dictionary<string, string[]>();

        string? email = null;
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["Email"] = new[] { "El correo electronico es obligatorio." };
        }
        else
        {
            email = request.Email.Trim();
            if (email.Length > 256)
            {
                errors["Email"] = new[] { "El correo electronico no puede superar los 256 caracteres." };
            }
            else if (!new EmailAddressAttribute().IsValid(email))
            {
                errors["Email"] = new[] { "El correo electronico no tiene un formato valido." };
            }
        }

        string? artistName = null;
        if (string.IsNullOrWhiteSpace(request.ArtistName))
        {
            errors["ArtistName"] = new[] { "El nombre artistico es obligatorio." };
        }
        else
        {
            artistName = request.ArtistName.Trim();
            if (artistName.Length > 100)
            {
                errors["ArtistName"] = new[] { "El nombre artistico no puede superar los 100 caracteres." };
            }
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["Password"] = new[] { "La contrasena es obligatoria." };
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = new DjAccount
        {
            UserName = email!,
            Email = email!,
            NormalizedEmail = email!.ToUpperInvariant(),
            NormalizedUserName = email!.ToUpperInvariant(),
            NombreArtistico = artistName!,
            Estado = "activo",
            CreatedAt = timeProvider.GetUtcNow(),
            UpdatedAt = timeProvider.GetUtcNow()
        };

        IdentityResult result;
        try
        {
            result = await userManager.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException ex) when (IsAccountUniqueViolation(ex))
        {
            return DuplicateEmailConflict();
        }

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)))
            {
                return DuplicateEmailConflict();
            }

            return Results.ValidationProblem(result.Errors.ToDictionary(
                e => e.Code.StartsWith("Password", StringComparison.OrdinalIgnoreCase) ? "Password" : e.Code,
                e => new[] { e.Description }));
        }

        var response = new RegisterResponse(user.Id, user.Email!, user.NombreArtistico, user.CreatedAt);
        return Results.Created("/api/auth/me", response);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        SignInManager<DjAccount> signInManager,
        UserManager<DjAccount> userManager)
    {
        var errors = new Dictionary<string, string[]>();

        string? email = null;
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["Email"] = new[] { "El correo electronico es obligatorio." };
        }
        else
        {
            email = request.Email.Trim();
            if (email.Length > 256)
            {
                errors["Email"] = new[] { "El correo electronico no puede superar los 256 caracteres." };
            }
            else if (!new EmailAddressAttribute().IsValid(email))
            {
                errors["Email"] = new[] { "El correo electronico no tiene un formato valido." };
            }
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["Password"] = new[] { "La contrasena es obligatoria." };
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = await userManager.FindByEmailAsync(email!);

        if (user is null || user.Estado != "activo")
        {
            return Results.Unauthorized();
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return Results.Unauthorized();
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        return TypedResults.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        SignInManager<DjAccount> signInManager,
        IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["RefreshToken"] = new[] { "El token de actualizacion es obligatorio." }
            });
        }

        var options = bearerTokenOptions.Get(IdentityConstants.BearerScheme);

        AuthenticationTicket? ticket;
        try
        {
            ticket = options.RefreshTokenProtector.Unprotect(request.RefreshToken);
        }
        catch (CryptographicException)
        {
            return Results.Unauthorized();
        }

        if (ticket?.Properties?.ExpiresUtc is not DateTimeOffset expiresUtc || expiresUtc <= timeProvider.GetUtcNow())
        {
            return Results.Unauthorized();
        }

        var user = await signInManager.ValidateSecurityStampAsync(ticket.Principal);
        if (user is null || user.Estado != "activo")
        {
            return Results.Unauthorized();
        }

        var newPrincipal = await signInManager.CreateUserPrincipalAsync(user);
        return TypedResults.SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
    }

    private static async Task<IResult> MeAsync(
        ClaimsPrincipal user,
        UserManager<DjAccount> userManager)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var id))
        {
            return Results.Unauthorized();
        }

        var dj = await userManager.FindByIdAsync(id.ToString());
        if (dj is null || dj.Estado != "activo")
        {
            return Results.Forbid();
        }

        return Results.Ok(new MeResponse(dj.Id, dj.Email!, dj.NombreArtistico));
    }

    private static bool IsAccountUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                    && postgresException.ConstraintName is DuplicateEmailIndex or DuplicateUserNameIndex;
            }
        }

        return false;
    }

    private static IResult DuplicateEmailConflict()
    {
        return Results.Problem(
            detail: "Ya existe una cuenta con este correo electronico.",
            statusCode: StatusCodes.Status409Conflict,
            title: "Email ya registrado",
            type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10");
    }
}
