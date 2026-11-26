using System.Security.Claims;
using DjRequests.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DjRequests.Api.Modules.Administration.Policies;

public sealed class ActiveAdminHandler : AuthorizationHandler<ActiveAdminRequirement>
{
    private readonly UserManager<AdminAccount> _userManager;
    private readonly SignInManager<AdminAccount> _signInManager;
    private readonly IdentityOptions _identityOptions;

    public ActiveAdminHandler(
        UserManager<AdminAccount> userManager,
        SignInManager<AdminAccount> signInManager,
        IOptions<IdentityOptions> identityOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _identityOptions = identityOptions.Value;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveAdminRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var adminId))
        {
            return;
        }

        var admin = await _userManager.FindByIdAsync(adminId.ToString());
        if (admin is null || !admin.IsActive)
        {
            return;
        }

        var securityStampClaimType = _identityOptions.ClaimsIdentity.SecurityStampClaimType;
        var securityStamp = context.User.FindFirstValue(securityStampClaimType);
        if (!await _signInManager.ValidateSecurityStampAsync(admin, securityStamp))
        {
            return;
        }

        context.Succeed(requirement);
    }
}
