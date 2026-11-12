# Arquitectura y entorno

## Estado comprobado en Windows
Node 22.22.0; npm 10.9.4; OpenCode 1.18.32; .NET SDK 10.0.401. Docker Engine 27.0.3 y Compose 2.28.1. Contenedor postgres:17 healthy. Base dj_requests, usuario administrador local dj_admin. Acceso host 127.0.0.1:5433, contenedor postgres:5432. Nunca incluir contraseña real en documentación.
global.json y carpetas backend, mobile, web, desktop, infrastructure, docs ya existen. compose.yaml y .env están en raíz. No reemplazarlos sin inspección. Angular 21.2 ya creado en `web/` (web de invitados, con Node 22.22.0); `desktop/` aún no creado.

## Componentes
Todos los clientes hablan con la API mediante HTTPS. Solo backend y tareas operativas autorizadas acceden a PostgreSQL. QR contiene URL del sitio y código de invitación, nunca credenciales del DJ. Código permite entrar a una sala, no administrar.
Una API modular: Accounts, Rooms, Requests, Playlists, Profiles, Retention; Administration en fase 3. EF Core maneja persistencia. Un proceso periódico del backend maneja expiración. Actualizaciones en tiempo real se incorporan en tarea posterior; se consulta la API al reconectar y no se usa el canal como única fuente de verdad.

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

### Sesiones de invitado (implementado)
- Credencial: 32 bytes de `RandomNumberGenerator` en Base64URL sin padding (43 chars). Se entrega solo al crear la sesión; en BD se persiste únicamente el hash SHA-256 (`sesiones_invitado.token_hash`, hex 64). Nunca se usa un UUID del cliente como autenticación.
- Creación: `POST /api/guest-sessions` con el código de sala. Transacción + `SELECT ... FOR UPDATE` sobre la sala; `now` post-lock revalida DJ activo, `purgeAt`, `endsAt` y estado `abierta`. Colisiones del índice único `token_hash` se detectan por SQLSTATE `23505` y se reintentan (hasta 5), sin preconsulta.
- Uso: `GET /api/guest/rooms/{roomId}` con `X-Guest-Token`. El backend revalida en cada petición (propiedad de sala, revocación, expiración, `endsAt`, `purgeAt`, DJ activo). El `expiresAt` del invitado se fija al crear (= `EndsAt` de la sala) y no cambia al reprogramar; el efectivo es `min(expiresAt, endsAt actual)`.
- Precedencia 401/404 (en `GuestSessionService`): pertenencia a la sala antes que revocación/expiración; sesión expirada (401) antes que sala finalizada (404). Un token de otra sala o una sala inexistente/finalizada/purgada o DJ suspendido → 404 genérico sin revelar destino; token ausente/malformado/desconocido/revocado/expirado → 401.
- Rate limiting declarativo: middleware `UseGuestSessionValidation` **antes** de `UseRateLimiter` popula `HttpContext.Items["GuestRateLimitPartition"]` (sesión válida → `session:{salaId}:{sesionId}`; inválida → IP con fallback `"unknown"`) sin tocar `HttpContext.User` ni cortar con errores. Los intentos inválidos consumen cuota por IP. `X-Forwarded-For` no se lee. Límites `guest-create` 60/min y `guest-get` 120/min, `QueueLimit = 0`, 429 con `Retry-After`.
- El token de invitado no autentica endpoints de DJ; un bearer de DJ no sustituye `X-Guest-Token`. `Cache-Control: no-store` en respuestas de invitado.

## Perfil del DJ (implementado)
- Endpoints privados con política `ActiveDj`: `GET/PATCH /api/dj/profile` y `POST/DELETE /api/dj/profile/photo`. La identidad propietaria procede del `NameIdentifier` autenticado; el cliente no envía un `djId` confiable.
- Endpoint invitado: `GET /api/guest/rooms/{roomId}/dj` con `X-Guest-Token`. Reutiliza la validación de sesión y solo devuelve nombre artístico, descripción, año, URL de foto y enlaces sociales. La respuesta usa `Cache-Control: no-store`.
- Fotos públicas: `GET /api/profile-photos/{fileName}` solo admite nombres internos generados por el servidor. El almacenamiento se configura mediante `FileStorage`; por defecto queda en `storage/profiles`, fuera de Git y del directorio publicado. No se guardan imágenes en PostgreSQL ni Base64 en JSON.
- Procesamiento: JPEG/PNG, 5 MB, máximo 50 millones de píxeles de origen, dimensión final máxima de 2048 px y eliminación de EXIF/IPTC/XMP. El reemplazo guarda primero la nueva imagen y conserva la anterior si falla la persistencia.
- El backend usa `SixLabors.ImageSharp` 4.1.3. Los builds Debug funcionan; antes de publicar un build Release debe resolverse su requisito de licencia comercial o sustituirse por una alternativa compatible.

## Retención y operación
Usar UTC, TimeProvider inyectable y timestamptz. Persistir zona IANA del evento para presentación. Ventana de retención: 168 horas exactas tras ends_at.

Retención implementada en `Modules/Retention`: `RetentionService`, en transacción corta, selecciona y bloquea en **una sola consulta** `SELECT ... ORDER BY purge_at, id LIMIT BatchSize FOR UPDATE SKIP LOCKED` las salas con `purge_at <= now`: `SKIP LOCKED` omite las filas que otra transacción ya bloquea y sigue el escaneo, de modo que el lote se llena con las siguientes salas vencidas disponibles; después revalida la caducidad con el reloj **bajo bloqueo** y borra con `ExecuteDeleteAsync`; las cascadas existentes eliminan sesiones, solicitudes y playlists y se preservan DJ, perfil, foto y géneros. `RetentionWorker` (`BackgroundService`) ejecuta una pasada inmediata al arrancar y después cada `Retention:IntervalMinutes`, drenando lotes de `Retention:BatchSize` mientras el lote venga completo (`SelectedRooms >= BatchSize`) y se borre al menos una sala; las filas bloqueadas por otras instancias quedan para la siguiente pasada; un fallo se registra y espera al siguiente intervalo sin bucle de reintentos. Configuración `Retention` con `Enabled=false` por defecto (`IntervalMinutes=15`, `BatchSize=100`) validada al arranque. En producción objetivo de borrado físico dentro de 15 minutos bajo operación normal. La API bloquea acceso al llegar purge_at aunque el worker falle. Tras reinicio procesa atrasados. Coordinación segura entre instancias mediante SKIP LOCKED y revalidación bajo bloqueo.
No confundir volumen persistente con backup. Definir expiración de respaldos antes de producción y volver a purgar datos vencidos antes de reabrir una restauración. No afirmar que borrar filas borra inmediatamente respaldos existentes.
Auditoría sin nombres de canciones, enlaces, payloads, tokens o correos copiados. Métricas operativas sin historial musical. Health endpoints nunca revelan credenciales ni excepciones internas.

## Fuentes verificadas para este paquete
- https://opencode.ai/docs/rules/ — AGENTS.md en raíz y lectura explícita de archivos referenciados.
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-custom-storage-providers?view=aspnetcore-10.0 — Identity y almacenamiento.
- https://hub.docker.com/_/postgres — imagen oficial e inicialización.
Verificar documentación vigente de EF Core y Npgsql al fijar dependencias. No considerar una versión no restaurada como validada.
