using DjRequests.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace DjRequests.Api.Modules.Accounts.Policies;

public sealed class ActiveDjHandler(UserManager<DjAccount> userManager) 
    : AuthorizationHandler<ActiveDjRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, 
        ActiveDjRequirement requirement)
    {
        var userId = context.User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userId, out var id))
        {
            var dj = await userManager.FindByIdAsync(id.ToString());
            if (dj is { Estado: "activo" })
            {
                context.Succeed(requirement);
            }
        }
    }
}
