# DJ Requests — Estado de la tarea 01

## Resumen
- Tarea 01: estructura del backend, modelo de datos, migración inicial y SQL de revisión. No aplicado a dj_requests.

## Versiones fijadas
| Paquete | Versión | Notas |
|---------|---------|-------|
| Microsoft.EntityFrameworkCore | 10.0.12 | LTS .NET 10 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | Compatible con EF Core 10.0.x |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.12 | Compatible con EF Core 10.0.x |
| Testcontainers.PostgreSql | 4.15.0 | Compatible .NET 10 |
| dotnet-ef (herramienta local) | 10.0.12 | Restaurada en backend/.config/dotnet-tools.json |

## Archivos creados/modificados
- `backend/DjRequests.slnx`, `backend/.config/dotnet-tools.json`
- `backend/src/DjRequests.Api/*` (Entidades, Mappings, Persistence, Modules/Health, Program.cs, configuración)
- `backend/src/DjRequests.Api/Persistence/Migrations/*` (InitialCore)
- `backend/artifacts/InitialCore.sql` (SQL idempotente de revisión, NO aplicado)
- `backend/tests/DjRequests.IntegrationTests/*` (Helpers, Fixtures, SchemaTests)
- `backend/README.md`, `docs/ESTADO.md`

## Comandos ejecutados
```cmd
dotnet new sln -n DjRequests -o backend
dotnet new webapi -n DjRequests.Api -o backend\src\DjRequests.Api --framework net10.0 --no-https
dotnet new xunit -n DjRequests.IntegrationTests -o backend\tests\DjRequests.IntegrationTests --framework net10.0
dotnet sln backend\DjRequests.slnx add backend\src\DjRequests.Api\DjRequests.Api.csproj backend\tests\DjRequests.IntegrationTests\DjRequests.IntegrationTests.csproj
dotnet add backend\src\DjRequests.Api package Microsoft.EntityFrameworkCore --version 10.0.12
dotnet add backend\src\DjRequests.Api package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.3
dotnet add backend\src\DjRequests.Api package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 10.0.12
dotnet add backend\src\DjRequests.Api package Microsoft.EntityFrameworkCore.Design --version 10.0.12
dotnet tool update dotnet-ef --version 10.0.12 --tool-manifest backend\.config\dotnet-tools.json
cd backend\src\DjRequests.Api; dotnet ef migrations add InitialCore --output-dir Persistence\Migrations
cd backend\src\DjRequests.Api; dotnet ef migrations script --output ..\..\artifacts\InitialCore.sql --idempotent
dotnet build backend\DjRequests.slnx --no-restore
```

## Resultados
- **Build**: Exitoso. 0 errores, 1 advertencia menor (CS0618 en Testcontainers, no bloqueante).
- **Migración**: Regenerada en `backend/src/DjRequests.Api/Persistence/Migrations/20261006034357_InitialCore.*`, SQL idempotente exportado a `backend/artifacts/InitialCore.sql`. **NO aplicado** a `dj_requests`.
- **Tests**: 15 pruebas de integración ejecutadas con PostgreSQL aislado via Testcontainers (postgres:17). Todas superadas (15/15). No se ejecutan contra `dj_requests`.

## Notas
- PK compuesta `(Id, SalaId)` en `SesionInvitado` para soportar FK compuestas hacia `Solicitud` y `PlaylistCompartida`.
- Tablas Identity: `djs`, `roles`, `user_claims`, `user_logins`, `user_tokens`, `role_claims`, `user_roles` (con columnas estándar de Identity y nombres PascalCase; `djs` reemplaza `AspNetUsers` en el contexto).
- Health ready: verifica conectividad DB (SELECT 1), **no** acredita que el esquema esté aplicado.
- `purge_at` calculado por servicio (ends_at + 168h) — solo persistido en BD.
- Restricciones: checks para estados DJ/sala, nombre sala no vacío, email no vacío, y validaciones estrictas en tests (SqlState/ConstraintName).

## Pendientes
- Aplicar migración manualmente tras revisar SQL.
- Ejecutar tests cuando Docker esté disponible.
- Implementar módulos siguientes según alcance (auth, worker limpieza, etc.) — fuera de esta tarea.