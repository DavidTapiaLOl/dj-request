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

**IMPORTANTE**: La migración **NO se ha aplicado** a `dj_requests`. Revisar `InitialCore.sql` antes de aplicarla.

### 4. Aplicar migración manualmente (tras revisión)
```cmd
cd C:\Users\juand\dj-requests\backend\src\DjRequests.Api
dotnet ef database update
```

Este paso lo realiza el usuario tras validar el esquema. No se ejecuta automáticamente al arrancar la API.

### 5. Compilar
```cmd
cd C:\Users\juand\dj-requests
dotnet build backend\DjRequests.slnx --no-restore
```

### 6. Ejecutar tests (requiere Docker)
Las pruebas de integración usan Testcontainers con PostgreSQL 17 en contenedor independiente (base desechable). **No** se ejecutan contra `dj_requests`.

```cmd
cd C:\Users\juand\dj-requests
dotnet test backend\DjRequests.slnx --no-build
```

**Si Docker no está disponible**: la fixture de tests no omite explícitamente; Testcontainers fallará al intentar inicializar el contenedor (sin falsificar aprobación). Ver resultado real.

### 7. Health endpoints
- `GET /health/live` — 200 OK (proceso vivo)
- `GET /health/ready` — Verifica conectividad DB (`SELECT 1`). Retorna 200 OK o 503 Service Unavailable. **No acredita** que el esquema esté aplicado.

## Notas
- EF Core Migrations es la fuente ejecutable del esquema. No se duplica DDL en scripts de init de Docker.
- `EnsureCreated` **no** se utiliza. Migraciones solo manuales.
- DTOs explícitos (no se devuelven entidades EF directamente) — aplicar en endpoints futuros.
- Tablas Identity: `djs`, `roles`, `user_claims`, `user_logins`, `user_tokens`, `role_claims`, `user_roles` con columnas estándar (`Id`, `NormalizedEmail`, etc.). Se distingue entre pruebas de esquema (restricciones, índices, FKs, checks) y funciones pendientes (servicios como cálculo de purge_at, reprogramación, limpieza, etc.).
- No se muestran secretos en logs/respuestas. `.env` es local, `.env.example` solo placeholders.
- Versiones fijadas: EF Core 10.0.12, Npgsql 10.0.3, Identity EF 10.0.12, Testcontainers 4.15.0.