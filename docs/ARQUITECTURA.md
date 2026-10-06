# Arquitectura y entorno

## Estado comprobado en Windows
Node 22.22.0; npm 10.9.4; OpenCode 1.18.32; .NET SDK 10.0.401. Docker Engine 27.0.3 y Compose 2.28.1. Contenedor postgres:17 healthy. Base dj_requests, usuario administrador local dj_admin. Acceso host 127.0.0.1:5433, contenedor postgres:5432. Nunca incluir contraseña real en documentación.
global.json y carpetas backend, mobile, web, desktop, infrastructure, docs ya existen. compose.yaml y .env están en raíz. No reemplazarlos sin inspección. Angular aún no creado; verificar requisitos de Node al escoger versión, no asumir que cualquier versión funciona con Node instalado.

## Componentes
Todos los clientes hablan con la API mediante HTTPS. Solo backend y tareas operativas autorizadas acceden a PostgreSQL. QR contiene URL del sitio y código de invitación, nunca credenciales del DJ. Código permite entrar a una sala, no administrar.
Una API modular: Accounts, Rooms, Requests, Playlists, Retention; Administration en fase 3. EF Core maneja persistencia. Un proceso periódico del backend maneja expiración. Actualizaciones en tiempo real se incorporan en tarea posterior; se consulta la API al reconectar y no se usa el canal como única fuente de verdad.

## Estructura propuesta del backend
- backend/DjRequests.slnx
- backend/src/DjRequests.Api/ : API, módulos, DbContext y migraciones en Persistence/Migrations.
- backend/tests/DjRequests.IntegrationTests/ : pruebas con PostgreSQL aislado.
- backend/.config/dotnet-tools.json : dotnet-ef local, versión compatible y fija.
Un proyecto API inicialmente, separado por carpetas/módulos. No crear cuatro capas vacías ni múltiples servicios sin necesidad.

## Credenciales y configuración
La cuenta dj_admin es administrativa de desarrollo, no cuenta de ejecución de producción. Más adelante usar propietario de migraciones y rol de aplicación con privilegios limitados (sin superuser, create role ni create database).
Compose lee .env; ASP.NET Core no lo carga automáticamente. La API usa ConnectionStrings:DefaultConnection mediante User Secrets local o variables de entorno. Documentar configuración local sin imprimir la contraseña. En Docker, host=postgres, port=5432; desde Windows, host=127.0.0.1, port=5433.
No añadir cargadores de dotenv por conveniencia sin necesidad. No persistir una cadena real en appsettings.json. HTTPS/CORS restringido para producción; credenciales DJ en almacenamiento seguro móvil cuando se implemente.

## Autenticación
Fase 1: ASP.NET Core Identity con Guid y usuario DjAccount mapeado a djs; no se comparte tabla con invitados. Conservar tablas auxiliares de Identity. El esquema permite tokens soportados por framework en tarea de autenticación posterior; no diseñar un JWT ad hoc ahora.
Fase 3: administradores separados, sin endpoints públicos de registro, autenticación robusta y control de privilegios. Modelo y auditoría están documentados; no crear credenciales de administrador de ejemplo.
Invitado: token opaco aleatorio, limitado a sala, expirable y revocable; guardar solo hash. No usar un UUID enviado por el cliente como autenticación.

## Retención y operación
Usar UTC, TimeProvider inyectable y timestamptz. Persistir zona IANA del evento para presentación. Ventana de retención propuesta: 168 horas exactas tras ends_at.
Worker cada 15 minutos; en producción objetivo de borrado físico dentro de 15 minutos bajo operación normal. API bloquea acceso al llegar purge_at aunque worker falle. Tras reinicio procesa atrasados. Limpieza transaccional por lotes y con bloqueo/coordinación segura entre instancias; comprobar fechas bajo bloqueo.
No confundir volumen persistente con backup. Definir expiración de respaldos antes de producción y volver a purgar datos vencidos antes de reabrir una restauración. No afirmar que borrar filas borra inmediatamente respaldos existentes.
Auditoría sin nombres de canciones, enlaces, payloads, tokens o correos copiados. Métricas operativas sin historial musical. Health endpoints nunca revelan credenciales ni excepciones internas.

## Fuentes verificadas para este paquete
- https://opencode.ai/docs/rules/ — AGENTS.md en raíz y lectura explícita de archivos referenciados.
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-custom-storage-providers?view=aspnetcore-10.0 — Identity y almacenamiento.
- https://hub.docker.com/_/postgres — imagen oficial e inicialización.
Verificar documentación vigente de EF Core y Npgsql al fijar dependencias. No considerar una versión no restaurada como validada.
