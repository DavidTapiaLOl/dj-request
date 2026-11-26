using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Administration.Authentication;
using DjRequests.Api.Modules.Administration.Dtos;
using DjRequests.Api.Modules.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DjRequests.Api.Modules.Administration.Endpoints;

public static class AdminAuthEndpoints
{
    public static void MapAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/auth")
            .WithTags("AdminAuth");

        group.MapPost("/login", LoginAsync)
            .RequireRateLimiting(AdminAuthenticationDefaults.LoginRateLimitPolicy)
            .AllowAnonymous()
            .WithName("AdminLogin")
            .Produces(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .RequireRateLimiting(AdminAuthenticationDefaults.RefreshRateLimitPolicy)
            .AllowAnonymous()
            .WithName("AdminRefresh")
            .Produces(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", MeAsync)
            .RequireAuthorization(AdminAuthenticationDefaults.Policy)
            .WithName("AdminMe")
            .Produces<AdminMeResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> LoginAsync(
        AdminLoginRequest request,
        SignInManager<AdminAccount> signInManager,
        UserManager<AdminAccount> userManager)
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
            return NoStoreResults.AddNoStore(Results.ValidationProblem(errors));
        }

        var admin = await userManager.FindByEmailAsync(email!);
        if (admin is null || !admin.IsActive)
        {
            return NoStoreResults.AddNoStore(Results.Unauthorized());
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            admin,
            request.Password,
            lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return NoStoreResults.AddNoStore(Results.Unauthorized());
        }

        var principal = await signInManager.CreateUserPrincipalAsync(admin);
        return NoStoreResults.AddNoStore(
            TypedResults.SignIn(principal, authenticationScheme: AdminAuthenticationDefaults.BearerScheme));
    }

    private static async Task<IResult> RefreshAsync(
        AdminRefreshRequest request,
        SignInManager<AdminAccount> signInManager,
        IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
        TimeProvider timeProvider)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return NoStoreResults.AddNoStore(Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["RefreshToken"] = new[] { "El token de actualizacion es obligatorio." }
            }));
        }

        var options = bearerTokenOptions.Get(AdminAuthenticationDefaults.BearerScheme);

        AuthenticationTicket? ticket;
        try
        {
            ticket = options.RefreshTokenProtector.Unprotect(request.RefreshToken);
        }
        catch (CryptographicException)
        {
            return NoStoreResults.AddNoStore(Results.Unauthorized());
        }

        if (ticket?.Properties?.ExpiresUtc is not DateTimeOffset expiresUtc ||
            expiresUtc <= timeProvider.GetUtcNow())
        {
            return NoStoreResults.AddNoStore(Results.Unauthorized());
        }

        var admin = await signInManager.ValidateSecurityStampAsync(ticket.Principal);
        if (admin is null || !admin.IsActive)
        {
            return NoStoreResults.AddNoStore(Results.Unauthorized());
        }

        var principal = await signInManager.CreateUserPrincipalAsync(admin);
        return NoStoreResults.AddNoStore(
            TypedResults.SignIn(principal, authenticationScheme: AdminAuthenticationDefaults.BearerScheme));
    }

    private static async Task<IResult> MeAsync(
        ClaimsPrincipal user,
        UserManager<AdminAccount> userManager)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var id))
        {
            return NoStoreResults.AddNoStore(Results.Unauthorized());
        }

        var admin = await userManager.FindByIdAsync(id.ToString());
        if (admin is null || !admin.IsActive)
        {
            return NoStoreResults.AddNoStore(Results.Forbid());
        }

        return NoStoreResults.AddNoStore(
            Results.Ok(new AdminMeResponse(admin.Id, admin.Name, admin.Email!)));
    }
}
