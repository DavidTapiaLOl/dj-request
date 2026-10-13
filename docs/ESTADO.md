# DJ Requests — Estado de la tarea 02

## Resumen
- Tarea 01: estructura del backend, modelo de datos, migración inicial y SQL de revisión. La migración `20261006034357_InitialCore` fue aplicada localmente a `dj_requests` por el usuario (confirmación: `Done.`).
- Tarea 02: autenticación de DJs — endpoints, Identity, tokens, rate limiting por IP, tests.

## Versiones fijadas
| Paquete | Versión | Notas |
|---------|---------|-------|
| Microsoft.EntityFrameworkCore | 10.0.12 | LTS .NET 10 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | Compatible con EF Core 10.0.x |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.12 | Compatible con EF Core 10.0.x |
| Microsoft.AspNetCore.Authentication.BearerToken | 10.0.12 | Incluido en Identity EF 10.0.12 |
| Microsoft.AspNetCore.RateLimiting | 10.0.12 | Incluido en ASP.NET Core 10 |
| Testcontainers.PostgreSql | 4.15.0 | Compatible .NET 10 |
| dotnet-ef (herramienta local) | 10.0.12 | Restaurada en backend/.config/dotnet-tools.json |

## Archivos creados/modificados (Tarea 02)
- `backend/src/DjRequests.Api/Modules/Accounts/Dtos/*.cs` (RegisterRequest, RegisterResponse, LoginRequest, TokenResponse, RefreshRequest, MeResponse)
- `backend/src/DjRequests.Api/Modules/Accounts/Endpoints/AuthEndpoints.cs`
- `backend/src/DjRequests.Api/Modules/Accounts/Policies/ActiveDjRequirement.cs`
- `backend/src/DjRequests.Api/Modules/Accounts/Policies/ActiveDjHandler.cs`
- `backend/src/DjRequests.Api/Modules/Accounts/Options/AuthRateLimitOptions.cs`
- `backend/src/DjRequests.Api/Program.cs` (configuración Identity, BearerToken, Authorization, RateLimiting)
- `backend/src/DjRequests.Api/appsettings.json` (AuthRateLimits)
- `backend/tests/DjRequests.IntegrationTests/Modules/Accounts/AuthEndpointsTests.cs`
- `backend/tests/DjRequests.IntegrationTests/Modules/Accounts/RateLimitingTests.cs`
- `backend/tests/DjRequests.IntegrationTests/Fixtures/WebAppFactory.cs` (base única por test, contenedor compartido)
- `backend/tests/DjRequests.IntegrationTests/Fixtures/TestIpStartupFilter.cs` (simulación de `RemoteIpAddress` vía header `X-Test-Client-IP`, solo infraestructura de pruebas)
- `backend/tests/DjRequests.IntegrationTests/Fixtures/PostgresFixture.cs` (contenedor PostgreSQL compartido + colección IntegrationCollection)
- `backend/tests/DjRequests.IntegrationTests/Fixtures/DatabaseFixture.cs` (WebAppFactoryCollection removed)
- `backend/README.md` (sección Auth Endpoints)
- `backend/src/DjRequests.Api/Accounts.http` (ejemplos)
- `docs/ESTADO.md` (este archivo)

## Comandos ejecutados (Tarea 02)
```cmd
dotnet add backend\src\DjRequests.Api package Microsoft.AspNetCore.RateLimiting --version 10.0.12
dotnet build backend\DjRequests.slnx --no-restore
dotnet test backend\DjRequests.slnx --filter "FullyQualifiedName~Accounts"
dotnet test backend\DjRequests.slnx --filter "(FullyQualifiedName~AuthEndpointsTests.Me_)|(FullyQualifiedName~Expired)"
dotnet test backend\DjRequests.slnx
```

## Resultados (Tarea 02)
- **Build**: Exitoso. 0 errores, 1 advertencia preexistente (CS0618 en `DatabaseFixture.cs`, no bloqueante).
- **Tests completos**: 46/46 en verde (15 SchemaTests + 31 Accounts), ~19 s, con Docker disponible.
- **Auth Endpoints**:
  - `POST /api/auth/register` — 201 Created; valida formato de email, artistName obligatorio (≤100, trim), password min 12 vía Identity, email único case-insensitive; errores de validación con claves de campo (`Email`, `ArtistName`).
  - `POST /api/auth/login` — 200 OK con accessToken (15 min), refreshToken (7 días), tokenType=Bearer, expiresIn=900.
  - `POST /api/auth/refresh` — 200 OK con nuevo par de tokens; valida expiración con TimeProvider, usuario, estado activo y security stamp.
  - `GET /api/auth/me` — 200/401/403 correctos; requiere política ActiveDj (estado="activo").
- **Schemes por defecto**: `DefaultAuthenticateScheme`, `DefaultChallengeScheme` y `DefaultForbidScheme` fijados a `IdentityConstants.BearerScheme` (sin ellos, `AddIdentity` dejaba la cookie por defecto y el challenge redirigía a `/Account/Login` → 404).
- **TimeProvider**: `BearerTokenOptions.TimeProvider` se enlaza al `TimeProvider` de DI (`TimeProvider.System` en producción, `FakeTimeProvider` en tests) para expiración de access token y refresh.
- **Identity**: Password min 12 chars, lockout 5 intentos/15 min, email único normalizado.
- **Rate Limiting**: partición por IP del cliente con `RateLimitPartition.GetFixedWindowLimiter` sobre `HttpContext.Connection.RemoteIpAddress` (register 5/min/IP, login 10/min/IP, refresh 20/min/IP, ventana 1 min, sin cola, límites configurables en `AuthRateLimits`). `RemoteIpAddress` es la IP del cliente conectado directamente; detrás de un proxy será la del proxy salvo `ForwardedHeaders` explícito. Si es `null` se usa la partición compartida `"unknown"` con límite. No se lee `X-Forwarded-For`. 429 con cuerpo ProblemDetails (`application/problem+json`) y `Retry-After` vía `OnRejected`. Independiente del bloqueo por cuenta de Identity.
- **Aislamiento de tests**: un `WebAppFactory` por método de test (BD PostgreSQL única por test, host y rate limiter propios); contenedor PostgreSQL compartido por colección (`PostgresFixture`/`IntegrationCollection`). Sin datos compartidos ni agotamiento de límite entre tests.

## Pendientes (Tarea 02)
- Verificación de email (confirmación de registro) — fuera de alcance.
- Recuperación de contraseña — fuera de alcance.
- Logout y gestión de sesiones por dispositivo — fuera de alcance.
- Revocación individual de refresh tokens — fuera de alcance.
- Rotación de refresh token de un solo uso — documentada como limitación.

## Notas
- Migración `20261006034357_InitialCore` aplicada localmente a `dj_requests` por el usuario (confirmación: `Done.`). Aplicación local confirmada por el usuario, no verificada por esta ejecución. No reaplicar ni modificar la migración inicial.
- Claves Data Protection: persistir en volumen en producción. No commitear a Git.
- Un despliegue detrás de proxy o balanceador necesita configurar explícitamente `ForwardedHeadersMiddleware` (`KnownProxies`/`KnownNetworks`, `ForwardLimit`) para limitar por IP real; sin esa configuración, `RemoteIpAddress` es la IP del intermediario.
- `X-Test-Client-IP` se lee únicamente en `TestIpStartupFilter` (proyecto de pruebas); no existe lectura de esa cabecera en el código de producción.
- Rate limiter en memoria por instancia de host (sin almacenamiento compartido entre instancias).
- Tokens bearer opacos nativos (no JWT) — sin dependencia de JWT.
