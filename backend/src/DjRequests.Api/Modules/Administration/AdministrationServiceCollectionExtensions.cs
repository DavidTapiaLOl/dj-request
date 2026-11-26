using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Administration.Authentication;
using DjRequests.Api.Modules.Administration.Options;
using DjRequests.Api.Modules.Administration.Policies;
using DjRequests.Api.Modules.Administration.Provisioning;
using DjRequests.Api.Modules.Administration.Services;
using DjRequests.Api.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DjRequests.Api.Modules.Administration;

public static class AdministrationServiceCollectionExtensions
{
    public static IServiceCollection AddAdministration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AdminDbContext>(options =>
            AdminDbContext.UseAdminStore(options, configuration.GetConnectionString("DefaultConnection")));

        services.AddDataProtection();
        services.AddIdentityCore<AdminAccount>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<AdminDbContext>()
            .AddSignInManager();

        services.AddAuthentication()
            .AddBearerToken(AdminAuthenticationDefaults.BearerScheme, options =>
            {
                options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
                options.RefreshTokenExpiration = TimeSpan.FromHours(8);
            });

        services.AddOptions<BearerTokenOptions>(AdminAuthenticationDefaults.BearerScheme)
            .Configure<TimeProvider>((options, timeProvider) => options.TimeProvider = timeProvider);

        services.AddOptions<BearerTokenOptions>(AdminAuthenticationDefaults.BearerScheme)
            .Configure<IDataProtectionProvider>((options, dataProtectionProvider) =>
            {
                options.BearerTokenProtector = new TicketDataFormat(
                    dataProtectionProvider.CreateProtector(
                        AdminAuthenticationDefaults.AccessTokenProtectorPurpose));
                options.RefreshTokenProtector = new TicketDataFormat(
                    dataProtectionProvider.CreateProtector(
                        AdminAuthenticationDefaults.RefreshTokenProtectorPurpose));
            });

        services.AddAuthorization(options =>
            options.AddPolicy(
                AdminAuthenticationDefaults.Policy,
                policy => policy
                    .AddAuthenticationSchemes(AdminAuthenticationDefaults.BearerScheme)
                    .RequireAuthenticatedUser()
                    .AddRequirements(new ActiveAdminRequirement())));

        services.AddScoped<IAuthorizationHandler, ActiveAdminHandler>();
        services.AddScoped<AdminProvisioningService>();
        services.AddScoped<AdminDjService>();
        services.Configure<AdminAuthRateLimitOptions>(
            configuration.GetSection("AdminAuthRateLimits"));

        return services;
    }
}
