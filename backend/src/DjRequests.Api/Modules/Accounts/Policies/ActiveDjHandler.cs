using System.Security.Claims;
using DjRequests.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DjRequests.Api.Modules.Accounts.Policies;

public sealed class ActiveDjHandler : AuthorizationHandler<ActiveDjRequirement>
{
    private readonly UserManager<DjAccount> _userManager;
    private readonly SignInManager<DjAccount> _signInManager;
    private readonly IdentityOptions _identityOptions;

    public ActiveDjHandler(
        UserManager<DjAccount> userManager,
        SignInManager<DjAccount> signInManager,
        IOptions<IdentityOptions> identityOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _identityOptions = identityOptions.Value;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveDjRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var djId))
        {
            return;
        }

        var dj = await _userManager.FindByIdAsync(djId.ToString());
        if (dj is null || dj.Estado != "activo")
        {
            return;
        }

        var securityStampClaimType = _identityOptions.ClaimsIdentity.SecurityStampClaimType;
        var securityStamp = context.User.FindFirstValue(securityStampClaimType);
        if (!await _signInManager.ValidateSecurityStampAsync(dj, securityStamp))
        {
            return;
        }

        context.Succeed(requirement);
    }
}
