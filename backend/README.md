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
La migración `InitialCore` y su SQL de revisión ya fueron generados:
- Migraciones: `backend/src/DjRequests.Api/Persistence/Migrations/`
- SQL idempotente (revisión): `backend/artifacts/InitialCore.sql`

**Estado de la migración:** la migración `20261006034357_InitialCore` **ya fue aplicada** a `dj_requests` localmente por el usuario (confirmación: `Done.`). Aplicación local confirmada por el usuario; esta información no procede de una verificación de esta ejecución. No volver a aplicarla ni modificar la migración inicial.

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
- Email: requerido, formato válido (EmailAddressAttribute), se hace trim
- Password: requerido, mínimo 12 chars (sin requisitos de complejidad; lo valida Identity)
- ArtistName: requerido, máximo 100 chars, se hace trim

Los errores de validación de campo usan `ValidationProblemDetails` con claves de campo (`Email`, `ArtistName`).

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
- 400: Datos inválidos
- 409: Email ya registrado (case-insensitive)
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
- 401: Credenciales inválidas o cuenta suspendida (mensaje genérico)
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
- 401: Token inválido, expirado, security stamp cambiado, usuario suspendido
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
- 401: Sin token, token inválido o expirado
- 403: Cuenta suspendida (aunque el token no haya expirado)

---

### Configuración de Autenticación

**Identity Options:**
- Password mínimo: 12 caracteres
- Sin requisitos de complejidad (dígitos, mayúsculas, símbolos)
- Lockout: 5 intentos fallidos -> 15 min de bloqueo
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

## Notas
- EF Core Migrations es la fuente ejecutable del esquema. No se duplica DDL en scripts de init de Docker.
- `EnsureCreated` **no** se utiliza. Migraciones solo manuales.
- DTOs explícitos (no se devuelven entidades EF directamente) — aplicar en endpoints futuros.
- Tablas Identity: `djs`, `roles`, `user_claims`, `user_logins`, `user_tokens`, `role_claims`, `user_roles` con columnas estándar (`Id`, `NormalizedEmail`, etc.). Se distingue entre pruebas de esquema (restricciones, índices, FKs, checks) y funciones pendientes (servicios como cálculo de purge_at, reprogramación, limpieza, etc.).
- No se muestran secretos en logs/respuestas. `.env` es local, `.env.example` solo placeholders.
- Versiones fijadas: EF Core 10.0.12, Npgsql 10.0.3, Identity EF 10.0.12, Testcontainers 4.15.0.
