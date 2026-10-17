# DJ Requests — Estado de la tarea 04

## Resumen
- Tarea 01: estructura del backend, modelo de datos, migración inicial y SQL de revisión.
- Tarea 02: autenticación de DJs — endpoints, Identity, tokens, rate limiting por IP, tests.
- Tarea 03: creación y gestión de salas del DJ — endpoints `/api/rooms` con validación estricta, concurrencia con `FOR UPDATE`, código criptográfico, zona horaria IANA, inviteUrl configurable, pruebas e integración y migración de concurrencia.
- Tarea 04 (correcciones acotadas post-revisión): validación de esquema en `Frontend:BaseUrl` (0a), test concurrente de colisión forzada de código de sala (0b), y ajustes del contrato de invitados.

## Archivos creados/modificados (Tarea 04)
- `backend/src/DjRequests.Api/Modules/Rooms/Options/FrontendUrlOptionsValidator.cs` — rechaza esquemas ≠ http/https en cualquier entorno.
- `backend/tests/DjRequests.IntegrationTests/Modules/Rooms/RoomOptionsValidationTests.cs` — añadido `FrontendUrl_InvalidScheme_FailsInDevelopment` (`ftp://`, `file://`, `ws://`).
- `backend/tests/DjRequests.IntegrationTests/Modules/Rooms/RoomEndpointsTests.cs` — añadido `Create_ConcurrentRequests_ForcedCollision_No500_NoDataLoss` con generador `CollidingThenFreeCodeGenerator` (thread-safe).
- `backend/tests/DjRequests.IntegrationTests/Fixtures/WebAppFactory.cs` — `NpgsqlConnection.ClearPool` al disponer cada factory (evita agotar conexiones del contenedor compartido).

## Comandos ejecutados (Tarea 04)
```cmd
dotnet build backend\src\DjRequests.Api\DjRequests.Api.csproj --no-restore
dotnet build backend\tests\DjRequests.IntegrationTests\DjRequests.IntegrationTests.csproj --no-restore
dotnet test backend\tests\DjRequests.IntegrationTests\DjRequests.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~Modules.Guests"
dotnet test backend\tests\DjRequests.IntegrationTests\DjRequests.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~Modules.Rooms"
dotnet test backend\DjRequests.slnx --no-build
```

## Resultados (Tarea 04)
- **Build API**: 0 errores, 0 advertencias.
- **Build tests**: 0 errores, 1 advertencia preexistente (CS0618 en `DatabaseFixture.cs`).
- **Tests Guests**: 40/40 en verde.
- **Tests Rooms**: 89/89 en verde (+4 vs 85: 3 esquemas + 1 colisión concurrente).
- **Suite completa**: 193/193 en verde (~53 s), Docker disponible con Testcontainers PostgreSQL 17. 0 fallidas, 0 omitidas.
- **Ajuste 0a — esquema de `Frontend:BaseUrl`**: el validador ya rechazaba http/https incorrectamente en cualquier entorno; ahora hay test explícito en Development (`ftp://`, `file://`, `ws://` → falla) además del ya existente en Production.
- **Ajuste 0b — colisión concurrente forzada**: test con `CollidingThenFreeCodeGenerator` (devuelve el mismo código a las primeras N llamadas y luego códigos libres, thread-safe con `Interlocked`). Dos `POST /api/rooms` concurrentes colisionan de verdad contra `IX_salas_codigo` (SQLSTATE `23505`), reintentan y ambas terminan 201 con códigos distintos; 3 salas persisten con códigos únicos, sin 500 ni pérdida de datos.

## Notas (Tarea 04)
- La suite completa superó el umbral de conexiones del contenedor (53300 `too many clients`) al pasar de 149 a 193 tests: cada `WebAppFactory` crea una BD única y su propio pool Npgsql, que no se liberaba al disposing. Se añadió `NpgsqlConnection.ClearPool` en `DisposeAsync`.
- **Migraciones**: no se generó ni aplicó ninguna migración en esta tarea (`sesiones_invitado` ya existía desde `InitialCore`). `dj_requests` no fue tocada.

## Pendientes (Tarea 04)
- Ninguno dentro del alcance acotado (Rooms 0a/0b + contrato de invitados verificado con suite completa en verde).

---

# DJ Requests — Estado de la tarea 03

## Resumen
- Tarea 01: estructura del backend, modelo de datos, migración inicial y SQL de revisión. La migración `20261006034357_InitialCore` fue aplicada localmente a `dj_requests` por el usuario (confirmación: `Done.`).
- Tarea 02: autenticación de DJs — endpoints, Identity, tokens, rate limiting por IP, tests.
- Tarea 03: creación y gestión de salas del DJ — endpoints `/api/rooms` (CREATE, LIST, GET, PUT, PATCH state) con validación estricta, concurrencia con `FOR UPDATE`, código criptográfico, zona horaria IANA, inviteUrl configurable, pruebas de integración y migración de concurrencia.

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
- `backend/src/DjRequests.Api/Program.cs` (configuración Identity, BearerToken, Authorization, RateLimiting, ProblemDetails 401/403)
- `backend/src/DjRequests.Api/appsettings.json` (AuthRateLimits)
- `backend/tests/DjRequests.IntegrationTests/Modules/Accounts/AuthEndpointsTests.cs`
- `backend/tests/DjRequests.IntegrationTests/Modules/Accounts/RateLimitingTests.cs`
- `backend/tests/DjRequests.IntegrationTests/Fixtures/WebAppFactory.cs` (base única por test, contenedor compartido, barrera de concurrencia)
- `backend/tests/DjRequests.IntegrationTests/Fixtures/RegisterInsertBarrierInterceptor.cs` (barrera determinista solo para tests, solo inserciones de `DjAccount`)
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

## Comandos ejecutados (corrección acotada post-revisión)
```cmd
dotnet build backend\DjRequests.slnx --no-restore
dotnet test backend\DjRequests.slnx --no-build
dotnet test backend\DjRequests.slnx --no-build --filter "<filtro por test fallido>"
```

## Resultados (Tarea 02, tras la corrección acotada)
- **Build**: Exitoso. 0 errores, 1 advertencia preexistente (CS0618 en `DatabaseFixture.cs`, no bloqueante).
- **Tests completos**: 64/64 en verde (15 SchemaTests + 41 AuthEndpointsTests + 8 RateLimitingTests), ~20 s, con Docker disponible.
- **Validación de entradas (manual en handlers, antes de `Trim()`)**: `POST /api/auth/register`, `login` y `refresh` validan null/omitido, Email obligatorio + formato + ≤256, `artistName` obligatorio + ≤100 tras trim, `password` obligatorio sin trim (política Identity), `refreshToken` obligatorio; errores 400 con `ValidationProblemDetails` y claves de campo consistentes (`Email`, `ArtistName`, `Password`, `RefreshToken`). Ninguna entrada inválida produce 500; campos extra del cliente se ignoran.
- **Auth Endpoints**:
  - `POST /api/auth/register` — 201 Created; 409 por email duplicado (case-insensitive) con `ProblemDetails` y `Content-Type: application/problem+json` (SQLSTATE 23505 únicamente sobre `IX_djs_normalized_email`/`UserNameIndex`; otras violaciones de integridad NO se convierten en 409).
  - `POST /api/auth/login` — 200 OK con accessToken (15 min), refreshToken (7 días), tokenType=Bearer, expiresIn=900. Fallos (inexistente, contraseña incorrecta, bloqueo, suspendido) devuelven 401 con mensaje genérico idéntico.
  - `POST /api/auth/refresh` — 200 OK con nuevo par de tokens; valida formato/caducidad con TimeProvider, usuario, estado activo y `ValidateSecurityStampAsync`; token manipulado sin claim de stamp o con caducidad vencida → 401 (nunca 500; `CryptographicException` capturada).
  - `GET /api/auth/me` — 200/401/403 correctos; requiere política ActiveDj (estado="activo").
- **ProblemDetails en 401/403**: middleware en `Program.cs` que, sin cuerpo escrito y con respuesta 401/403, emite RFC 9457 (`status`, título según RFC 9110 §15.5.2/§15.5.4) con `Content-Type: application/problem+json` vía `IProblemDetailsService`; no sobrescribe respuestas con cuerpo. El challenge bearer conserva la cabecera `WWW-Authenticate`.
- **Lockout (.NET 10)**: al cruzar el umbral (5 intentos), `UserManager.AccessFailedAsync` reinicia `AccessFailedCount` a 0 y conserva el estado en `LockoutEnd` (15 min). El test verifica ambos estados: umbral sin bloqueo a los 4 intentos y bloqueo activo tras el 5º, con rechazo del password correcto.
- **Concurrencia**: test con `Task.WhenAll` y barrera `RegisterInsertBarrierInterceptor` (solo en tests, solo inserciones de `DjAccount`, asíncrona con timeout de 10 s); resultado real: 1×201 + 1×409, una sola cuenta persistida, ambos requests atravesaron la barrera, ningún 500.
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

## Archivos creados/modificados (Tarea 03)
- `backend/src/DjRequests.Api/Modules/Rooms/Dtos/CreateRoomRequest.cs`
- `backend/src/DjRequests.Api/Modules/Rooms/Dtos/RoomResponse.cs`
- `backend/src/DjRequests.Api/Modules/Rooms/Dtos/UpdateRoomRequest.cs`
- `backend/src/DjRequests.Api/Modules/Rooms/Dtos/UpdateRoomStateRequest.cs`
- `backend/src/DjRequests.Api/Modules/Rooms/Endpoints/RoomEndpoints.cs`
- `backend/src/DjRequests.Api/Modules/Rooms/Services/IRoomCodeGenerator.cs`
- `backend/src/DjRequests.Api/Modules/Rooms/Services/RoomCodeGenerator.cs`
- `backend/src/DjRequests.Api/Modules/Rooms/Services/TimeZoneValidator.cs`
- `backend/src/DjRequests.Api/Modules/Rooms/Options/FrontendUrlOptions.cs`
- `backend/src/DjRequests.Api/Modules/Rooms/Options/FrontendUrlOptionsValidator.cs` (validación de `Frontend:BaseUrl` al arrancar)
- `backend/src/DjRequests.Api/Program.cs` (DI de `IRoomCodeGenerator`, `FrontendUrlOptions` con `ValidateOnStart` + validador, verificación de zona default; `MapRoomEndpoints()`)
- `backend/src/DjRequests.Api/appsettings.json` y `appsettings.Development.json` (sección `Frontend:BaseUrl`)
- `backend/src/DjRequests.Api/Persistence/Mappings/SalaMap.cs` (`Version` con `IsConcurrencyToken()`)
- `backend/src/DjRequests.Api/Persistence/Migrations/20261016003450_SalaVersionConcurrencyToken.cs` (+ Designer, snapshot actualizado)
- `backend/artifacts/SalaVersionConcurrencyToken.sql` (SQL idempotente de revisión, generado)
- `backend/tests/DjRequests.IntegrationTests/Modules/Rooms/RoomEndpointsTests.cs` (70 pruebas: 6 offset explícito, 3 colisiones de código, overflow, parser estricto, contrato PUT/PATCH, pausa bajo bloqueo)
- `backend/tests/DjRequests.IntegrationTests/Modules/Rooms/RoomOptionsValidationTests.cs` (15 pruebas unitarias de `FrontendUrlOptionsValidator` y `TimeZoneValidator`)
- `backend/tests/DjRequests.IntegrationTests/Fixtures/SequenceCodeGenerator.cs` (generadores deterministas para tests de colisión)
- `backend/tests/DjRequests.IntegrationTests/Fixtures/RoomLockPauseInterceptor.cs` (pausa en `FOR UPDATE` para revalidación post-lock)
- `backend/tests/DjRequests.IntegrationTests/Fixtures/WebAppFactory.cs` (soporte de `CustomCodeGenerator` y `EnableRoomLockPause`)
- `backend/src/DjRequests.Api/Rooms.http` (ejemplos)
- `backend/README.md` (sección Rooms Endpoints y estado de migraciones)
- `docs/ESTADO.md` (este archivo)
- `docs/BASE_DE_DATOS.md` (cabecera actualizada con estado de migraciones)

## Comandos ejecutados (corrección de revisión)
```cmd
dotnet build backend\src\DjRequests.Api\DjRequests.Api.csproj --no-restore
dotnet build backend\tests\DjRequests.IntegrationTests\DjRequests.IntegrationTests.csproj --no-restore
dotnet test backend\tests\DjRequests.IntegrationTests\DjRequests.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~Modules.Rooms"
dotnet test backend\DjRequests.slnx --no-build
```

## Resultados (corrección de revisión — commit 6b638cf)
- **Build**: exitoso, 0 errores, 1 advertencia preexistente (CS0618 en `DatabaseFixture.cs`).
- **Tests Rooms**: 85/85 en verde (70 endpoints + 15 unitarios de validadores). Incluye: 3 de colisión real de código (reintento 23505, agotamiento 409, concurrentes sin 500), overflow de `purgeAt` en POST y PUT, revalidación post-lock con pausa determinista en `FOR UPDATE`, parser estricto (texto extra y fecha sin hora → 400), timezone omitida en PUT → default, versiones `0`/negativas → 400 en PUT y PATCH, overflow de paginación, y validadores de `Frontend:BaseUrl`/`TimeZoneValidator`.
- **Suite completa**: 149/149 en verde (85 Rooms + 64 resto: 15 SchemaTests + 41 AuthEndpointsTests + 8 RateLimitingTests), ~40 s, Docker disponible con Testcontainers PostgreSQL 17. 0 fallidas, 0 omitidas.
- **Endpoints**:
  - `POST /api/rooms` — 201 con id, código de 10 chars (`[A-HJ-NP-Z2-9]`, RNG criptográfico). Colisiones del índice único `IX_salas_codigo` se manejan por SQLSTATE `23505` en `SaveChangesAsync` con reintento (hasta 5 intentos); `estado=abierta`, `version=1`, `purgeAt=endsAt+168h` (pre-validado contra overflow), `inviteUrl={Frontend:BaseUrl}/join/{codigo}`. Timezone IANA validada con `TimeZoneInfo.TryConvertIanaIdToWindowsId`; omitida → `America/Mexico_City`; vacía/inválida/Windows → 400. Campos extra del cliente se ignoran.
  - `GET /api/rooms` — 200, lista propia con paginación (`page>=1`, `pageSize` 1–100, `(page-1)*pageSize` protegido contra overflow), excluye purgadas.
  - `GET /api/rooms/{id}` — 200/404 (ajena o purgada → 404 sin revelar existencia).
  - `PUT /api/rooms/{id}` — 200 requiere todos los campos (`timezone` puede omitirse → default `America/Mexico_City`); `version` positiva obligatoria (omitida/0/negativa → 400); recalcula `purgeAt`; conserva código/estado/límites; transacción + `SELECT ... FOR UPDATE`; `now` post-lock como única fuente de verdad; bajo bloqueo se revalida el nuevo `endsAt > now` (→400 si venció esperando el lock); versión obsoleta o evento finalizado → 409. Dos PUT con la misma versión → exactamente 1×200 + 1×409, sin pérdida de datos.
  - `PATCH /api/rooms/{id}/state` — 200; `version` positiva obligatoria (0/negativa → 400); transiciones `abierta`/`pausada`/`cerrada` antes del fin; mismo estado con versión válida → 200 sin incrementar versión; después del fin → 409; mismo mecanismo de bloqueo que PUT.
- **Contrato de fechas (offset explícito, parser estricto)**: `startsAt`/`endsAt` son ISO 8601 con offset obligatorio (`Z` o `±HH:mm`/`±HHmm`); parser `TryParseExact` con formatos estrictos (rechaza texto extra y fechas sin hora completa); `±HHmm` se normaliza a `±HH:mm` antes del parse. Validado en endpoint con `TryParseDateTimeOffsetWithOffset` antes de normalizar a UTC. Fecha sin offset, sin hora ni con basura → 400 con error de campo (`StartsAt`/`EndsAt`); inválida, omitida o null → 400. El offset determina el instante; `timezone` determina la presentación y no se exige coincidencia. Se conservan validaciones de orden, caducidad y desbordamiento (pre-validación: `endsAt > DateTimeOffset.MaxValue - 168h` → 400). PUT inválido no modifica la sala ni su versión.
- **Colisiones de código**: sin preconsulta `AnyAsync`; el reintento se basa en el error real de PostgreSQL (SQLSTATE `23505` + constraint `IX_salas_codigo`), con `EntityState.Detached` para limpiar el tracker EF entre intentos. Errores de BD distintos no se convierten en colisiones.
- **Concurrencia**: `SalaMap` marca `Version` como `IsConcurrencyToken()`; PUT usan transacción + bloqueo de fila con `now` post-lock; PATCH idéntico. Revalidación de propiedad, purgeAt, endsAt persistido, versión y nuevo endsAt.
- **Zona horaria**: `InvalidTimeZoneException` ya no expone `ex.Message` al usuario; se lanza `SystemTimeZoneException` (error de servidor, no 400 de validación) distinta de `ValidationException` (error de usuario). La zona default `America/Mexico_City` se verifica al iniciar la API.
- **inviteUrl**: base URL de frontend configurable vía `Frontend:BaseUrl`; validada al arrancar (`IValidateOptions` + `ValidateOnStart`): absoluta, sin credenciales/query/fragmento, HTTPS fuera de Development, HTTP solo loopback en Development. No se deriva del header Host; `TrimEnd('/')` y ruta `/join/{codigo}`.
- **Migración**: `20261016003450_SalaVersionConcurrencyToken` generada (vacía: solo anotación de modelo, sin DDL); snapshot actualizado con `IsConcurrencyToken` en `salas.version`; SQL idempotente exportado a `backend/artifacts/SalaVersionConcurrencyToken.sql`. **Aún no aplicada** a ninguna base (los tests usan bases desechables; `dj_requests` no fue tocada).

## Pendientes (Tarea 03)
- Aplicar `SalaVersionConcurrencyToken` a `dj_requests` (decisión del usuario, tras revisar el SQL; vacía, solo bookkeeping).
- QR del inviteUrl: fuera de alcance.
- Límites de sesión (`max_solicitudes_sesion`, `max_playlists_sesion`, `intervalo_envio_segundos`): asignados por servidor, no editables vía API en esta tarea.

## Notas
- Migración `20261006034357_InitialCore` aplicada localmente a `dj_requests` por el usuario (confirmación: `Done.`). Aplicación local confirmada por el usuario, no verificada por esta ejecución. No reaplicar ni modificar la migración inicial.
- Claves Data Protection: persistir en volumen en producción. No commitear a Git.
- Un despliegue detrás de proxy o balanceador necesita configurar explícitamente `ForwardedHeadersMiddleware` (`KnownProxies`/`KnownNetworks`, `ForwardLimit`) para limitar por IP real; sin esa configuración, `RemoteIpAddress` es la IP del intermediario.
- `X-Test-Client-IP` se lee únicamente en `TestIpStartupFilter` (proyecto de pruebas); no existe lectura de esa cabecera en el código de producción.
- Rate limiter en memoria por instancia de host (sin almacenamiento compartido entre instancias).
- Tokens bearer opacos nativos (no JWT) — sin dependencia de JWT.
