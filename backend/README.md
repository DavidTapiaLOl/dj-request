# DJ Requests — Backend

## Configuración inicial

### 1. Restablecer herramientas .NET
```cmd
cd C:\Users\juand\dj-requests\backend
dotnet tool restore --tool-manifest .config\dotnet-tools.json
```

### 2. Configurar User Secrets (desarrollo local)
Nunca se debe commitear la contraseña. Usar User Secrets:

```cmd
cd C:\Users\juand\dj-requests\backend\src\DjRequests.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5433;Database=dj_requests;Username=dj_admin;Password=TU_PASSWORD_AQUI"
```

- **Desde Docker (host postgres)**: `Host=postgres;Port=5432;Database=dj_requests;Username=dj_admin;Password=...`
- **Desde Windows/host local**: `Host=127.0.0.1;Port=5433;Database=dj_requests;Username=dj_admin;Password=...` (según compose.yaml)

ASP.NET Core **no** carga `.env` automáticamente. ConnectionStrings se leen desde User Secrets o variables de entorno.

### 3. Revisar migración y SQL
Las migraciones y su SQL de revisión ya fueron generados:
- Migraciones: `backend/src/DjRequests.Api/Persistence/Migrations/`
- SQL idempotente (revisión): `backend/artifacts/InitialCore.sql` y `backend/artifacts/SalaVersionConcurrencyToken.sql`

**Estado de las migraciones:**
- `20261006034357_InitialCore` **ya fue aplicada** a `dj_requests` localmente por el usuario (confirmación: `Done.`). No volver a aplicarla ni modificarla.
- `20261016003450_SalaVersionConcurrencyToken` fue generada con la Tarea 03 ( Rooms ) y **aún no aplicada**. Es una migración **vacía** (Up/Down sin operaciones): `IsConcurrencyToken()` sobre `salas.version` (bigint) es una anotación de modelo de EF Core, no genera DDL. Solo añade el registro en `__EFMigrationsHistory` y actualiza el snapshot. El SQL de revisión correspondiente está en `backend/artifacts/SalaVersionConcurrencyToken.sql`.

Aplicar esta migración pendiente (cuando el usuario lo decida, tras revisar el SQL):
```cmd
cd C:\Users\juand\dj-requests\backend\src\DjRequests.Api
dotnet ef database update --migration SalaVersionConcurrencyToken
```

### 4. Aplicar migraciones futuras (si las hubiera)
```cmd
cd C:\Users\juand\dj-requests\backend\src\DjRequests.Api
dotnet ef database update
```

En un entorno nuevo, este paso lo realiza el usuario tras validar el SQL de revisión. No se ejecuta automáticamente al arrancar la API. **No** repetir este comando para `20261006034357_InitialCore`, que ya está aplicada.

### 5. Compilar
```cmd
cd C:\Users\juand\dj-requests
dotnet build backend\DjRequests.slnx --no-restore
```

### 6. Ejecutar tests (requiere Docker)
Las pruebas de integración usan Testcontainers con PostgreSQL 17 (bases desechables, una por test). **No** se ejecutan contra `dj_requests`.

```cmd
cd C:\Users\juand\dj-requests
dotnet test backend\DjRequests.slnx --no-build
```

**Si Docker no está disponible**: la fixture de tests no omite explícitamente; Testcontainers fallará al intentar inicializar el contenedor (sin falsificar aprobación). Ver resultado real.

### 7. Health endpoints
- `GET /health/live` — 200 OK (proceso vivo)
- `GET /health/ready` — Verifica conectividad DB (`SELECT 1`). Retorna 200 OK o 503 Service Unavailable. **No acredita** que el esquema esté aplicado.

## Auth Endpoints

### POST /api/auth/register
Registra un nuevo DJ.

**Request:**
```json
{
  "email": "dj@example.com",
  "password": "passphrase-de-12-chars-minimo",
  "artistName": "DJ Example"
}
```

**Validaciones:**
- Email: requerido, formato válido (EmailAddressAttribute), se hace trim, máximo 256 chars
- Password: requerido, mínimo 12 chars (sin requisitos de complejidad; lo valida Identity). No se hace trim
- ArtistName: requerido, máximo 100 chars, se hace trim

Los errores de validación de campo usan `ValidationProblemDetails` (`application/problem+json`) con claves de campo consistentes: `Email`, `ArtistName` y `Password` (en login) o `RefreshToken` (en refresh).

**Response 201:**
```json
{
  "id": "guid",
  "email": "dj@example.com",
  "artistName": "DJ Example",
  "createdAt": "2026-10-12T10:00:00Z"
}
```

**Errores:**
- 400: Datos inválidos (`ValidationProblemDetails`)
- 409: Email ya registrado (case-insensitive), cuerpo `ProblemDetails` con `application/problem+json` (solo violación de unicidad de email; otras violaciones de integridad no se traducen a 409)
- 429: Rate limit excedido (5/min por IP del cliente)

---

### POST /api/auth/login
Inicia sesión y obtiene tokens.

**Request:**
```json
{
  "email": "dj@example.com",
  "password": "passphrase-de-12-chars-minimo"
}
```

**Response 200:**
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "eyJ...",
  "tokenType": "Bearer",
  "expiresIn": 900
}
```

- Access token: 15 min (900 seg)
- Refresh token: 7 días
- Usa tokens bearer opacos nativos de ASP.NET Core Identity

**Errores:**
- 400: Datos inválidos (`ValidationProblemDetails`; email con formato inválido o password vacío)
- 401: Credenciales inválidas, bloqueo por intentos fallidos o cuenta suspendida (mensaje genérico idéntico en todos los casos)
- 429: Rate limit excedido (10/min por IP del cliente)

---

### POST /api/auth/refresh
Renueva el par de tokens.

**Request:**
```json
{
  "refreshToken": "eyJ..."
}
```

**Response 200:** Igual que login.

**Limitaciones documentadas:**
- No implementa rotación de un solo uso: el refresh token anterior puede seguir siendo válido hasta su expiración
- No hay revocación individual ni sesiones por dispositivo
- Valida security stamp: si cambia, el refresh es rechazado

**Errores:**
- 400: `refreshToken` vacío o ausente (`ValidationProblemDetails`, clave `RefreshToken`)
- 401: Token inválido, manipulado, expirado, security stamp cambiado o usuario suspendido (nunca 500)
- 429: Rate limit excedido (20/min por IP del cliente)

---

### GET /api/auth/me
Obtiene el perfil del DJ autenticado.

**Headers:**
```
Authorization: Bearer <accessToken>
```

**Response 200:**
```json
{
  "id": "guid",
  "email": "dj@example.com",
  "artistName": "DJ Example"
}
```

**Errores:**
- 401: Sin token, token inválido o expirado (cuerpo ProblemDetails `application/problem+json`; el challenge conserva `WWW-Authenticate`)
- 403: Cuenta suspendida (aunque el token no haya expirado; también ProblemDetails)

---

### Configuración de Autenticación

**Identity Options:**
- Password mínimo: 12 caracteres
- Sin requisitos de complejidad (dígitos, mayúsculas, símbolos)
- Lockout: 5 intentos fallidos -> 15 min de bloqueo. En .NET 10, al activarse el bloqueo `UserManager.AccessFailedAsync` reinicia `AccessFailedCount` a 0 y el estado queda en `LockoutEnd`
- Email único normalizado (case-insensitive)

**Token Options:**
- Access token: 15 minutos
- Refresh token: 7 días
- Tokens bearer opacos nativos (no JWT)
- `BearerTokenOptions.TimeProvider` se enlaza al `TimeProvider` de DI (en producción, `TimeProvider.System`)

**Rate Limits (configurables en appsettings.json):**
```json
"AuthRateLimits": {
  "RegisterPermitLimit": 5,
  "LoginPermitLimit": 10,
  "RefreshPermitLimit": 20,
  "WindowMinutes": 1
}
```

**Partición de los límites:** las políticas particionan con `RateLimitPartition.GetFixedWindowLimiter` usando `HttpContext.Connection.RemoteIpAddress` como clave:
- Es la dirección del cliente **conectado directamente**. Detrás de un proxy o balanceador será la IP de dicho intermediario, salvo que se configure explícitamente `ForwardedHeadersMiddleware` con sus `KnownProxies`/`KnownNetworks` y `ForwardLimit`; un despliegue detrás de proxy requiere esa configuración explícita para limitar por IP real.
- No se lee `X-Forwarded-For` directamente y no se confía en proxies arbitrarios: una cabecera `X-Forwarded-For` enviada por el cliente no altera la partición.
- Si `RemoteIpAddress` es `null`, se usa la partición compartida `"unknown"`, que **también tiene límite** (no es vía de evasión).
- El límite por IP es independiente del bloqueo por cuenta de Identity (5 intentos fallidos → 15 min), que sigue aplicándose por usuario.

**Respuesta 429:** cuerpo `ProblemDetails` con `Content-Type: application/problem+json` y cabecera `Retry-After` (segundos hasta la siguiente ventana), emitidos por `OnRejected`.

**Respuestas 401/403 sin cuerpo:** un middleware en `Program.cs` las completa con `ProblemDetails` RFC 9457 (`application/problem+json`) vía `IProblemDetailsService`, sin sobrescribir respuestas que ya lleven cuerpo. El challenge bearer añade `WWW-Authenticate`.

**Política ActiveDj:** Requiere DJ con `Estado = "activo"` en endpoints protegidos. Los schemes por defecto (authenticate/challenge/forbid) apuntan al scheme bearer.

---

### Data Protection Keys
Las claves de Data Protection (para firmar tokens) deben persistirse en volumen en producción. **No commitear claves a Git.**

En Docker: montar volumen en `/root/.aspnet/DataProtection-Keys` o configurar `PersistKeysToFileSystem`.

---

### Pendientes (fuera de esta tarea)
- Verificación de email (confirmación de registro)
- Recuperación de contraseña
- Logout y gestión de sesiones por dispositivo
- Revocación individual de refresh tokens

## Rooms Endpoints

Todos los endpoints de salas están bajo `/api/rooms`, requieren autenticación bearer (`Authorization: Bearer <accessToken>`) y la política `ActiveDj` (cuenta con `estado = "activo"`). Sin token → 401; cuenta suspendida → 403. Los errores de validación usan `ValidationProblemDetails`; los conflictos y no encontrados usan `ProblemDetails` (`application/problem+json`).

**Endpoints:** `POST /api/rooms`, `GET /api/rooms`, `GET /api/rooms/{id}`, `PUT /api/rooms/{id}`, `PATCH /api/rooms/{id}/state`.

### Contrato de fechas (startsAt, endsAt)

`startsAt` y `endsAt` son **ISO 8601 con offset explícito obligatorio**: terminan en `Z` (UTC) o llevan offset numérico `±HH:mm` / `±HHmm`. El parser es estricto (`TryParseExact`): exige fecha y hora completas con offset; no acepta texto extra ni formatos parciales.

| Entrada | Resultado |
|---|---|
| `2026-11-13T21:00:00-06:00` | válido |
| `2026-11-14T03:00:00Z` | válido; mismo instante que el anterior |
| `2026-11-13T21:00:00` | **400** — sin offset explícito |
| `2026-11-13` | **400** — sin hora ni offset |
| `2026-11-13T21:00:00-06:00 basura` | **400** — texto extra |
| fecha inválida | 400 |
| omitida o null | 400 |

- El offset determina el **instante**; se normaliza a UTC al persistir (`DateTimeOffset` en entidades y respuestas).
- `timezone` determina cómo se **presenta** ese instante al usuario; no se exige coincidencia con el offset recibido (una fecha en `Z` es válida para un evento en `America/Mexico_City`).
- El parsing es estricto en los endpoints; no se usa la zona local del servidor.
- Se conservan las validaciones de orden (`endsAt > startsAt`), caducidad (`endsAt > now`) y desbordamiento de `purgeAt` (pre-validación: `endsAt` no puede superar `DateTimeOffset.MaxValue - 168h`).

### POST /api/rooms
Crea una sala para el DJ autenticado.

**Request:**
```json
{
  "name": "Mi Fiesta",
  "startsAt": "2026-12-01T20:00:00-06:00",
  "endsAt": "2026-12-02T02:00:00-06:00",
  "timezone": "America/Mexico_City"
}
```

- `name`: obligatorio, 1–120 chars tras trim (no puede quedar vacío).
- `startsAt` / `endsAt`: obligatorios, ISO 8601 con offset explícito (ver contrato de fechas arriba). `endsAt` debe ser mayor que `startsAt` y posterior al momento actual.
- `timezone`: identificador IANA (validado con `TimeZoneInfo.TryConvertIanaIdToWindowsId`; se rechazan IDs Windows y desconocidos). **Omitido** → default `America/Mexico_City`; **vacío o inválido** → 400. Nunca hay fallback silencioso a UTC.

**Response 201** (`Location: /api/rooms/{id}`): todos los campos de la sala, con valores asignados por servidor:
- `id` y `codigo` generados por servidor (código de 10 caracteres de `[A-HJ-NP-Z2-9]`, sin I/O/0/1, generador criptográfico). Las colisiones del índice único `IX_salas_codigo` se detectan por SQLSTATE `23505` en `SaveChangesAsync` y se reintentan con un código nuevo (hasta 5 intentos); no se preconsultan códigos con `AnyAsync`.
- `estado`: `"abierta"`; `version`: `1`; `maxSolicitudesSesion`: 10; `maxPlaylistsSesion`: 2; `intervaloEnvioSegundos`: 30.
- `purgeAt`: `endsAt + 168 horas` exactas.
- `hasEnded` / `canReceiveRequests`: indicadores calculados con el servidor.
- `inviteUrl`: `{Frontend:BaseUrl}/join/{codigo}` (la base URL es configuración, no se deriva del header Host).

Campos extra enviados por el cliente (`id`, `codigo`, `estado`, `djId`, `version`, `purgeAt`, etc.) se **ignoran**: nunca controlan valores de servidor.

**Errores:** 400 (`ValidationProblemDetails`, claves `Name`, `StartsAt`, `EndsAt`, `Timezone`); 409 si no se puede generar código único tras 5 intentos.

### GET /api/rooms?page=1&pageSize=20
Lista las salas del DJ autenticado, excluyendo las purgadas (`purge_at > now`). Orden: `created_at` descendente. `page >= 1`; `pageSize` entre 1 y 100 (fuera de rango → 400).

### GET /api/rooms/{id}
Detalle de una sala propia, no purgada (`purge_at > now`). Sala ajena o purgada → 404 (mismo detalle, sin filtrar existencia). Después del fin pero antes de purge: 200 con `hasEnded: true`.

### PUT /api/rooms/{id}
Reprograma una sala. **Sin actualizaciones parciales**: requiere **todos** los campos (`timezone` puede omitirse; ver abajo).

**Request:**
```json
{
  "version": 1,
  "name": "Sala Renombrada",
  "startsAt": "2026-12-01T21:00:00-06:00",
  "endsAt": "2026-12-02T03:00:00-06:00",
  "timezone": "Europe/Madrid"
}
```

- `version`: obligatoria y debe ser un **entero positivo** (omitida, `0` o negativa → 400); positiva pero obsoleta → 409.
- `timezone`: **omitida** → default `America/Mexico_City` (misma regla que POST); **vacía o inválida** → 400.
- `startsAt` / `endsAt` siguen el contrato de fechas (offset explícito obligatorio, parser estricto).
- `purgeAt` se recalcula como `endsAt + 168h` (con pre-validación de desbordamiento).
- `codigo`, `estado` y columnas de límites no cambian.

**Concurrencia:** transacción explícita + bloqueo de fila `SELECT ... FOR UPDATE`; tras adquirir el bloqueo se toma la hora actual (`now` post-lock) como única fuente de verdad y se revalidan: propiedad, `purgeAt` (→404), `endsAt` persistido (→409), versión (→409), y que el **nuevo** `endsAt` sea posterior a `now` (→400). `version` distinta a la actual → 409. Dos PUT con la misma versión: exactamente uno 200 y otro 409, sin pérdida de datos.

**Errores:** 400 (validaciones); 404 (sala ajena/purgada); 409 (versión obsoleta, evento ya finalizado — `now >= endsAt`, o colisión de concurrencia).

### PATCH /api/rooms/{id}/state
Cambia el estado de una sala propia.

**Request:**
```json
{ "state": "pausada", "version": 1 }
```

- `state` permitido: `abierta`, `pausada`, `cerrada`. Transiciones entre estos tres estados permitidas antes del fin del evento; después del fin → 409.
- `version`: obligatoria y debe ser un **entero positivo** (omitida, `0` o negativa → 400); positiva pero obsoleta → 409.
- Mismo estado con versión válida → 200 con el estado actual **sin** incrementar `version`.
- Cambio de estado incrementa `version` y `updatedAt`; conserva fechas y `purgeAt`.
- Mismo mecanismo de transacción + `FOR UPDATE` y revalidación que PUT. Versión obsoleta → 409.

### Configuración Frontend (inviteUrl)
```json
"Frontend": {
  "BaseUrl": "https://localhost:3000"
}
```
- `appsettings.json`: `https://localhost:3000` (producción, HTTPS).
- `appsettings.Development.json`: `http://localhost:3000` (solo desarrollo).
- No se deriva del header Host. QR fuera de alcance.
- **Validación al arrancar** (`IValidateOptions` + `ValidateOnStart`): la URL debe ser absoluta, sin credenciales, query ni fragmento; HTTPS obligatorio fuera de Development; HTTP solo en localhost/loopback durante Development. Configuración inválida → fallo claro al iniciar (la API no sirve peticiones).
- La zona horaria predeterminada `America/Mexico_City` se verifica al iniciar; si no está disponible en el sistema, la API no arranca.

### Límites de sesión (asignados por servidor)
| Columna | Valor |
|---|---|
| max_solicitudes_sesion | 10 |
| max_playlists_sesion | 2 |
| intervalo_envio_segundos | 30 |

No son editables vía API en esta tarea.

---

## Guests Endpoints

Los endpoints de invitados están bajo `/api/guest`. **No requieren autenticación bearer**: el invitado se identifica con un token temporal en la cabecera `X-Guest-Token`. Los invitados no tienen cuenta ni contraseña.

**Endpoints:** `POST /api/guest/sessions`, `GET /api/guest/rooms/{roomId}`.

### POST /api/guest/sessions
Crea una sesión temporal de invitado a partir del código de la sala.

**Request:**
```json
{ "code": "ABCD2FGHJK" }
```

- `code`: obligatorio. Tras `Trim()` y `ToUpperInvariant()` debe tener exactamente 10 caracteres de `[A-HJ-NP-Z2-9]` (sin I, O, 0 ni 1). Formato inválido → 400 (`ValidationProblemDetails`, clave `Code`). Campos extra del cliente (`guestToken`, `expiresAt`, etc.) se **ignoran**.

**Response 201** (`Location: /api/guest/rooms/{roomId}`):
```json
{
  "guestToken": "…43 chars base64url…",
  "expiresAt": "2026-12-02T08:00:00Z",
  "room": { "id": "guid", "name": "Sala Test", "state": "abierta", "canReceiveRequests": true,
            "maxSolicitudesSesion": 10, "maxPlaylistsSesion": 2, "intervaloEnvioSegundos": 30, … }
}
```

- `guestToken`: 32 bytes aleatorios (`RandomNumberGenerator`) en Base64URL sin padding (43 chars). Es la **única** credencial entregada al invitado; **solo se persiste su hash SHA-256** (`token_hash`, hex 64). El token original no se guarda.
- `expiresAt`: se fija al crear como `EndsAt` de la sala y **no** cambia al reprogramar la sala. El efectivo es `min(expiresAt, endsAt actual)`.
- Transacción con `SELECT ... FOR UPDATE` sobre la sala; `now` post-lock revalida: DJ activo, `purgeAt`, `endsAt` y estado `abierta`. Colisiones del índice único `IX_sesiones_invitado_token_hash` se detectan por SQLSTATE `23505` y se reintentan (hasta 5 intentos); sin preconsulta `AnyAsync`.

**Errores:**
- 400: código con formato inválido.
- 404: sala inexistente, finalizada (`endsAt <= now`), purgada (`purgeAt <= now`) o DJ suspendido.
- 409: sala `pausada` o `cerrada`; o token no único tras 5 intentos.
- 429: rate limit `guest-create` (60/min por IP).

### GET /api/guest/rooms/{roomId}
Devuelve el estado mínimo de la sala para el invitado.

**Headers:**
```
X-Guest-Token: <guestToken>
```

**Response 200:**
```json
{
  "room": { "id": "guid", "name": "Sala Test", "state": "abierta", "canReceiveRequests": false, … },
  "expiresAt": "2026-12-02T08:00:00Z"
}
```

- `canReceiveRequests` es `false` si la sala está `pausada`/`cerrada` (una sala pausada sigue siendo legible, solo deja de recibir).
- `expiresAt` = `min(session.ExpiresAt, sala.EndsAt)`.

**Validación y códigos de estado** (el backend revalida en cada petición; nunca se confía en el token):
- 401: token ausente, mal formado (longitud ≠ 43 o Base64 inválido), desconocido, revocado (`revoked_at`) o expirado (`now >= expires_at`).
- 404: sala inexistente, **token de otra sala**, sala finalizada (`now >= endsAt`) o purgada, o DJ suspendido. El detalle es genérico y no revela la sala destino.

**Orden de precedencia** (definido en `GuestSessionService`): se comprueba pertenencia a la sala antes de revocación/expiración; entre sesión expirada (401) y sala finalizada (404), la expiración de la sesión tiene prioridad. Esto permite que al **acortar** el evento (sala finalizada con sesión aún vigente) se responda 404, y al **extender** el evento el `expiresAt` del invitado no se alarga.

### Validación previa al rate limiter (middleware)

`UseGuestSessionValidation` se ejecuta **antes** de `UseRateLimiter` y **no** escribe `HttpContext.User` ni corta la respuesta con errores de autenticación. Su único efecto es poblar `HttpContext.Items["GuestRateLimitPartition"]` para la política `guest-get`:
- Sesión válida → clave `session:{salaId}:{sesionId}`.
- Sesión inválida (ausente, malformada, desconocida o de otra sala) → `RemoteIpAddress` con fallback `"unknown"`.

La validación de la credencial se consulta **antes** de aplicar el límite, de modo que los intentos inválidos también consumen cuota (por IP). `X-Forwarded-For` no se lee y no altera la partición; se usa `Connection.RemoteIpAddress`. No se persiste IP ni se loggea `X-Guest-Token`.

### Rate Limits de invitados (configurables)
```json
"GuestRateLimits": {
  "PostPermitLimit": 60,
  "GetPermitLimit": 120,
  "WindowMinutes": 1
}
```

- `guest-create` (POST): 60/min por IP. `guest-get` (GET): 120/min por sesión (o por IP si la credencial es inválida). Ventana fija, `QueueLimit = 0`.
- 429 con cuerpo `ProblemDetails` (`application/problem+json`) y `Retry-After`.
- Límite declarativo con `RequireRateLimiting(...)` en cada endpoint; no se mezcla con contadores manuales.

### Seguridad y privacidad
- Solo se persiste `token_hash` (SHA-256 en hex); la tabla `sesiones_invitado` no guarda el token original.
- `Cache-Control: no-store` en respuestas de invitado; no se sirve audio ni previews.
- El token de invitado **no** autentica endpoints de DJ (protegidos por bearer + `ActiveDj`), y un bearer de DJ **no** sustituye `X-Guest-Token`.
- Una sesión pertenece a exactamente una sala; el endpoint no permite cruzar salas (404 sin revelar destino).

---

## Notas
- EF Core Migrations es la fuente ejecutable del esquema. No se duplica DDL en scripts de init de Docker.
- `EnsureCreated` **no** se utiliza. Migraciones solo manuales.
- DTOs explícitos (no se devuelven entidades EF directamente) — aplicar en endpoints futuros.
- Tablas Identity: `djs`, `roles`, `user_claims`, `user_logins`, `user_tokens`, `role_claims`, `user_roles` con columnas estándar (`Id`, `NormalizedEmail`, etc.). Se distingue entre pruebas de esquema (restricciones, índices, FKs, checks) y funciones pendientes (servicios como cálculo de purge_at, reprogramación, limpieza, etc.).
- No se muestran secretos en logs/respuestas. `.env` es local, `.env.example` solo placeholders.
- Versiones fijadas: EF Core 10.0.12, Npgsql 10.0.3, Identity EF 10.0.12, Testcontainers 4.15.0.
