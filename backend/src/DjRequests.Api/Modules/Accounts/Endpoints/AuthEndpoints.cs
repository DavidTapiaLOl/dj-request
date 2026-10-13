using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Accounts.Dtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DjRequests.Api.Modules.Accounts.Endpoints;

public static class AuthEndpoints
{
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
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .RequireRateLimiting("refresh")
            .AllowAnonymous()
            .WithName("Refresh")
            .Produces<TokenResponse>(StatusCodes.Status200OK)
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
        var email = request.Email.Trim();
        var artistName = request.ArtistName.Trim();

        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(email))
        {
            errors["Email"] = new[] { "El correo electronico es obligatorio." };
        }
        else if (!new EmailAddressAttribute().IsValid(email))
        {
            errors["Email"] = new[] { "El correo electronico no tiene un formato valido." };
        }

        if (string.IsNullOrWhiteSpace(artistName))
        {
            errors["ArtistName"] = new[] { "El nombre artistico es obligatorio." };
        }
        else if (artistName.Length > 100)
        {
            errors["ArtistName"] = new[] { "El nombre artistico no puede superar los 100 caracteres." };
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = new DjAccount
        {
            UserName = email,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            NombreArtistico = artistName,
            Estado = "activo",
            CreatedAt = timeProvider.GetUtcNow(),
            UpdatedAt = timeProvider.GetUtcNow()
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)))
            {
                return Results.Conflict(new ProblemDetails
                {
                    Title = "Email ya registrado",
                    Status = StatusCodes.Status409Conflict,
                    Detail = "Ya existe una cuenta con este correo electrónico."
                });
            }
            return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
        }

        var response = new RegisterResponse(user.Id, user.Email!, user.NombreArtistico, user.CreatedAt);
        return Results.Created("/api/auth/me", response);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        SignInManager<DjAccount> signInManager,
        UserManager<DjAccount> userManager)
    {
        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);

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
        HttpContext http,
        UserManager<DjAccount> userManager,
        SignInManager<DjAccount> signInManager,
        IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
        TimeProvider timeProvider)
    {
        var options = bearerTokenOptions.Get(IdentityConstants.BearerScheme);
        var ticket = options.RefreshTokenProtector.Unprotect(request.RefreshToken);

        if (ticket?.Properties?.ExpiresUtc is not DateTimeOffset expiresUtc || expiresUtc < timeProvider.GetUtcNow())
        {
            return Results.Unauthorized();
        }

        var userId = ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var id))
        {
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || user.Estado != "activo")
        {
            return Results.Unauthorized();
        }

        var securityStamp = ticket.Principal.FindFirstValue(new ClaimsIdentityOptions().SecurityStampClaimType);
        if (securityStamp is not null && user.SecurityStamp != securityStamp)
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
}
