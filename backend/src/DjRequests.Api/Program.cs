using System.Globalization;
using System.Threading.RateLimiting;
using DjRequests.Api.Entities;
using DjRequests.Api.Modules.Accounts.Endpoints;
using DjRequests.Api.Modules.Accounts.Options;
using DjRequests.Api.Modules.Accounts.Policies;
using DjRequests.Api.Modules.Health;
using DjRequests.Api.Modules.Rooms.Options;
using DjRequests.Api.Modules.Rooms.Services;
using DjRequests.Api.Modules.Rooms.Endpoints;
using DjRequests.Api.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDbContext<DjRequestsDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddProblemDetails();

builder.Services.AddIdentity<DjAccount, IdentityRole<Guid>>(options =>
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
    .AddEntityFrameworkStores<DjRequestsDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.BearerScheme;
    options.DefaultChallengeScheme = IdentityConstants.BearerScheme;
    options.DefaultForbidScheme = IdentityConstants.BearerScheme;
})
    .AddBearerToken(IdentityConstants.BearerScheme, options =>
    {
        options.BearerTokenExpiration = TimeSpan.FromMinutes(15);
        options.RefreshTokenExpiration = TimeSpan.FromDays(7);
    });

builder.Services.AddOptions<BearerTokenOptions>(IdentityConstants.BearerScheme)
    .Configure<TimeProvider>((options, timeProvider) => options.TimeProvider = timeProvider);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ActiveDj", policy =>
        policy.AddRequirements(new ActiveDjRequirement()));
});

builder.Services.AddScoped<IAuthorizationHandler, ActiveDjHandler>();

builder.Services.Configure<AuthRateLimitOptions>(
    builder.Configuration.GetSection("AuthRateLimits"));

builder.Services.AddOptions<FrontendUrlOptions>()
    .Bind(builder.Configuration.GetSection(FrontendUrlOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<FrontendUrlOptions>, FrontendUrlOptionsValidator>();

builder.Services.AddRateLimiter(options =>
{
    var authOptions = builder.Configuration.GetSection("AuthRateLimits").Get<AuthRateLimitOptions>()
        ?? new AuthRateLimitOptions();

    var window = TimeSpan.FromMinutes(authOptions.WindowMinutes);

    static Func<HttpContext, RateLimitPartition<string>> PartitionByClientIp(int permitLimit, TimeSpan window)
    {
        return context =>
        {
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
        };
    }

    options.AddPolicy("register", PartitionByClientIp(authOptions.RegisterPermitLimit, window));
    options.AddPolicy("login", PartitionByClientIp(authOptions.LoginPermitLimit, window));
    options.AddPolicy("refresh", PartitionByClientIp(authOptions.RefreshPermitLimit, window));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }

        var problemDetailsService = context.HttpContext.RequestServices.GetService<IProblemDetailsService>();
        if (problemDetailsService is not null)
        {
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context.HttpContext,
                ProblemDetails = new ProblemDetails
                {
                    Title = "Demasiadas solicitudes",
                    Detail = "Se ha excedido el limite de solicitudes para esta direccion IP. Intente de nuevo mas tarde.",
                    Status = StatusCodes.Status429TooManyRequests,
                    Type = "https://tools.ietf.org/html/rfc6585#section-4"
                }
            });
        }
    };
});

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IRoomCodeGenerator, RoomCodeGenerator>();
builder.Services.Configure<TimeZoneValidatorOptions>(
    config => config.DefaultTimezone = "America/Mexico_City");

// Verificar que la zona horaria predeterminada este disponible en el sistema
try
{
    TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
}
catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
{
    throw new InvalidOperationException(
        "La zona horaria predeterminada 'America/Mexico_City' no esta disponible en el sistema.", ex);
}

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseRateLimiter();

app.Use(async (context, next) =>
{
    await next();

    var statusCode = context.Response.StatusCode;
    if (statusCode is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden))
    {
        return;
    }

    if (context.Response.HasStarted
        || context.Response.ContentType is not null
        || context.Response.ContentLength > 0)
    {
        return;
    }

    context.Response.ContentLength = null;

    var problemDetailsService = context.RequestServices.GetService<IProblemDetailsService>();
    if (problemDetailsService is null)
    {
        return;
    }

    await problemDetailsService.WriteAsync(new ProblemDetailsContext
    {
        HttpContext = context,
        ProblemDetails = new ProblemDetails
        {
            Title = statusCode == StatusCodes.Status401Unauthorized ? "No autorizado" : "Prohibido",
            Detail = statusCode == StatusCodes.Status401Unauthorized
                ? "La solicitud no supero la autenticacion."
                : "La solicitud no tiene permisos para acceder al recurso.",
            Status = statusCode,
            Type = statusCode == StatusCodes.Status401Unauthorized
                ? "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.2"
                : "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.4"
        }
    });
});

app.UseAuthentication();
app.UseAuthorization();

// Map health endpoints
app.MapHealthEndpoints();

// Map auth endpoints
app.MapAuthEndpoints();

// Register rooms endpoints
app.MapRoomEndpoints();

app.Run();
public partial class Program { }
