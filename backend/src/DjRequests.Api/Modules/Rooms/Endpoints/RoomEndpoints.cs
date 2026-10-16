using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DjRequests.Api.Modules.Rooms.Dtos;
using DjRequests.Api.Modules.Rooms.Options;
using DjRequests.Api.Modules.Rooms.Services;
using DjRequests.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DjRequests.Api.Modules.Rooms.Endpoints;

public static class RoomEndpoints
{
    private const int MaxCodeAttempts = 5;
    private const double RetentionHours = 168.0;
    private const string RoomCodeUniqueIndexName = "IX_salas_codigo";
    private const string PostgresUniqueViolationSqlState = "23505";

    private static readonly string[] StrictIsoDateTimeFormats =
    {
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.fffK",
        "yyyy-MM-dd'T'HH:mm:ss.ffffffK",
        "yyyy-MM-dd'T'HH:mm:ss.fffffffK"
    };

    public static void MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rooms")
            .WithTags("Rooms")
            .RequireAuthorization("ActiveDj");

        group.MapPost("", CreateAsync)
            .WithName("CreateRoom")
            .Produces<RoomResponse>(StatusCodes.Status201Created)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapGet("", ListAsync)
            .WithName("ListRooms")
            .Produces<List<RoomResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("GetRoom")
            .Produces<RoomResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateRoom")
            .Produces<RoomResponse>(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/state", UpdateStateAsync)
            .WithName("UpdateRoomState")
            .Produces<RoomResponse>(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static Guid GetDjId(ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userId!);
    }

    private static bool HasExplicitUtcOffset(string value)
    {
        if (value.EndsWith('Z') || value.EndsWith('z'))
        {
            return true;
        }

        // ±HH:mm (6 caracteres finales)
        if (value.Length >= 6
            && (value[^6] == '+' || value[^6] == '-')
            && value[^3] == ':'
            && char.IsDigit(value[^5]) && char.IsDigit(value[^4])
            && char.IsDigit(value[^2]) && char.IsDigit(value[^1]))
        {
            return true;
        }

        // ±HHmm (5 caracteres finales)
        if (value.Length >= 5
            && (value[^5] == '+' || value[^5] == '-')
            && char.IsDigit(value[^4]) && char.IsDigit(value[^3])
            && char.IsDigit(value[^2]) && char.IsDigit(value[^1]))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseDateTimeOffsetWithOffset(
        string? input,
        string fieldName,
        Dictionary<string, string[]> errors,
        out DateTimeOffset utcValue)
    {
        utcValue = default;
        var camelField = char.ToLowerInvariant(fieldName[0]) + fieldName[1..];

        if (string.IsNullOrWhiteSpace(input))
        {
            errors[fieldName] = new[] { $"{camelField} es obligatoria." };
            return false;
        }

        var trimmed = input.Trim();

        if (!HasExplicitUtcOffset(trimmed))
        {
            errors[fieldName] = new[]
            {
                $"{camelField} debe incluir offset explícito (Z o ±HH:mm). Ej: 2026-11-13T21:00:00-06:00."
            };
            return false;
        }

        // Normalizar ±HHmm (sin dos puntos) a ±HH:mm para TryParseExact
        if (trimmed.Length >= 5
            && (trimmed[^5] == '+' || trimmed[^5] == '-')
            && char.IsDigit(trimmed[^4]) && char.IsDigit(trimmed[^3])
            && char.IsDigit(trimmed[^2]) && char.IsDigit(trimmed[^1]))
        {
            trimmed = $"{trimmed[..^2]}:{trimmed[^2..]}";
        }

        if (!DateTimeOffset.TryParseExact(
                trimmed,
                StrictIsoDateTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            errors[fieldName] = new[]
            {
                $"{camelField} tiene un formato inválido. Use ISO 8601 con offset explícito (Z o ±HH:mm)."
            };
            return false;
        }

        utcValue = parsed.ToUniversalTime();
        return true;
    }

    #region CREATE

    private static async Task<IResult> CreateAsync(
        CreateRoomRequest request,
        IRoomCodeGenerator codeGenerator,
        DjRequestsDbContext context,
        ClaimsPrincipal user,
        TimeProvider timeProvider,
        IOptions<FrontendUrlOptions> frontendOptions)
    {
        var djId = GetDjId(user);
        var now = timeProvider.GetUtcNow();

        var errors = new Dictionary<string, string[]>();

        // Nombre
        string? name = null;
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["Name"] = new[] { "El nombre es obligatorio." };
        }
        else
        {
            name = request.Name.Trim();
            if (name.Length == 0)
            {
                errors["Name"] = new[] { "El nombre no puede ser solo espacios." };
            }
            else if (name.Length > 120)
            {
                errors["Name"] = new[] { "El nombre no puede superar los 120 caracteres." };
            }
        }

        // Fechas: offset explícito obligatorio (Z o ±HH:mm)
        DateTimeOffset startsAt = default;
        DateTimeOffset endsAt = default;
        var startsOk = TryParseDateTimeOffsetWithOffset(request.StartsAt, "StartsAt", errors, out startsAt);
        var endsOk = TryParseDateTimeOffsetWithOffset(request.EndsAt, "EndsAt", errors, out endsAt);

        if (startsOk && endsOk)
        {
            if (endsAt <= startsAt)
            {
                errors["EndsAt"] = new[] { "endsAt debe ser mayor que startsAt." };
            }
            else if (endsAt <= now)
            {
                errors["EndsAt"] = new[] { "endsAt debe ser posterior al momento actual." };
            }
            else if (endsAt > DateTimeOffset.MaxValue - TimeSpan.FromHours(RetentionHours))
            {
                errors["EndsAt"] = new[] { "endsAt es demasiado lejano: provocaria desbordamiento en purgeAt." };
            }
        }

        // Zona horaria: omitida -> default; vacia o invalida -> 400
        string timezone;
        if (request.Timezone is null)
        {
            timezone = "America/Mexico_City";
        }
        else if (string.IsNullOrWhiteSpace(request.Timezone))
        {
            errors["Timezone"] = new[] { "La zona horaria no puede estar vacia. Use un identificador IANA como 'America/Mexico_City'." };
            timezone = string.Empty;
        }
        else if (request.Timezone.Length > 64)
        {
            errors["Timezone"] = new[] { "La zona horaria no puede superar los 64 caracteres." };
            timezone = string.Empty;
        }
        else if (!TimeZoneValidator.TryValidateAndResolve(request.Timezone, out var tzInfo, out var tzError))
        {
            errors["Timezone"] = new[] { tzError! };
            timezone = string.Empty;
        }
        else
        {
            timezone = tzInfo!.Id;
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // PurgeAt = EndsAt + 168 horas exactas (sin desbordamiento: ya validado arriba)
        var purgeAt = endsAt.AddHours(RetentionHours);

        // Generar codigo unico con reintento sobre colision reales del indice unique
        Entities.Sala? sala = null;
        for (int attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var candidate = codeGenerator.GenerateCode();
            var entity = new Entities.Sala
            {
                Id = Guid.NewGuid(),
                DjId = djId,
                Nombre = name!,
                Codigo = candidate,
                Estado = "abierta",
                StartsAt = startsAt,
                EndsAt = endsAt,
                Timezone = timezone,
                PurgeAt = purgeAt,
                MaxSolicitudesSesion = 10,
                MaxPlaylistsSesion = 2,
                IntervaloEnvioSegundos = 30,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            context.Salas.Add(entity);
            try
            {
                await context.SaveChangesAsync();
                sala = entity;
                break;
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is PostgresException pgEx
                && pgEx.SqlState == PostgresUniqueViolationSqlState
                && pgEx.ConstraintName == RoomCodeUniqueIndexName)
            {
                context.Entry(entity).State = EntityState.Detached;
            }
        }

        if (sala is null)
        {
            return Results.Problem(
                detail: $"No se pudo generar un codigo unico despues de {MaxCodeAttempts} intentos.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Codigo no disponible",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10");
        }

        var response = BuildRoomResponse(sala, now, frontendOptions.Value);
        return Results.Created($"/api/rooms/{sala.Id}", response);
    }

    #endregion

    #region LIST

    private static async Task<IResult> ListAsync(
        DjRequestsDbContext context,
        ClaimsPrincipal user,
        TimeProvider timeProvider,
        IOptions<FrontendUrlOptions> frontendOptions,
        int page = 1,
        int pageSize = 20)
    {
        if (page < 1)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["page"] = new[] { "page debe ser mayor o igual a 1." }
            });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["pageSize"] = new[] { "pageSize debe estar entre 1 y 100." }
            });
        }

        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["page"] = new[] { "page fuera de rango." }
            });
        }

        var djId = GetDjId(user);
        var now = timeProvider.GetUtcNow();

        var salas = await context.Salas
            .Where(s => s.DjId == djId && s.PurgeAt > now)
            .OrderByDescending(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = salas.Select(s => BuildRoomResponse(s, now, frontendOptions.Value)).ToList();
        return Results.Ok(result);
    }

    #endregion

    #region GET DETAIL

    private static async Task<IResult> GetAsync(
        Guid id,
        DjRequestsDbContext context,
        ClaimsPrincipal user,
        TimeProvider timeProvider,
        IOptions<FrontendUrlOptions> frontendOptions)
    {
        var djId = GetDjId(user);
        var now = timeProvider.GetUtcNow();

        var sala = await context.Salas
            .SingleOrDefaultAsync(s => s.Id == id && s.DjId == djId && s.PurgeAt > now);

        if (sala is null)
        {
            return Results.Problem(
                detail: "Sala no encontrada, ajena o caducada.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Sala no encontrada",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5");
        }

        return Results.Ok(BuildRoomResponse(sala, now, frontendOptions.Value));
    }

    #endregion

    #region UPDATE

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateRoomRequest request,
        DjRequestsDbContext context,
        ClaimsPrincipal user,
        TimeProvider timeProvider,
        IOptions<FrontendUrlOptions> frontendOptions)
    {
        var djId = GetDjId(user);
        var now = timeProvider.GetUtcNow();

        var errors = new Dictionary<string, string[]>();

        // Validar version (omitida -> 0 -> 400)
        if (request.Version <= 0)
        {
            errors["Version"] = new[] { "version debe ser un entero positivo." };
        }

        // Validar nombre
        string? name = null;
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors["Name"] = new[] { "El nombre es obligatorio." };
        }
        else
        {
            name = request.Name.Trim();
            if (name.Length == 0)
            {
                errors["Name"] = new[] { "El nombre no puede ser solo espacios." };
            }
            else if (name.Length > 120)
            {
                errors["Name"] = new[] { "El nombre no puede superar los 120 caracteres." };
            }
        }

        // Validar fechas: offset explícito obligatorio (Z o ±HH:mm)
        DateTimeOffset startsAt = default;
        DateTimeOffset endsAt = default;
        var startsOk = TryParseDateTimeOffsetWithOffset(request.StartsAt, "StartsAt", errors, out startsAt);
        var endsOk = TryParseDateTimeOffsetWithOffset(request.EndsAt, "EndsAt", errors, out endsAt);

        if (startsOk && endsOk)
        {
            if (endsAt <= startsAt)
            {
                errors["EndsAt"] = new[] { "endsAt debe ser mayor que startsAt." };
            }
            else if (endsAt <= now)
            {
                errors["EndsAt"] = new[] { "No se puede reprogramar un evento que ya termino." };
            }
            else if (endsAt > DateTimeOffset.MaxValue - TimeSpan.FromHours(RetentionHours))
            {
                errors["EndsAt"] = new[] { "endsAt es demasiado lejano: provocaria desbordamiento en purgeAt." };
            }
        }

        // Validar zona horaria: omitida -> default; vacia o invalida -> 400
        string timezone;
        if (request.Timezone is null)
        {
            timezone = "America/Mexico_City";
        }
        else if (string.IsNullOrWhiteSpace(request.Timezone))
        {
            errors["Timezone"] = new[] { "La zona horaria no puede estar vacia. Use un identificador IANA como 'America/Mexico_City'." };
            timezone = string.Empty;
        }
        else if (request.Timezone.Length > 64)
        {
            errors["Timezone"] = new[] { "La zona horaria no puede superar los 64 caracteres." };
            timezone = string.Empty;
        }
        else if (!TimeZoneValidator.TryValidateAndResolve(request.Timezone, out var tzInfo, out var tzError))
        {
            errors["Timezone"] = new[] { tzError! };
            timezone = string.Empty;
        }
        else
        {
            timezone = tzInfo!.Id;
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // PurgeAt = EndsAt + 168 horas exactas (sin desbordamiento: ya validado arriba)
        var purgeAt = endsAt.AddHours(RetentionHours);

        // Transaccion con bloqueo de fila FOR UPDATE
        await using var transaction = await context.Database.BeginTransactionAsync();

        // Bloquear la fila de sala (orden consistente sala -> sesion en futuro)
        var lockedSalas = await context.Salas
            .FromSqlRaw("SELECT * FROM salas WHERE id = {0} FOR UPDATE", id)
            .ToListAsync();

        var sala = lockedSalas.SingleOrDefault();

        // Hora actual POST-bloqueo: unica fuente de verdad para todas las revalidaciones
        now = timeProvider.GetUtcNow();

        if (sala is null || sala.DjId != djId || sala.PurgeAt <= now)
        {
            await transaction.RollbackAsync();
            return Results.Problem(
                detail: "Sala no encontrada, ajena o caducada.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Sala no encontrada",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5");
        }

        if (sala.EndsAt <= now)
        {
            await transaction.RollbackAsync();
            return Results.Problem(
                detail: "No se puede revivir o reprogramar un evento que ya termino.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Evento finalizado",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10");
        }

        if (sala.Version != request.Version)
        {
            await transaction.RollbackAsync();
            return Results.Problem(
                detail: "La version de la sala no coincide con la version actual.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Version obsoleta",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10");
        }

        // Revalidar fechas bajo bloqueo con hora post-lock
        if (endsAt <= now)
        {
            await transaction.RollbackAsync();
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["EndsAt"] = new[] { "endsAt debe ser posterior al momento actual." }
            });
        }

        if (endsAt <= startsAt)
        {
            await transaction.RollbackAsync();
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["EndsAt"] = new[] { "endsAt debe ser mayor que startsAt." }
            });
        }

        // Aplicar cambios
        sala.Nombre = name!;
        sala.StartsAt = startsAt;
        sala.EndsAt = endsAt;
        sala.Timezone = timezone;
        sala.PurgeAt = purgeAt;
        sala.Version = request.Version + 1;
        sala.UpdatedAt = now;

        try
        {
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Results.Problem(
                detail: "La version de la sala no coincide con la version actual.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Version obsoleta",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10");
        }

        return Results.Ok(BuildRoomResponse(sala, timeProvider.GetUtcNow(), frontendOptions.Value));
    }

    #endregion

    #region PATCH STATE

    private static async Task<IResult> UpdateStateAsync(
        Guid id,
        UpdateRoomStateRequest request,
        DjRequestsDbContext context,
        ClaimsPrincipal user,
        TimeProvider timeProvider,
        IOptions<FrontendUrlOptions> frontendOptions)
    {
        var djId = GetDjId(user);

        if (request.State != "abierta" && request.State != "pausada" && request.State != "cerrada")
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["State"] = new[] { "Estado invalido. Estados permitidos: abierta, pausada, cerrada." }
            });
        }

        if (request.Version <= 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Version"] = new[] { "version debe ser un entero positivo." }
            });
        }

        // Transaccion con bloqueo de fila FOR UPDATE
        await using var transaction = await context.Database.BeginTransactionAsync();

        var lockedSalas = await context.Salas
            .FromSqlRaw("SELECT * FROM salas WHERE id = {0} FOR UPDATE", id)
            .ToListAsync();

        var sala = lockedSalas.SingleOrDefault();

        if (sala is null || sala.DjId != djId || sala.PurgeAt <= timeProvider.GetUtcNow())
        {
            await transaction.RollbackAsync();
            return Results.Problem(
                detail: "Sala no encontrada, ajena o caducada.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Sala no encontrada",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.5");
        }

        // Revalidar despues de adquirir el bloqueo con hora actual
        var now = timeProvider.GetUtcNow();
        if (sala.EndsAt <= now)
        {
            await transaction.RollbackAsync();
            return Results.Problem(
                detail: "No se pueden modificar el estado de un evento que ya termino.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Evento finalizado",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10");
        }

        if (sala.Version != request.Version)
        {
            await transaction.RollbackAsync();
            return Results.Problem(
                detail: "La version de la sala no coincide con la version enviada.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Version obsoleta",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10");
        }

        // Si es el mismo estado con version valida: devolver estado actual sin incrementar Version
        if (sala.Estado == request.State)
        {
            await transaction.RollbackAsync();
        return Results.Ok(BuildRoomResponse(sala, now, frontendOptions.Value));
        }

        // Cambiar estado, incrementar version, actualizar UpdatedAt
        sala.Estado = request.State;
        sala.Version = request.Version + 1;
        sala.UpdatedAt = timeProvider.GetUtcNow();

        try
        {
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Results.Problem(
                detail: "La version de la sala no coincide con la version enviada.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Version obsoleta",
                type: "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10");
        }

        return Results.Ok(BuildRoomResponse(sala, timeProvider.GetUtcNow(), frontendOptions.Value));
    }

    #endregion

    private static RoomResponse BuildRoomResponse(Entities.Sala sala, DateTimeOffset now, FrontendUrlOptions frontendOptions)
    {
        var hasEnded = now >= sala.EndsAt;
        var canReceiveRequests = sala.Estado == "abierta" && now < sala.EndsAt && now < sala.PurgeAt;

        var baseUrl = frontendOptions.BaseUrl.TrimEnd('/');
        var inviteUrl = $"{baseUrl}/join/{sala.Codigo}";

        return new RoomResponse(
            sala.Id,
            sala.DjId,
            sala.Nombre,
            sala.Codigo,
            sala.Estado,
            sala.StartsAt,
            sala.EndsAt,
            sala.Timezone,
            sala.PurgeAt,
            sala.Version,
            sala.CreatedAt,
            sala.UpdatedAt,
            sala.MaxSolicitudesSesion,
            sala.MaxPlaylistsSesion,
            sala.IntervaloEnvioSegundos,
            hasEnded,
            canReceiveRequests,
            inviteUrl
        );
    }
}
