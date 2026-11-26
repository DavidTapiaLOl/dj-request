# DJ Requests — Estado de verificaciones posteriores (2026-11-25)

# DJ Requests — Tarea 13C: suspensión y reactivación administrativa de DJs

## Alcance ejecutado
Rama `feat/admin-dj-status` creada desde `feat/admin-djs` (`a633a73`). Solo backend, migración admin, SQL de revisión y documentación. Sin commit ni push. No se modificaron clientes, dependencias, contratos DJ/invitado, datos reales de `dj_requests` ni la API en ejecución. `Retention:Enabled` permanece `false`.

## Contrato implementado
- `PATCH /api/admin/djs/{id:guid}/state`, protegido por `AdminBearer` y `ActiveAdmin`; `Cache-Control: no-store` en todas las respuestas, incluidos 400/401/403/404/409 y errores previos al handler.
- Request: `{ "estado": "suspendido", "expectedEstado": "activo" }`. Ambos campos son obligatorios; se normalizan con `Trim` y minúsculas; solo se aceptan `activo` y `suspendido`. Inválidos → 400. DJ inexistente → 404. Estado real distinto de `expectedEstado` → 409. Estado real igual a `expectedEstado` y a `estado` → 200 sin cambios ni auditoría duplicada.
- Response 200: DTO explícito `{ "id": "guid", "estado": "suspendido" }`. Sin `PasswordHash`, `SecurityStamp`, correos normalizados, lockout ni datos de sesión.
- `expectedEstado` detecta conflictos de estado, pero no sustituye una versión monotónica: no detecta todos los cambios intermedios que terminen en el mismo estado.
- La suspensión cambia `djs.estado`, renueva el security stamp, revoca `sesiones_invitado.revoked_at` y registra auditoría. No borra cuentas, perfiles, fotos, salas, solicitudes ni playlists; no cambia el estado de salas ni `purge_at`/`ends_at`.
- Tras suspender, el DJ no puede iniciar sesión, refrescar tokens ni usar rutas `ActiveDj` (403); los tokens emitidos antes dejan de ser válidos aunque se reactive la cuenta. Las sesiones de invitado revocadas siguen revocadas; crear sesión con DJ suspendido devuelve 404. Al reactivar hace falta un login nuevo y las nuevas sesiones siguen las reglas existentes de sala abierta, no finalizada y no purgada.
- `ActiveDjHandler` valida ahora el security stamp mediante `SignInManager<DjAccount>.ValidateSecurityStampAsync` y el tipo de claim configurado en `IdentityOptions.ClaimsIdentity.SecurityStampClaimType`. No se interpreta JWT, no hay cookies ni cambios en los esquemas de autenticación.

## Atomicidad y concurrencia
- Una sola `NpgsqlConnection` perteneciente a `DjRequestsDbContext` y una transacción compartida; `AdminDbContext` se crea dentro de la operación sobre esa misma conexión. No hay transacciones distribuidas ni `SaveChanges` independientes fuera de la transacción.
- Orden de bloqueo DJ → salas: `SELECT * FROM djs WHERE "Id" = {0} FOR UPDATE` y, solo para transiciones, `SELECT * FROM salas WHERE dj_id = {0} FOR UPDATE`. La revalidación de `expectedEstado` ocurre bajo bloqueo. Las sesiones se revocan con SQL parametrizado sobre `sala_id IN (SELECT id FROM salas WHERE dj_id = @djId)`, sin bloquear `sesiones_invitado` antes que `salas`.
- La creación de sesión de invitado mantiene su bloqueo de sala y revalida DJ activo; la prueba de carrera verifica que no queda ninguna sesión no revocada. La limitación documentada es que una petición que ya estaba en ejecución antes de la suspensión puede completar lógica iniciada previamente, pero su sesión queda revocada por la transacción de suspensión.

## Auditoría
- `auditoria_admin` pertenece a `AdminDbContext`: `id`, `administrador_id` FK nullable a `admin_accounts` (`ON DELETE SET NULL`), `accion`, `tipo_recurso`, `recurso_id`, `resultado` y `created_at`. Índices por `created_at` y por (`administrador_id`, `created_at`); checks de texto no vacío.
- Solo transiciones efectivas: `DJ_Suspendido` o `DJ_Reactivado`, `tipo_recurso = DJ`, `resultado = Exitoso`, `recurso_id = id` del DJ y `created_at = TimeProvider`. El administrador se toma del `NameIdentifier` autenticado, nunca del body. Sin payload libre, contraseñas, tokens, correos ni contenido musical. No hay endpoint de consulta ni limpieza automática; la retención de auditoría queda pendiente de decisión.

## Migración
- `AddAuditoriaAdmin` **generada** en `AdminDbContext` con historial `__EFMigrationsHistory_Admin`; SQL de revisión exportado a `backend/artifacts/AddAuditoriaAdmin.sql`. No se modificaron migraciones aplicadas ni se duplicaron tablas Identity. La migración **no fue aplicada** a `dj_requests` por este agente.
- La primera generación con un DLL obsoleto produjo una migración vacía; se eliminó y regeneró desde una copia temporal del proyecto, sin detener la API del usuario.

## Cambios aplicados
- `backend/src/DjRequests.Api/Entities/AuditoriaAdmin.cs` — **nuevo**.
- `backend/src/DjRequests.Api/Modules/Administration/Dtos/AdminDjStateRequest.cs` y `AdminDjStateResponse.cs` — **nuevos**.
- `backend/src/DjRequests.Api/Modules/Administration/Services/AdminDjService.cs` — `SetStateAsync` con transacción compartida, bloqueo, renovación de stamp, revocación y auditoría.
- `backend/src/DjRequests.Api/Modules/Administration/Endpoints/AdminDjEndpoints.cs` — `PATCH /state` con validación, DTO y `no-store`.
- `backend/src/DjRequests.Api/Modules/Accounts/Policies/ActiveDjHandler.cs` — validación de security stamp.
- `backend/src/DjRequests.Api/Persistence/AdminDbContext.cs` — `DbSet` y mapeo de `auditoria_admin`.
- `backend/src/DjRequests.Api/Persistence/AdminMigrations/20261126035100_AddAuditoriaAdmin.cs` y `.Designer.cs`, y `AdminDbContextModelSnapshot.cs` — **generados/actualizados**.
- `backend/artifacts/AddAuditoriaAdmin.sql` — **nuevo**.
- `backend/tests/DjRequests.IntegrationTests/Modules/Administration/AdminDjStateEndpointsTests.cs` — **nuevo**: 11 pruebas con PostgreSQL/Testcontainers.
- `docs/ESTADO.md`, `docs/ARQUITECTURA.md`, `docs/BASE_DE_DATOS.md` y `backend/README.md` — documentación.

## Resultados reales (verificados con evidencia)

| Comprobación | Resultado real |
|---|---|
| Build con salida temporal | `dotnet build backend\DjRequests.slnx --nologo -p:BaseOutputPath=...\dj-requests-13c-tests-build\` → correcto, 0 errores; avisos preexistentes de licencia `SixLabors.ImageSharp 4.1.3` y `CS0618` en `DatabaseFixture.cs`. |
| `AdminDjStateEndpointsTests` | 11/11 correctas, 0 fallidas, 0 omitidas, 8 s. |
| Pruebas afectadas | `AdminAuthEndpointsTests\|AdminDjEndpointsTests\|AdminDjStateEndpointsTests\|AuthEndpointsTests\|RoomEndpointsTests\|GuestEndpointsTests` → 194/194 correctas, 1 m 22 s. |
| Suite completa | `dotnet test backend\DjRequests.slnx --nologo --no-build -p:BaseOutputPath=...\dj-requests-13c-tests-build\` → **402/402 correctas**, 0 fallidas, 0 omitidas, 2 m 23 s. |
| Git | Rama `feat/admin-dj-status`, sin commit ni push; cambios ajenos conservados. `git diff --check` sin errores; solo avisos de normalización LF→CRLF en archivos modificados. |
| Base de datos | `AddAuditoriaAdmin` generada y SQL exportado; no aplicada a `dj_requests`. Pruebas en bases efímeras, `Retention:Enabled=false`. |
| Seguridad | Sin secretos, tokens, cadenas de conexión ni `.env` en diffs, logs o documentación. |

## Límites y pendientes
- Sin verificación manual del usuario sobre el nuevo endpoint; la evidencia nueva es automatizada.
- Falta decidir la retención de `auditoria_admin` y revisar/aplicar `AddAuditoriaAdmin` en `dj_requests` cuando el usuario lo autorice.
- Las secciones históricas siguientes describen estados anteriores, no el estado final de esta tarea.

---

# DJ Requests — Corrección de búsqueda literal posterior al commit 79d603e

## Alcance ejecutado
Corrección en `feat/admin-djs` sobre el commit `79d603e`. Solo backend: `AdminDjService.cs`, `AdminDjEndpointsTests.cs` y `docs/ESTADO.md`. Commit y push en `feat/admin-djs` con el mensaje acordado, sin force ni merge; sin cambios en móvil, web, contratos, dependencias ni migraciones; `dj_requests` intacta; servidores no detenidos.

## Cambios aplicados
- `backend/src/DjRequests.Api/Modules/Administration/Services/AdminDjService.cs` — `search` pasa a patrón literal: conserva `Trim` y máximo 100 definido en el endpoint, escapa `\`, `%` y `_`, y usa `EF.Functions.ILike(columna, patrón, "\\")`, con patrón parametrizado y `ESCAPE '\'` explícito generado por Npgsql.
- `backend/tests/DjRequests.IntegrationTests/Modules/Administration/AdminDjEndpointsTests.cs` — 6 pruebas nuevas: búsqueda por correo insensible a mayúsculas, `Trim` de espacios exteriores, literalidad de `%`, `_` y `\`, `page=int.MaxValue` con `pageSize=100`, orden por `Id` con `CreatedAt` idéntico y paginación sin duplicados, y sesión real de invitado con `X-Guest-Token` sin acceso administrativo.
- `docs/ESTADO.md` — esta sección.

## Resultados reales (verificados con evidencia)

| Comprobación | Resultado real |
|---|---|
| Build con salida temporal | `dotnet build backend\DjRequests.slnx --nologo -p:BaseOutputPath=...\dj-requests-13b-literal-build\` → correcto, 0 errores; avisos preexistentes de licencia `SixLabors.ImageSharp 4.1.3` y `CS0618` en `DatabaseFixture.cs`. No se detuvo la API. |
| Primera ejecución de `AdminDjEndpointsTests` | 13/14; único fallo `List_SearchWithLikeWildcards_IsLiteral`: `429 TooManyRequests` al registrar seis DJs seguidos. Corregido creando los DJs de esa prueba con `UserManager<DjAccount>` en la base aislada. |
| Pruebas `AdminDjEndpointsTests` | 14/14 correctas, 0 fallidas, 0 omitidas, 9 s. |
| Pruebas administrativas combinadas | `dotnet test ... --filter "FullyQualifiedName~AdminAuthEndpointsTests\|FullyQualifiedName~AdminDjEndpointsTests"` → 36/36 correctas, 18 s. |
| Suite completa | `dotnet test backend\DjRequests.slnx --nologo -p:BaseOutputPath=...\dj-requests-13b-literal-build\` → **391/391 correctas**, 0 fallidas, 0 omitidas, 2 m 17 s. |
| `git diff --check` | Código de salida 0; solo avisos LF→CRLF en `AdminDjService.cs`, `AdminDjEndpointsTests.cs` y `docs/ESTADO.md`. |
| Base de datos y seguridad | Sin migraciones generadas ni aplicadas; sin secretos, tokens, cadenas de conexión ni `.env` en diffs, logs o documentación. |
| Git | Corrección publicada en `feat/admin-djs` con el commit `fix(admin): buscar DJs literalmente y completar pruebas`; sin force ni merge. Se conservaron los cambios ajenos. |

## Límites y pendientes
- Sin prueba manual de los nuevos endpoints; la evidencia manual del usuario sigue cubriendo únicamente login/me administrativo.
- Las secciones históricas siguientes describen estados anteriores, no el estado final de esta corrección.

---

# DJ Requests — Estado de verificaciones posteriores (2026-11-25)

# DJ Requests — Tarea 13B: consulta administrativa de DJs

## Alcance ejecutado
Backend únicamente. Rama `feat/admin-djs` creada desde `feat/admin-auth` (`c894834`); el estado previo al cierre era sin commit ni push. Sin migraciones, clientes, dependencias, datos de `dj_requests` ni contratos DJ/invitado. No se detuvo la API en ejecución.

## Contexto confirmado por el usuario
- `AddAdminIdentity` ya se aplicó localmente en `dj_requests`.
- Se creó una cuenta de administrador con `dotnet run --project backend/src/DjRequests.Api -- --provision-admin`.
- Se verificaron manualmente `POST /api/admin/auth/login` y `GET /api/admin/auth/me`.
- Estos tres puntos son evidencia del usuario; este agente no ejecutó la migración, el aprovisionamiento ni esas verificaciones manuales.

## Contrato implementado
- `GET /api/admin/djs?page=1&pageSize=20&search=&estado=` — listado paginado, protegido por `AdminBearer`/`ActiveAdmin`, sin caché.
- `GET /api/admin/djs/{id:guid}` — detalle de un DJ, protegido por la misma política, sin caché.
- Filtros: `search` opcional (trim, máx. 100 caracteres, `ILIKE` insensible a mayúsculas sobre email y nombre artístico); `estado` opcional (`activo` o `suspendido`, normalizado a minúsculas).
- Paginación: `page >= 1`, `pageSize` 1-100, orden `CreatedAt DESC, Id`, respuesta con `items`, `page`, `pageSize`, `totalCount` y `hasMore`.
- Detalle: campos públicos del DJ y perfil (`description`, `startYear`, `photoUrl`, `socialLinks`); si no hay perfil, esos campos son nulos. `photoUrl` se genera con `IFileStorage.GetPublicUrl`.
- Errores 400/401/403/404 con `Cache-Control: no-store`; las respuestas nunca incluyen `PasswordHash`, `SecurityStamp`, correos normalizados, `lockout`, roles ni datos de sesión.
- Sin endpoints de edición, suspensión, reactivación ni eliminación de DJs.

Ejemplos:

```http
GET /api/admin/djs?page=1&pageSize=20
Authorization: Bearer <access-token-de-administrador>

GET /api/admin/djs/2f8c1c5e-4a2e-4c1f-9c0b-6d7a1e2f3b44
Authorization: Bearer <access-token-de-administrador>
```

## Cambios aplicados
- `backend/src/DjRequests.Api/Modules/Administration/Dtos/AdminDjListItemDto.cs` — **nuevo**: `Id`, `Email`, `ArtistName`, `Estado`, `CreatedAt`.
- `backend/src/DjRequests.Api/Modules/Administration/Dtos/AdminDjDto.cs` — **nuevo**: detalle con campos públicos y `SocialLinksDto`.
- `backend/src/DjRequests.Api/Modules/Administration/Dtos/AdminDjPageResponse.cs` — **nuevo**: `Items`, `Page`, `PageSize`, `TotalCount`, `HasMore`.
- `backend/src/DjRequests.Api/Modules/Administration/Services/AdminDjService.cs` — **nuevo**: consulta `AsNoTracking`, filtros, orden estable, conteo y construcción de DTOs.
- `backend/src/DjRequests.Api/Modules/Administration/Endpoints/AdminDjEndpoints.cs` — **nuevo**: endpoints GET, validación de consulta y 404 tipado.
- `backend/src/DjRequests.Api/Modules/Administration/AdministrationServiceCollectionExtensions.cs` — registra `AdminDjService`.
- `backend/src/DjRequests.Api/Program.cs` — mapea `app.MapAdminDjEndpoints()`.
- `backend/src/DjRequests.Api/Modules/Administration/AdminAuthNoStoreMiddleware.cs` — amplía `no-store` a `/api/admin/djs`, manteniendo `Response.OnStarting`.
- `backend/tests/DjRequests.IntegrationTests/Modules/Administration/AdminDjEndpointsTests.cs` — **nuevo**: 8 pruebas de integración con PostgreSQL/Testcontainers.
- `docs/ESTADO.md` — esta sección.

## Resultados reales (verificados con evidencia)

| Comprobación | Resultado real |
|---|---|
| Build directo inicial | `dotnet build backend\DjRequests.slnx --nologo` falló con `MSB3027`/`MSB3021`: el proceso `DjRequests.Api (22712)` bloqueaba `bin\Debug\net10.0\DjRequests.Api.exe`. No se detuvo el servidor. |
| Build en salida temporal | `dotnet build backend\DjRequests.slnx --nologo -p:BaseOutputPath=...\dj-requests-13b-build\` → correcto, 0 errores; avisos preexistentes de licencia `SixLabors.ImageSharp 4.1.3` y `CS0618` en `DatabaseFixture.cs`. |
| Primera ejecución de `AdminDjEndpointsTests` | 7/8; único fallo `Get_Detail_ReturnsProfileDataWithoutIdentityFields`: `JsonSerializer.Deserialize` sin `JsonSerializerDefaults.Web` devolvió `Guid.Empty`. Corregido en la prueba. |
| Pruebas `AdminDjEndpointsTests` | 8/8 correctas, 0 fallidas, 0 omitidas, 5 s. |
| Pruebas administrativas combinadas | `dotnet test ... --filter "FullyQualifiedName~AdminAuthEndpointsTests\|FullyQualifiedName~AdminDjEndpointsTests"` → 30/30 correctas, 15 s. |
| Suite completa | `dotnet test backend\DjRequests.slnx --nologo -p:BaseOutputPath=...\dj-requests-13b-full\` → **385/385 correctas**, 0 fallidas, 0 omitidas, 2 m 14 s. |
| Base de datos | Sin migraciones generadas ni aplicadas por este agente; `dj_requests` intacta; pruebas en bases efímeras `dj_requests_test_<guid>` con `Retention:Enabled=false`. |
| Git | Antes del cierre: rama `feat/admin-djs`, sin commit ni push; `git status --short`: 4 archivos modificados y 6 rutas nuevas; `git diff --check`: código 0, solo avisos LF→CRLF en 3 archivos rastreados; `git diff --stat` (solo rastreados): 4 archivos, 73 inserciones, 2 borrados. El cierre incluye commit y push de la lista explícita de archivos de esta tarea. |
| Seguridad | Sin secretos, tokens, cadenas de conexión ni `.env` en diffs, logs o documentación. |
| Verificación manual de los nuevos endpoints | No ejecutada todavía; la evidencia manual del usuario cubre únicamente `POST /api/admin/auth/login` y `GET /api/admin/auth/me`. |

## Límites y pendientes
- El cierre publica la lista explícita de archivos de esta tarea en `feat/admin-djs`; SHA y estado final se reportan fuera de este documento.
- No se agregó auditoría, rate limiting nuevo, seed de administradores ni acciones de escritura sobre DJs.
- La API en ejecución no se detuvo; los builds se hicieron en carpetas temporales.
- Las secciones históricas siguientes describen estados anteriores, no el estado final de esta tarea.

---

# DJ Requests — Verificación y correcciones posteriores de autenticación administrativa (2026-11-25)

## Alcance ejecutado
Verificación y endurecimiento de la base de autenticación administrativa antes de iniciar gestión administrativa de DJs, con autorización explícita del plan. Rama `feat/admin-auth`, HEAD `cd2098b`, sin commit ni push. No se tocaron migraciones, `dj_requests`, clientes, contratos DJ/invitado, dependencias ni LAN. Los cambios del usuario previos a esta tarea se conservaron.

## Cambios aplicados
- `backend/src/DjRequests.Api/Modules/Administration/AdminAuthNoStoreMiddleware.cs` — **nuevo**: fija `Cache-Control: no-store` en `/api/admin/auth` antes de rate limiting/autenticación y lo reaplica en `Response.OnStarting` para cubrir respuestas que limpian cabeceras (JSON malformado).
- `backend/src/DjRequests.Api/Modules/Common/NoStoreResult.cs` — expone `CacheControlValue` para reutilizar el valor sin duplicarlo.
- `backend/src/DjRequests.Api/Program.cs` — registra el middleware tras `UseRouting()`; el rate limiter usa la misma constante.
- `backend/src/DjRequests.Api/Modules/Administration/Endpoints/AdminAuthEndpoints.cs` — elimina wrappers `NoStoreResults.AddNoStore(...)`; la cabecera la garantiza el middleware de ruta.
- `backend/src/DjRequests.Api/Modules/Administration/Provisioning/AdminProvisioningService.cs` — valida trim, nombre 1-100, correo 1-256 y formato, contraseña no vacía y duplicados por email (`FindByEmailAsync`, SQLSTATE 23505 y errores Identity de duplicado); registra solo códigos/ID, nunca secretos.
- `backend/tests/DjRequests.IntegrationTests/Modules/Rooms/RoomEndpointsTests.cs` — reloj controlado `2026-11-25T12:00:00Z`, helpers de fechas y corrección del caso inválido que trataba `03:00Z` y `09:00-06:00` como instantes equivalentes; ahora se validan ambos instantes y `PurgeAt`.
- `backend/tests/DjRequests.IntegrationTests/Modules/Administration/AdminAuthEndpointsTests.cs` — amplía cobertura a 400/JSON malformado, 401/403, 429, caducidad access/refresh, inactivo, security stamp, tokens cruzados DJ↔admin, mismo GUID, lockout y ausencia de registro público.
- `backend/tests/DjRequests.IntegrationTests/Modules/Administration/AdminProvisioningServiceTests.cs` — **nuevo**: 11 pruebas con PostgreSQL/Testcontainers para trim, límites 100/256, correo inválido, contraseña vacía, duplicado insensible a mayúsculas sin sobrescritura y duplicado concurrente (una sola cuenta).
- `docs/ESTADO.md` — esta sección.

## Resultados reales (verificados con evidencia)

| Comprobación | Resultado real |
|---|---|
| Docker inicial | `docker info` falló: `dockerDesktopLinuxEngine` no disponible. Se inició Docker Desktop; daemon respondió `Docker 27.0.3 listo`. |
| `dotnet build backend\DjRequests.slnx --nologo` (final) | Correcto: 0 errores, 1 aviso de licencia de `SixLabors.ImageSharp 4.1.3`. En ejecuciones intermedias apareció además el aviso preexistente `CS0618` en `DatabaseFixture.cs`. |
| Primera ejecución de pruebas afectadas | 97/98; único fallo `AdminAuthEndpointsTests.Login_MalformedJson_Returns400WithNoStore` sin `Cache-Control: no-store`. Corregido con `Response.OnStarting`. |
| Pruebas afectadas, tras la corrección | `dotnet test backend\tests\DjRequests.IntegrationTests\DjRequests.IntegrationTests.csproj --nologo --filter "FullyQualifiedName~RoomEndpointsTests|FullyQualifiedName~AdminAuthEndpointsTests|FullyQualifiedName~AdminProvisioningServiceTests"` → **98/98 correctas**, 0 fallidas, 0 omitidas, 40 s. |
| Suite completa | `dotnet test backend\DjRequests.slnx --nologo` → **377/377 correctas**, 0 fallidas, 0 omitidas, 2 m 9 s, PostgreSQL 17 con Testcontainers. |
| `git diff --check` | Código de salida 0; solo avisos LF→CRLF propios del repositorio. |
| Git | `## feat/admin-auth...origin/feat/admin-auth`; 7 archivos modificados y 2 rutas nuevas (incluye `docs/ESTADO.md`); sin commit ni push. |
| Base de datos | Ninguna migración generada ni aplicada en esta tarea; `AddAdminIdentity` sigue **generada, no aplicada por este agente**; `dj_requests` no fue tocada. |

## Límites y pendientes
- No se ha publicado ni modificado el commit actual; los cambios verificados están en el árbol de trabajo.
- `AddAdminIdentity` continúa pendiente de revisión/aplicación por el usuario; no se regeneró el SQL de revisión.
- La gestión administrativa de DJs no se inició; es el siguiente paso una vez aceptado este estado.
- Sin secretos, tokens, cadenas de conexión ni `.env` en diffs, logs o documentación.
- La sección histórica de la tarea 13A siguiente describe el estado de aquel momento, no el estado final verificado arriba.

---

# DJ Requests — Estado de la tarea 13A (base de autenticación administrativa)

## Resumen
Base de autenticación para administradores separada de DJs e invitados: `AdminAccount` y `AdminDbContext` con Identity, tablas `admin_*`, esquema bearer `AdminBearer` y política `ActiveAdmin`, endpoints `POST /api/admin/auth/login`, `POST /api/admin/auth/refresh` y `GET /api/admin/auth/me`, y alta interactiva explícita mediante `--provision-admin`. Sin registro público, roles, seed, dashboard ni segundo factor. `AddAdminIdentity` fue **generada**, con SQL de revisión; **no aplicada** a `dj_requests` por este agente.

## Archivos creados/modificados
### Backend
- `backend/src/DjRequests.Api/Entities/AdminAccount.cs` — **nuevo**: `IdentityUser<Guid>` con `Name`, `IsActive` y `CreatedAt`.
- `backend/src/DjRequests.Api/Persistence/AdminDbContext.cs` — **nuevo**: `IdentityUserContext` con tablas `admin_accounts`, `admin_user_claims`, `admin_user_logins` y `admin_user_tokens`, historial `__EFMigrationsHistory_Admin` e índices propios (`IX_admin_accounts_normalized_email`, `IX_admin_accounts_normalized_username`) para no colisionar con `djs`.
- `backend/src/DjRequests.Api/Modules/Administration/` — **nuevo**: registro de servicios, constantes bearer, opciones de rate limit, política/handler `ActiveAdmin`, DTOs, endpoints, `AdminProvisioningService` y comando `--provision-admin`.
- `backend/src/DjRequests.Api/Program.cs` — registra administración, políticas `admin-login`/`admin-refresh`, mapea endpoints y permite el modo aprovisionamiento sin arrancar el servidor ni el worker.
- `backend/src/DjRequests.Api/appsettings.json` — sección `AdminAuthRateLimits`.
- `backend/src/DjRequests.Api/Persistence/AdminMigrations/` — **nuevo**: migración `20261119025942_AddAdminIdentity`, snapshot y designer.
- `backend/artifacts/AddAdminIdentity.sql` — **nuevo**: SQL generado para revisión.
- `backend/tests/DjRequests.IntegrationTests/Fixtures/WebAppFactory.cs` — migra también `AdminDbContext` y añade `CreateAdminDbContextAsync`.
- `backend/tests/DjRequests.IntegrationTests/Modules/Administration/AdminAuthEndpointsTests.cs` — **nuevo**: 8 pruebas de login/refresh/me, no-store, desactivación, security stamp, aislamiento DJ↔admin y rate limit.

### Documentación
- `docs/BASE_DE_DATOS.md` — corrige estado de `AddDjProfiles` según confirmación del usuario; documenta tablas admin, migración, historial y alta explícita.
- `docs/ARQUITECTURA.md` — actualiza la fase 3 con la base implementada y sus pendientes.
- `backend/README.md` — documenta alta, endpoints admin, política, token options y migración pendiente.
- `docs/ESTADO.md` — esta sección.

## Resultados reales (verificados con evidencia)
Estado: rama `feat/admin-auth` creada desde `f978ac9`, cambios sin commitear.

| Comprobación | Resultado real |
|---|---|
| `dotnet build backend\DjRequests.slnx --nologo` | Correcto. Avisos preexistentes: licencia de `SixLabors.ImageSharp 4.1.3` y `CS0618` en `DatabaseFixture.cs` |
| `dotnet test ... --filter "FullyQualifiedName~AdminAuthEndpointsTests"` | **8/8 correctas**, PostgreSQL 17 con Testcontainers |
| Pruebas admin + regresión DJ `Register_ValidRequest_Returns201AndPersistsDj` | **9/9 correctas**; confirmó que la política Identity admin usa las mismas reglas que DJ y no rompe registro |
| Suite completa `dotnet test backend\DjRequests.slnx --nologo` | **351 correctas, 1 fallida, 0 omitidas, total 352** |
| Fallo de la suite | `RoomEndpointsTests.Create_StartsAtWithZ_EndsWithOffset_EquivalentInstant_PersistsSameUtc`: esperaba 201, obtuvo 400 porque usa fechas fijas `2026-11-14` ya pasadas. **Preexistente**: reproducido en worktree temporal limpio en `f978ac9`; el worktree fue eliminado después |
| `dotnet tool restore --tool-manifest .config\dotnet-tools.json` | Correcto, dotnet-ef 10.0.12 |
| `dotnet ef migrations add AddAdminIdentity --context AdminDbContext ...` | Migración generada; regenerada tras renombrar el índice de usuario para evitar colisión con `UserNameIndex` de DJs |
| `dotnet ef migrations script --context AdminDbContext ... --output artifacts\AddAdminIdentity.sql` | SQL generado (2982 bytes) |
| Migraciones aplicadas a `dj_requests` | **Ninguna por este agente** |
| `git status --short` y `git diff --stat` | 7 archivos modificados y 5 rutas nuevas; sin secretos ni `.env` |
| `git diff --check` | Código de salida 0; solo avisos LF→CRLF propios del repositorio |

## Límites y pendientes
- El usuario debe revisar `backend/artifacts/AddAdminIdentity.sql` y decidir si aplica la migración; este agente no la aplicó.
- Después de aplicarla, crear la cuenta con `dotnet run --project backend/src/DjRequests.Api -- --provision-admin`.
- Refresh no tiene rotación de un solo uso ni revocación individual; cambiar el security stamp sí invalida tokens.
- No hay segundo factor, auditoría, panel de administración ni persistencia de claves de Data Protection para producción.
- Sin commit ni push. No se publicaron cuentas ni secretos.

---

# DJ Requests — Estado de la tarea 12 (playlists compartidas de extremo a extremo)

## Resumen
Playlists compartidas completas y conectadas: el invitado envía el enlace de una playlist desde la web (nuevo modo "Compartir playlist" junto al formulario de canciones) y el DJ la consulta en una pestaña nueva "Playlists" de la app móvil. Backend nuevo en `Modules/Playlists` con los mismos invariantes que las solicitudes (transacción, bloqueo sala→sesión, idempotencia antes de cuota, intervalo compartido, `no-store`) y privacidad reforzada con pruebas. Sin migraciones (la tabla `playlists_compartidas` ya existía en `InitialCore`), sin dependencias nuevas y sin cambios en contratos existentes de canciones.

## Archivos creados/modificados
### Backend
- `backend/src/DjRequests.Api/Modules/Playlists/Dtos/` — **nuevo**: `CreatePlaylistRequest` (`Nombre?` ≤200, `Enlace` ≤2048, `IdempotencyKey`), `PlaylistResponse` (`Id`, `Nombre`, `Enlace`, `CreatedAt` — sin sesión/sala) y `PlaylistPageResponse` (`Items`, `Page`, `PageSize`, `TotalCount`, `HasMore`).
- `backend/src/DjRequests.Api/Modules/Guests/GuestTokenValidation.cs` — **nuevo**: `ValidateRequestAsync` compartido para endpoints nuevos de invitado (hash → sala → revocación/expiración → `endsAt`/`purgeAt` → DJ activo, con precedencia 401/404 y `no-store`). Solo lo usan los endpoints nuevos; el flujo de solicitudes quedó intacto.
- `backend/src/DjRequests.Api/Modules/Playlists/Services/PlaylistService.cs` — **nuevo**: `CreateAsync` en transacción con bloqueo en orden sala→sesión, revalidación post-lock (`abierta`, `endsAt`, `purgeAt`), idempotencia primero (mismo payload → 200 sin consumir cuota; payload distinto → 409), después cuota `MaxPlaylistsSesion` y intervalo sobre `sesion.UltimoEnvioAt` (compartido con canciones), actualiza `UltimoEnvioAt`; `ListForDjAsync` solo con `PurgeAt > now`, orden `CreatedAt DESC, Id`, paginación ≤100.
- `backend/src/DjRequests.Api/Modules/Playlists/Endpoints/PlaylistEndpoints.cs` — **nuevo**: `POST /api/guest/rooms/{roomId:guid}/playlists` (`X-Guest-Token`, `RequireRateLimiting("guest-create")`, 201/200/400/401/404/409/429 + `Retry-After`) y `GET /api/rooms/{roomId:guid}/playlists` (política `ActiveDj`, 404 si la sala es ajena o ya purgada); `NoStoreResults` en ambas rutas.
- `backend/src/DjRequests.Api/Program.cs` — `AddScoped<IPlaylistService, PlaylistService>()` y `app.MapPlaylistEndpoints()`.
- `backend/tests/DjRequests.IntegrationTests/Modules/Playlists/PlaylistEndpointsTests.cs` — **nuevo**: 36 pruebas (creación/normalización, validaciones de enlace y nombre, token 401/404, sala pausada 409 y finalizada 404, idempotencia replay/conflicto/replay con cuota llena, cuota 429, intervalo 429 con `Retry-After`, intervalo compartido canción↔playlist, concurrencia exacta 1×201+1×429, listado DJ con orden/paginación/401/otro DJ 404/tras `EndsAt` 200/tras `PurgeAt` 404/sin datos de sesión, invitado sin listado 405, `Cache-Control: no-store`).

### Web de invitados
- `web/src/app/core/models.ts` — `Playlist`, `CreatePlaylistInput`, `CreatePlaylistPayload`.
- `web/src/app/core/guest-room.service.ts` — `createPlaylist(roomId, guestToken, payload)` → `POST /guest/rooms/{id}/playlists`.
- `web/src/app/core/api-error.ts` — el 429 con título "Límite de playlists alcanzado" distingue el mensaje de cuota de playlist del de solicitudes.
- `web/src/app/features/room/room.page.ts` — selector de modo (`signal<'song' | 'playlist'>`, inicial canción), formulario de playlist independiente (estado, validación estricta de URL con host y sin credenciales, idempotencia por intento reutilizable en reintento, mensajes de cuota/intervalo/red conservando lo escrito) y h2 dinámico; no se recarga el listado público tras el éxito.
- `web/src/app/features/room/room.page.html` — `fieldset` accesible con radios segmentados ("Pedir canción" / "Compartir playlist"), formulario con labels `.sr-only` + placeholders visibles (misma excepción aprobada), nota "Solo el DJ podrá ver esta playlist" y estados de error/éxito.
- `web/src/app/features/room/room.page.scss` — `.mode-switch` segmentado con tokens del tema, foco visible y estado activo violeta con `on-primary`.
- `web/src/app/features/room/room.page.spec.ts` — 10 pruebas nuevas de playlists (modo, borrador al alternar, bloqueo de envío inválido, URLs rechazadas, envío con payload normalizado y privacidad, nombre en blanco → null, idempotencia reutilizada/cambiada, cuota, intervalo y limpieza de errores).

### Móvil (app del DJ)
- `mobile/src/api/dto.ts` — `PlaylistResponse` y `PlaylistPageResponse`.
- `mobile/src/api/client.ts` — `listPlaylists(roomId, { page, pageSize })` → `GET /api/rooms/{id}/playlists`.
- `mobile/src/utils/dateFormat.ts` — `formatSentAt(sentAt, timeZone)` con `d MMM · HH:mm` en la zona de la sala.
- `mobile/app/(app)/room/[id].tsx` — pestañas "Solicitudes" (inicial) y "Playlists" con `accessibilityRole="tablist"/"tab"` y carga perezosa (solo al abrir la pestaña, con refresco y "Cargar más"); tarjeta con nombre o "Playlist sin nombre", fecha de envío, "Abrir playlist" (`Linking` en nativo, `<a target="_blank" rel="noopener noreferrer">` en web), estados de carga/vacío/error con "Reintentar"; sin acciones de estado ni votos.

## Resultados reales (verificados con evidencia)
Estado de código al que corresponden todas las ejecuciones: rama `feat/shared-playlists`, **HEAD `4be73f5` con los cambios de la tarea 12 sin commitear**. Las pruebas verificaron **el árbol de trabajo**, no el contenido del commit `4be73f5`: los resultados no se atribuyen únicamente a `4be73f5`. La relación temporal confirma que ningún archivo afectado cambió después de cada ejecución:

| Comprobación | Evidencia | Estado verificado |
|---|---|---|
| Suite backend 344/344 | **Archivo** `C:\Users\juand\AppData\Local\Temp\opencode\full-suite.out.log` (1883 bytes, escrito 2026-11-12 00:10:38); `full-suite.err.log` vacío (0 bytes) | Último fuente backend 00:07:37 < ejecución 00:08:36–00:10:38; sin cambios backend posteriores |
| Build backend | Incluido en el log anterior (compiló `DjRequests.Api` y `DjRequests.IntegrationTests`, ambas DLL generadas) | Idéntico al de la suite |
| Pruebas web 45/45 | **Solo consola de sesión** (sin archivo); última ejecución inicio 00:17:17, 7 archivos | Último fuente web 00:16:56 < build 00:17:01 < pruebas 00:17:17; sin cambios web posteriores |
| Build web | **Solo consola de sesión**; salida `web\dist\dj-requests-web` con fecha 00:17:01 | Mismo estado que las pruebas web |
| Typecheck y lint móvil | **Solo consola de sesión** (ejecución final posterior a 00:22:21) | Último fuente móvil 00:22:21; sin cambios móviles posteriores |
| Export web móvil | **Solo consola de sesión**; `mobile\dist` recreado 00:24:01 | Mismo estado que typecheck/lint |

- **Suite completa backend**: **344 correctas, 0 con error, 0 omitidas**, total 344, duración 1 min 54 s. El ensamblado compilado en esa ejecución (DLL de pruebas 00:07:54) **contiene** las pruebas de playlists (`findstr` encuentra `PlaylistEndpointsTests` y `Create_ValidPlaylist`); el log no enumera tests individuales, así que la inclusión de los 36 nuevos se confirma por el DLL y por el conteo 308 previos + 36.
- **Advertencias del build backend en ese log**: solo licencia de `SixLabors.ImageSharp 4.1.3` (4 líneas del mismo aviso). **CS0618 no aparece en este log**: la afirmación habitual de "2 advertencias preexistentes (licencia + CS0618)" queda **no confirmada por esta evidencia** (se reportó en tareas anteriores, fuera de este log).
- **Pruebas playlists de forma aislada**: 36/36 con PostgreSQL 17 (Testcontainers) — **solo consola de sesión** (ejecución anterior a la suite; sin archivo de log).
- **Pruebas web**: 7 archivos, **45 correctas, 0 fallidas**. La primera ejecución dio 44/45 por un mock del test sin segunda respuesta; se corrigió solo el test y se repitió, obteniendo 45/45. El archivo de pruebas no cambió después (`00:15:37`), por lo que el 45/45 final cubre el estado actual.
- **Build web**: ejecución final **correcta, sin avisos**. Las dos ejecuciones previas del mismo código emitieron aviso de presupuesto del componente `room.page.scss` (150 y 61 bytes sobre 4 kB); se recortaron reglas redundantes en vez de subir el presupuesto.
- **TypeScript móvil**: `npx tsc --noEmit` **0 errores** en la ejecución final (la primera, anterior, falló con `TS1382` en `[id].tsx` y se corrigió).
- **Lint móvil**: `npx expo lint` **0 errores y 0 avisos** en la ejecución final. El único log de lint persistido (`...\opencode\tool-output\tool_1ce6b8107001JBtelqkZqHUFlJ`, 00:21:54, 5346 problemas) corresponde a la **primera ejecución fallida** que lintea `mobile/dist`; no refleja el estado final. Se retiró ese `dist/` (artefacto gitignored, regenerable) porque ESLint lo procesaba como bundle Metro.
- **Export web móvil**: `npx expo export --platform web` **correcto**, bundle 2.66 MB, 27 archivos.
- **`git diff --check`**: código de salida 0 tras las ediciones de documentación; solo avisos LF→CRLF propios del repositorio (no son errores de espacios).
- **Git**: las verificaciones anteriores se ejecutaron **antes de crear el commit de la tarea 12** (árbol de trabajo sin commitear sobre `4be73f5`); este documento se incorpora a ese commit y no altera lo verificado. En ese momento no había push de la tarea.
- **Confirmación del usuario**: «El usuario confirmó que realizó el recorrido manual de playlists y funcionó correctamente». Es una confirmación **del usuario**, no una inspección visual realizada por el agente: el agente no ejecutó recorrido manual ni inspección visual.
- Sin migraciones generadas ni aplicadas; `dj_requests` no fue tocada. Sin dependencias nuevas. `Retention:Enabled` sigue `false`.

### Evidencia no válida o no confirmada
- **Inspección visual por parte del agente** en navegador o dispositivo: **no realizada**; compilar y pasar pruebas no acredita el aspecto del selector web ni de las pestañas móviles. El único resultado funcional manual proviene de la confirmación del usuario (ver arriba).
- **Logs persistidos de pruebas web, build web, typecheck/lint final y export móvil**: no existen; solo hay salida de consola de la sesión.
- `mobile/export.log` (18/10/2026) y `backend\tests\DjRequests.IntegrationTests\TestResults\test_results.trx` (12/10/2026, 1 test fallido antiguo): **obsoletos**, no son evidencia de esta tarea.
- No existe script `lint` en `web/package.json`; no aplica lint en la web.

## Límites y pendientes
- El agente no ejecutó inspección visual en navegador ni en dispositivo: compilar y pasar las pruebas no acredita el aspecto final del selector web ni de las pestañas móviles. El funcionamiento manual lo verificó **el usuario** («realizó el recorrido manual de playlists y funcionó correctamente»), confirmación distinta de una inspección del agente.
- El invitado no tiene listado de playlists (GET de invitado → 405) por privacidad; el DJ es quien las consulta.
- Intervalo de envío entre canción y playlist comparte `ultimo_envio_at`: un envío de cualquiera retrasa el siguiente 30 s (decisión del diseño, cubierta por prueba).
- Sin WebSockets/polling: ambas pantallas usan carga/refresco manual, igual que las solicitudes.

---

# DJ Requests — Estado de la tarea 11 (retención automática de salas)

## Resumen
Retención automática en backend: `RetentionService` borra físicamente las salas con `purge_at <= now UTC` junto con su contenido temporal (sesiones, solicitudes y playlists) mediante las cascadas existentes, preservando DJ, perfil, foto, géneros y salas vigentes. `RetentionWorker` ejecuta el servicio en lotes desde un `BackgroundService`. Sin migraciones ni dependencias nuevas; `Retention:Enabled=false` por defecto.

## Archivos creados/modificados
- `backend/src/DjRequests.Api/Modules/Retention/Options/RetentionOptions.cs` — **nuevo**: `SectionName = "Retention"`, `Enabled`, `IntervalMinutes`, `BatchSize`.
- `backend/src/DjRequests.Api/Modules/Retention/Options/RetentionOptionsValidator.cs` — **nuevo**: `IntervalMinutes > 0` y `BatchSize` entre 1 y 1000; registrado con `ValidateOnStart`.
- `backend/src/DjRequests.Api/Modules/Retention/Services/IRetentionService.cs` — **nuevo**: `PurgeExpiredBatchAsync` devuelve `RetentionBatchResult(SelectedRooms, DeletedRooms)` (filas bloqueadas del lote y filas eliminadas).
- `backend/src/DjRequests.Api/Modules/Retention/Services/RetentionService.cs` — **nuevo**: en transacción corta, una sola consulta `SELECT ... ORDER BY purge_at, id LIMIT BatchSize FOR UPDATE SKIP LOCKED` sobre `salas` selecciona y bloquea las salas vencidas disponibles (las bloqueadas por otra transacción se omiten y el escaneo continúa con las siguientes); revalidación con `timeProvider.GetUtcNow()` **post-lock** y `ExecuteDeleteAsync` con guard `PurgeAt <= nowAfterLock` (cascadas eliminan sesiones, solicitudes y playlists).
- `backend/src/DjRequests.Api/Modules/Retention/Services/RetentionWorker.cs` — **nuevo**: `BackgroundService`; si está deshabilitado registra el motivo y no ejecuta pasadas; primera pasada inmediata al arrancar; drena lotes mientras el lote venga completo (`SelectedRooms >= BatchSize`) y se borre al menos una sala; una excepción se registra como fallo operativo y espera `IntervalMinutes` sin bucle de reintentos; las filas bloqueadas quedan para la siguiente pasada.
- `backend/src/DjRequests.Api/Program.cs` — registro de `RetentionOptions` con binding, validador `IValidateOptions` y `ValidateOnStart`; `IRetentionService` scoped; `AddHostedService<RetentionWorker>`.
- `backend/src/DjRequests.Api/appsettings.json` — sección `Retention`: `Enabled=false`, `IntervalMinutes=15`, `BatchSize=100`.
- `backend/tests/DjRequests.IntegrationTests/Fixtures/WebAppFactory.cs` — fuerza `Retention:Enabled=false` por defecto en memoria (permite override por prueba) para que User Secrets locales no activen la limpieza; `FakeTimeProvider` ahora implementa `CreateTimer`/`ITimer` para que `Advance` dispare timers (antes solo movía el reloj).
- Pruebas nuevas: `Modules/Retention/RetentionServiceTests.cs` (11 escenarios), `RetentionOptionsValidationTests.cs` (9 casos), `RetentionWorkerTests.cs` (5).

## Resultados reales
- **Build backend**: `dotnet build backend\DjRequests.slnx` correcto, 0 errores, 2 advertencias preexistentes (licencia Six Labors y CS0618).
- **Pruebas de retención**: `dotnet test ... --filter "FullyQualifiedName~Modules.Retention"` → **25/25 correctas** con PostgreSQL/Testcontainers (incluye revalidación post-lock con `RoomLockPauseInterceptor`, rollback con trigger `RAISE EXCEPTION`, concurrencia con `Task.WhenAll` y salto de filas bloqueadas con transacción independiente).
- **Suite completa**: `dotnet test backend\DjRequests.slnx --no-build` → **308/308 correctas**.
- **Git**: la tarea 11 quedó incluida en el commit `42fe587`; esta corrección de selección de lotes se registra en un commit posterior de esta misma rama.
- La limpieza **no** se ejecutó contra `dj_requests`; permanece deshabilitada por defecto.

## Límites y pendientes
- No hay endpoint ni UI que muestre al DJ la elegibilidad de borrado de sus salas (pendiente futuro).
- El borrado es físico e irreversible en salas vencidas; activarlo en un entorno con datos reales requiere revisión explícita.
- Sin cambios en `mobile/`, `web/`, `desktop/`, dependencias ni migraciones.

---

# DJ Requests — Estado de la tarea 10 (correcciones de perfiles y código de sala)

## Resumen
Dos correcciones acotadas sin tocar contratos API, autenticación, diseño, dependencias ni migraciones: la foto de perfil se orienta según su EXIF antes de borrar metadatos, y el código de sala se normaliza antes de validar el formulario de ingreso.

## Migración `AddDjProfiles`
- **Aplicada localmente a `dj_requests`, confirmada por el usuario** en esta tarea.
- Distinción importante: es una **confirmación del usuario**, no una comprobación ejecutada por este agente. No se volvió a aplicar, no se generó otra migración y no se consultó `__EFMigrationsHistory`; las pruebas de integración usan bases desechables creadas por Testcontainers, no `dj_requests`.

## Cambios
- `backend/src/DjRequests.Api/Modules/Profiles/Services/ProfileService.cs` — en `ProcessPhotoAsync` se aplica `image.Mutate(x => x.AutoOrient())` sobre los píxeles **antes** del redimensionado y de `StripMetadata`. Se conservan sin cambios los límites de tamaño, formatos permitidos, validación por contenido, dimensiones máximas y el reemplazo seguro de la foto anterior.
- `backend/tests/DjRequests.IntegrationTests/Modules/Profiles/ProfileEndpointsTests.cs` — prueba nueva `UploadPhoto_JpegWithExifOrientation_AppliesOrientationAndStripsExif`: JPEG 80×40 con EXIF `Orientation = 6` y mitades roja/azul; comprueba que el archivo guardado mide 40×80 (píxeles rotados), que el rojo queda arriba y el azul abajo y que `Metadata.ExifProfile` es `null`.
- `web/src/app/features/join-code/join-code.page.ts` — `onSubmit` calcula `normalizeCode(...)`, escribe ese valor en el `FormControl` y **después** comprueba `this.form.invalid`. El patrón `CODE_PATTERN` no cambia: solo se recortan espacios exteriores y se pasa a mayúsculas; los caracteres internos no válidos siguen rechazados.
- `web/src/app/features/join-code/join-code.page.spec.ts` — 5 pruebas nuevas: minúsculas con espacios exteriores, código mixto, caracteres no permitidos, espacio interno y longitud incorrecta tras recortar.

## Resultados reales
- **Build backend**: `dotnet build backend\DjRequests.slnx` correcto, 0 errores, 2 advertencias preexistentes (advertencias de licencia Six Labors en Debug/Release y CS0618 en `DatabaseFixture.cs`).
- **Pruebas Profiles con PostgreSQL/Testcontainers**: primera ejecución falló con `DockerUnavailableException` porque Docker no estaba en ejecución; se inició Docker Desktop (motor 27.0.3) y después `dotnet test ... --filter "FullyQualifiedName~Modules.Profiles"` → **41/41 correctas** (40 previas + 1 nueva de EXIF). No se modificó `dj_requests`.
- **Pruebas web**: `npm test -- --watch=false` → 7 archivos, **34/34 correctas** (29 previas + 5 nuevas).
- **Build Angular**: `npm run build` correcto; salida `web/dist/dj-requests-web`.
- **Git**: rama `feat/dj-profile`, sin commit ni push; cambios del árbol de trabajo previos conservados.

## Límites y pendientes
- **Pendiente conservado y no resuelto**: `SixLabors.ImageSharp` 4.1.3 exige licencia comercial para builds Release (los builds Debug siguen emitiendo las advertencias). No se resolvió ni se sustituyó la librería en esta tarea.
- No se ejecutó la suite completa de backend, solo el módulo Profiles.
- Sin cambios en `mobile/`, LAN, proxy, autenticación, contratos API ni migraciones.

---

# DJ Requests — Estado de la tarea 09 (rediseño web de invitados)

## Resumen
Rediseño de la web Angular de invitados según los mockups aprobados en `docs/mockups/web/`: nueva bienvenida en `/`, ingreso manual en `/join`, conservación de `/join/:code`, rediseño de sala y formulario, marca reutilizable y estados de movimiento reducido. Se conservaron sesión, contratos API, validaciones, idempotencia, paginación y navegación al perfil del DJ. No se modificaron backend, base de datos, `mobile/` ni dependencias.

## Cambios
- `web/src/app/shared/brand-logo/` — **nuevo**: marca Angular standalone con variantes `compact` y `hero`, geometría de `docs/brand/dj-requests-mark.svg` y arcos animados.
- `web/src/app/features/welcome/` — **nuevo**: bienvenida en `/`, tres pasos y único CTA a `/join`; sin API.
- `web/src/app/app.routes.ts` — `/` pasa a bienvenida; se añade `/join`; se conservan `/join/:code`, `/room/:roomId` y `/room/:roomId/dj`.
- `web/src/app/features/join-code/` — rediseño con flecha al inicio, marca central, campo visible, ayuda, placeholders y botón "Entrar a la sala"; se conserva la entrada automática por enlace y el manejo de errores.
- `web/src/app/features/room/` — cabecera con marca y "Conoce al DJ", estado/nombre/horario, nombre artístico cargado con `getDjProfile()` sin bloquear la sala, formulario con labels `.sr-only` y placeholders, listado compacto, actualización accesible y paginación.
- `web/src/app/features/dj-profile/` — marca compacta y enlace de sesión expirada a `/join`.
- `web/src/styles.scss` — Arial/sans, `color-scheme: dark`, `.sr-only`, botones de icono, foco, animación de entrada y `prefers-reduced-motion`.
- Pruebas nuevas/actualizadas: bienvenida, ingreso manual y por enlace, errores, nombre del DJ y tolerancia a fallo del perfil.
- `docs/DESIGN_SYSTEM.md` — componentes y pantallas web, excepción aprobada de labels ocultos y limitación de `hasEnded`.

## Resultados reales
- **Pruebas web**: `npm test -- --watch=false` -> 7 archivos, 29/29 correctas.
- **Build web**: `npm run build` correcto; salida `web/dist/dj-requests-web`.
- **Playwright + API simulada sobre el build**: bienvenida → `/join` → sala, `/join/:code` directo, nombre "DJ Luna", placeholders exactos, labels de 1×1 px, cero errores en formulario intacto, envío vacío bloqueado sin POST, envío correcto con un solo POST y formulario limpio, actualización accesible y ausencia de desbordamiento horizontal a 390, 320 y escritorio.
- **Movimiento reducido**: arcos con `animation: none`; contenido con duración mínima; en movimiento normal los arcos terminan visibles con opacidad 0.2.
- **Capturas**: `C:\Users\juand\AppData\Local\Temp\opencode\dj-web-redesign-shots`. No se realizó inspección visual humana ni del modelo sobre los PNG.
- **Git**: sin commit ni push. Se conservaron los cambios previos del árbol de trabajo.
- **Backend/BD en esa tarea**: sin cambios de código; en esa época la migración `AddDjProfiles` aún no estaba aplicada. Después se aplicó localmente, confirmada por el usuario (ver tarea 10).

## Límites y pendientes
- El modelo no puede interpretar visualmente los mockups; la implementación se basó en la especificación textual y OCR. La fidelidad visual completa requiere revisión humana de las capturas.
- El DTO de invitado no expone `hasEnded`; no se muestra "Finalizada" ni se infiere desde la hora. Si se requiere, debe añadirse el dato en backend en una tarea posterior.
- La prueba de navegador usó API simulada; no se ejecutó el recorrido manual contra PostgreSQL/API real.
- Sin cambios en `mobile/`, LAN, proxy ni versiones de dependencias.

---

# DJ Requests — Estado de la tarea 08 (beta LAN)

## Resumen
Preparación de la beta LAN para demostrar desde teléfonos en el mismo Wi-Fi: API en `0.0.0.0:5000`, web Angular en `0.0.0.0:4200` con proxy relativo, Expo web en `8081`, enlaces de invitación con la IP LAN y CORS restringido al origen exacto de Expo. La IP se inyecta por variables de entorno/User Secrets; no queda hardcodeada en la aplicación.

## Cambios
- `FrontendUrlOptions.AllowHttpLan` y validador: HTTP LAN deshabilitado por defecto, solo en `Development`, solo IPv4 privada RFC1918; se conservan URL absoluta, sin credenciales/query/fragmento y HTTPS obligatorio fuera de Development.
- `Program.cs`: orígenes adicionales de Expo configurables con `Cors:AdditionalOrigins`; se conserva `http://localhost:8081` y no se usa `AllowAnyOrigin`.
- `Properties/launchSettings.json`: perfil `http-lan` en `http://0.0.0.0:5000` (`Development`).
- `web/package.json`: script `start:lan` con `--host 0.0.0.0 --port 4200`; el proxy sigue apuntando a `127.0.0.1:5000`.
- `web/src/app/core/ids.ts`: UUID v4 con `crypto.getRandomValues` cuando `crypto.randomUUID` no existe; sin `Math.random`.
- `mobile/.env`: solo cambia `EXPO_PUBLIC_API_URL` a la IP LAN. `mobile/package.json`: script `web:lan`.
- `mobile/app/(app)/room/[id].tsx`: el QR y `Share` usan `room.inviteUrl`; se añade enlace seleccionable y mensaje de respaldo cuando compartir/copiar no está disponible.
- Pruebas nuevas: validador HTTP LAN, integración de `inviteUrl` con `Frontend:BaseUrl` LAN y generación de UUID v4.
- `docs/DEMO_LAN.md`: configuración, orden de arranque, URLs, cambio de IP, aislamiento escolar, firewall y retirada de reglas.

## Resultados reales
- **Backend**: 242/242 pruebas correctas con PostgreSQL 17 (Testcontainers).
- **Web**: `npm run build` correcto; `npm test -- --watch=false` -> 4 archivos, 17/17 correctas.
- **Móvil**: `npx tsc --noEmit` sin errores; `npx expo export --platform web` correcto (carga `EXPO_PUBLIC_API_URL`).
- **API LAN**: PID 27092, escucha en `0.0.0.0:5000`; `/health/live` y `/health/ready` responden 200 por `127.0.0.1` y `192.168.68.107`; `/api/generos` por LAN devuelve 11 géneros en JSON.
- **CORS**: preflight desde `http://192.168.68.107:8081` devuelve 204 con `Access-Control-Allow-Origin` exacto.
- **inviteUrl**: verificado por prueba de integración como `http://192.168.68.107:4200/join/{Codigo}`.
- **Firewall**: perfil activo Público; reglas documentadas, no aplicadas (requieren administrador).
- Sin migraciones, sin borrar datos, sin commit ni push.

## Pendiente
- Levantar Angular y Expo manualmente en terminales independientes y completar la prueba en teléfonos (QR, envío, actualización, aceptar/reproducir). No se declara probado en dispositivo físico.

---

# DJ Requests — Estado de la tarea 07 (web de invitados)

## Resumen
Primera versión funcional de la web de invitados en `web/` (Angular 21 standalone): entrada por código o enlace QR, sesión de invitado por sala, envío de solicitudes y listado de todas las canciones de la sala. No incluye registro/login de invitados, votos, edición de solicitudes ajenas, playlists, WebSockets ni polling.

## Cambios de backend
- `backend/src/DjRequests.Api/Modules/Genres/Dtos/GeneroDto.cs` - **nuevo**: `Id` + `Nombre`.
- `backend/src/DjRequests.Api/Modules/Genres/Endpoints/GeneroEndpoints.cs` - **nuevo**: `GET /api/generos`, solo activos, orden por nombre, `AsNoTracking`.
- `backend/src/DjRequests.Api/Program.cs` - registro de `MapGeneroEndpoints()`.
- `backend/src/DjRequests.Api/appsettings.Development.json` - `Frontend:BaseUrl` pasa a `http://localhost:4200`; el validador existente se conserva.
- `backend/tests/DjRequests.IntegrationTests/Modules/Genres/GeneroEndpointsTests.cs` - **nuevo**: activos ordenados y exclusión de inactivos.
- `backend/tests/DjRequests.IntegrationTests/Modules/Rooms/RoomEndpointsTests.cs` - `inviteUrl` verificado como `http://localhost:4200/join/{Codigo}`.

## Cambios de web
- `web/proxy.conf.json` y script `start` con `--proxy-config`: `/api` -> `http://127.0.0.1:5000` (solo desarrollo).
- `web/src/app/core/`: configuración, modelos, errores HTTP diferenciados (red, validación, 404, 409, 429 con `Retry-After`, cuota), sesión por sala en `sessionStorage`, servicios de invitado y géneros, utilidades de idempotencia y etiquetas.
- `web/src/app/features/join-code`, `web/src/app/features/room`, `web/src/app/features/not-found`: flujo completo, reactive forms, validaciones, paginación "Cargar más", "Actualizar", estados de carga/vacío/error y sesión caducada.
- `web/src/styles.scss` y tokens `--djr-*`: fondo oscuro, acento violeta, componentes base, responsive y `prefers-reduced-motion`.
- Pruebas: sesión por sala/reutilización/dedupe, formulario, normalización, reintento idempotente, éxito, cuota, paginación y reingreso.

## Resultados reales
- **Build web**: `npm run build` correcto; salida `web/dist/dj-requests-web`.
- **Pruebas web**: `npm test -- --watch=false` -> 3 archivos, 14/14 correctas (Vitest).
- **Build backend**: correcto, 0 errores y 1 advertencia preexistente (CS0618 en `DatabaseFixture`); se compiló en salida temporal porque la API en ejecución bloqueaba `bin\Debug`.
- **Pruebas backend**: 230/230 correctas con `dotnet vstest` sobre PostgreSQL 17 (Testcontainers).
- **Migraciones**: ninguna generada ni aplicada; `dj_requests` no fue tocada.
- **Dependencias**: sin altas ni actualizaciones; instalación con `npx --yes npm@11.6.2 install`.
- **Git**: rama `feat/guest-web`; sin commit ni push. `web/` sigue sin seguimiento.

## Comprobación manual
- **Pendiente**: no se ejecutó el recorrido visual completo. Las instancias abiertas preceden a estos cambios: la API en `5000` responde `404` a `/api/generos` y el servidor Angular en `4200` devuelve `index.html` para `/api/generos` (no tiene cargado el proxy nuevo). Para probar: reiniciar la API y ejecutar `npm start` en `web/`; después, código -> sala -> enviar canción -> verla en la app del DJ -> cambiar estado -> "Actualizar".
- **Límite conocido**: el enlace/QR usa `http://localhost:4200/join/CODIGO`, que no es accesible desde otro teléfono; la configuración LAN queda pendiente y no se desactivaron validadores.

---

# DJ Requests — Estado de la tarea 06 (correcciones de solicitudes)

## Resumen
Corrección de los 7 hallazgos de la demo de solicitudes: listado de invitados por sala, separación de errores 400/409, 429 con `Retry-After`, revalidación temporal tras adquirir ambos bloqueos, paginación segura, `Cache-Control: no-store` en todas las respuestas de invitado y actualización de documentación.

## Archivos creados/modificados
- `backend/src/DjRequests.Api/Modules/Common/NoStoreResult.cs` — **nuevo**: wrapper `IResult` reutilizable que fija `Cache-Control: no-store` antes de ejecutar el resultado interno.
- `backend/src/DjRequests.Api/Modules/Common/PaginationValidation.cs` — **nuevo**: validación compartida `page`/`pageSize` y cálculo de offset en `long` para evitar overflow de `int`.
- `backend/src/DjRequests.Api/Modules/Requests/Services/SolicitudService.cs` — listado de invitados por `SalaId`; `now` se toma después de bloquear sala y sesión/solicitud; versión obsoleta → `VersionObsoleta`; conflicto de concurrencia → `Concurrencia`; paginación protegida.
- `backend/src/DjRequests.Api/Modules/Requests/Endpoints/SolicitudEndpoints.cs` — 409 para `VersionObsoleta`/`Concurrencia`; `Retry-After` en 429 con `application/problem+json`; paginación → 400; `no-store` en endpoints de invitado.
- `backend/src/DjRequests.Api/Modules/Guests/Endpoints/GuestEndpoints.cs` — `no-store` en creación de sesión (201 y errores) y en la consulta de sala (200, 401 y 404).
- `backend/tests/DjRequests.IntegrationTests/Fixtures/SesionInvitadoLockPauseInterceptor.cs` — **nuevo**: pausa determinista antes del `FOR UPDATE` de `sesiones_invitado`.
- `backend/tests/DjRequests.IntegrationTests/Fixtures/WebAppFactory.cs` — cableado de `EnableSesionLockPause`/`SesionLockPause`.
- `backend/tests/DjRequests.IntegrationTests/Modules/Requests/SolicitudEndpointsTests.cs` — 34 pruebas: aislamiento por sala, paginación fuera de rango, 400 vs 409, `Retry-After`, caducidad/revocación durante la espera de bloqueo.
- `song-requests-demo.http`, `docs/DEMO_PRIMERA_ENTREGA.md`, `docs/ARQUITECTURA.md`, `backend/README.md` y `ESTADO.md` — documentación alineada (ruta `/api/guest-sessions`, listado por sala, 409, `Retry-After`).

## Resultados reales
- **Build API**: correcto, 0 errores (se compiló en salida temporal porque la API estaba en ejecución y bloqueaba `bin\Debug`).
- **Pruebas**: `228/228` correctas con `dotnet vstest` sobre PostgreSQL 17 (Testcontainers), incluida `GetRoom_UnknownToken_Returns401`.
- **TypeScript móvil**: `npx tsc --noEmit` sin errores.
- **Migraciones**: ninguna generada ni aplicada; `dj_requests` no fue tocada.
- **Git**: correcciones incluidas en la rama `feat/song-requests-demo`.

## Comprobación manual
- **Completada (21/10/2026)**: con la API reiniciada, el usuario confirmó en dispositivo el flujo de invitado (envío de una solicitud) y de DJ (recepción y cambio de estado).
- Sin pendientes funcionales para esta tarea; quedan fuera de esta comprobación manual los casos ya cubiertos por pruebas automáticas (404 entre salas, 409 con refresco de lista y `Cache-Control: no-store`).

---

# DJ Requests — Estado de la tarea 05 (Frontend: Sesión + Auth UI)

## Resumen
Corrección de restauración de sesión (bug 401 en `initialRestore`), centrado y unificación visual de Login/Registro con Design System, contenedor compartido `AuthScreenContainer`, validación de contraseña en registro.

## Causa raíz del 401 en initialRestore
`initialRestore` solo verificaba `storage.getAccessToken()`. Si no había access token pero sí un refresh token válido, marcaba `unauthenticated` inmediatamente sin intentar la renovación. Esto ocurría cuando el access token expiraba y solo quedaba el refresh token en almacenamiento.

## Archivos creados/modificados
- `mobile/src/components/AuthScreenContainer.tsx` — **nuevo**: contenedor full-screen fondo `#0B0D12`, `ScrollView` centrado vertical/horizontal, maxWidth 400, padding horizontal 24, `keyboardShouldPersistTaps`, safe area.
- `mobile/src/auth/provider.tsx` — `initialRestore`/`restoreSession`: si no hay access token pero sí refresh token → `refreshAuth()` antes de `getMe()`. `restoreError` tipado (`session_expired` | `network` | `generic`). Manejo diferenciado: 401/403 en refresh → `session_expired` (borra tokens, banner en login); `NetworkError` → `network` (no borra tokens, vista retry); otro → `generic` (login sin banner).
- `mobile/src/components/AppTextField.tsx` — tipos corregidos (`KeyboardTypeOptions`, `autoComplete`, `textContentType`) para pasar `tsc`.
- `mobile/app/(auth)/login.tsx` — usa `AuthScreenContainer`, banner `session_expired` ("Tu sesión caducó. Inicia sesión de nuevo."), placeholders `textSecondary`, enlace con `textPrimary`/`primary`, botón loading.
- `mobile/app/(auth)/register.tsx` — usa `AuthScreenContainer`, validación cliente `password.length >= 12` (error tras blur/submit), `helpText="Mínimo 12 caracteres"`, placeholders `textSecondary`, errores inline (eliminado `Alert.alert`), botón loading, enlace consistente.
- `mobile/app/(auth)/_layout.tsx` — `network` → vista retry con `AppButton`; `session_expired`/`generic` → Stack login (banner en login.tsx).
- `mobile/app/(app)/_layout.tsx` — mismo patrón: `network` retry con tokens del tema, `session_expired` redirige a login.

## Comandos ejecutados
```cmd
npx tsc --noEmit
npx expo export --platform web
```

## Resultados
- **TypeScript**: 0 errores (corregido error preexistente en `AppTextField.tsx`).
- **Export Web**: exitoso, bundle 2.6 MB, 27 archivos exportados a `dist/`.
- **Verificación manual pendiente**: login/registro centrados en pantalla alta/baja, teclado abierto, restauración con sesión válida, renovación con solo refresh token, banner sesión caducada, logout, error de red con retry. No ejecutado en navegador en esta sesión.

## Pendientes
- Verificación visual en navegador/dispositivo de todos los flujos de autenticación.
- Aplicar migración `SalaVersionConcurrencyToken` a `dj_requests` (tarea 03, decisión del usuario).

---

# DJ Requests — Estado de la tarea 04

## Resumen
- Tarea 01: estructura del backend, modelo de datos, migración inicial y SQL de revisión.
- Tarea 02: autenticación de DJs — endpoints, Identity, tokens, rate limiting por IP, tests.
- Tarea 03: creación y gestión de salas del DJ — endpoints `/api/rooms` con validación estricta, concurrencia con `FOR UPDATE`, código criptográfico, zona horaria IANA, inviteUrl configurable, pruebas e integración y migración de concurrencia.
- Tarea 04 (correcciones acotadas post-revisión): validación de esquema en `Frontend:BaseUrl` (0a), test concurrente de colisión forzada de código de sala (0b), y ajustes del contrato de invitados. Correcciones implementadas según especificación: ruta POST /api/guest-sessions, validación unificada, rate limiting, lectura bajo bloqueo, parser token canónico, validación código sala ASCII.

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
