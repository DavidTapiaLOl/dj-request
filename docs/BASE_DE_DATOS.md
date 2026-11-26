# Diseño de datos — propuesta técnica v1

PostgreSQL 17, esquema public. Fuente ejecutable: migraciones EF Core. `20261006034357_InitialCore` fue aplicada localmente a `dj_requests` por el usuario; `20261016003450_SalaVersionConcurrencyToken` (vacía: solo anota `version` de `salas` como concurrency token en el modelo) y `20261108212801_AddDjProfiles` fueron generadas y, según confirmación del usuario, aplicadas localmente; este agente no las aplicó. SQL de revisión en `backend/artifacts/SalaVersionConcurrencyToken.sql` y `backend/artifacts/AddDjProfiles.sql`. Este documento diseña el modelo. UUID para PK; timestamptz para fechas UTC; snake_case en tablas/columnas. NOT NULL salvo marcado nullable. Longitudes se validan también en API. Restricciones con nombres explícitos.

## 1. djs — identidad permanente
Mapear DjAccount : IdentityUser<Guid> a djs. Conservar todas las propiedades necesarias de Identity (normalización, password_hash, security_stamp, concurrency_stamp, email_confirmed, bloqueo, intentos fallidos y campos de teléfono/2FA), aunque la UI inicial no use todas. No sustituirlas por un password varchar propio.
Campos de negocio: nombre_artistico varchar(100), estado varchar(16) default activo CHECK IN (activo,suspendido), created_at timestamptz, updated_at timestamptz.
Email requerido; exigir email normalizado único, además de índice de usuario normalizado único de Identity. UserName se establece al correo por servicio, no por datos del invitado. Mantener hash y configuración de Identity. Tabla djs contiene exclusivamente DJs; permisos de administración no se conceden aquí.
Identity necesita tablas auxiliares de claims, logins, tokens y, si se habilitan roles, roles/relaciones. No cuentan como cuentas adicionales de invitados. Preferir mappings estándar de Identity y documentar nombres exactos generados; no eliminar campos/tablas para cumplir artificialmente el listado resumido.
No sembrar cuentas/passwords de prueba en migración.

### 1.1 perfiles_dj
Una fila opcional por DJ. El nombre artístico permanece en `djs.nombre_artistico`; no se duplica.

| Columna | Tipo | Regla |
|---|---|---|
| id | uuid | PK |
| dj_id | uuid | FK djs(id), ON DELETE CASCADE, UNIQUE |
| descripcion | varchar(1500) nullable | Texto plano; vacíos normalizados a null |
| anio_inicio | integer nullable | CHECK entre 1900 y 2100; API limita además al año actual |
| foto_nombre_archivo | varchar(260) nullable | Nombre interno generado; no ruta cliente ni contenido binario |
| red_instagram / red_facebook / red_tiktok | varchar(2048) nullable | URL HTTPS validada por dominio en API |
| red_youtube / red_soundcloud | varchar(2048) nullable | URL HTTPS validada por dominio en API |
| created_at / updated_at | timestamptz | UTC |

Índice único `IX_perfiles_dj_dj_id`. La foto se almacena fuera de PostgreSQL; la fila conserva únicamente el nombre interno. El endpoint de invitado resuelve el perfil mediante la sala y nunca acepta un `dj_id` como autoridad del cliente.

## 2. salas
| Columna | Tipo | Regla |
|---|---|---|
| id | uuid | PK |
| dj_id | uuid | FK djs(id), ON DELETE RESTRICT |
| nombre | varchar(120) | trim no vacío |
| codigo | varchar(12) | Único; código aleatorio criptográfico de 10 caracteres mayúsculos sin ambiguos |
| estado | varchar(16) | abierta, pausada, cerrada; default abierta |
| starts_at | timestamptz | Inicio programado |
| ends_at | timestamptz | Mayor que starts_at |
| timezone | varchar(64) | Zona IANA validada en API; default America/Mexico_City |
| purge_at | timestamptz | ends_at + 168 horas, calculado por servicio |
| max_solicitudes_sesion | integer | Default 10, CHECK > 0 |
| max_playlists_sesion | integer | Default 2, CHECK > 0 |
| intervalo_envio_segundos | integer | Default 30, CHECK >= 0 |
| version | bigint | Concurrency token gestionado por aplicación, inicial 1 |
| created_at / updated_at | timestamptz | UTC |

CHECK purge_at > ends_at; servicio y pruebas garantizan diferencia exacta de 168 horas. Cambiar ends_at y purge_at atómicamente. Version incrementa en toda modificación y sirve para detectar actualizaciones simultáneas.
Índices: UNIQUE(codigo), (dj_id, created_at DESC), (purge_at). Código se genera con reintento ante colisión UNIQUE, nunca MAX+1 ni random predecible. QR se genera desde URL y código; no almacenar PNG.
La autorización de envíos comprueba estado y reloj además de columnas: now < ends_at, now < purge_at, DJ activo. FK RESTRICT para DJ evita borrados masivos accidentales; eliminación de cuenta tendrá flujo explícito posterior.

## 3. sesiones_invitado
id uuid PK; sala_id uuid FK salas(id) ON DELETE CASCADE; token_hash varchar(64) UNIQUE (SHA-256 hexadecimal de token aleatorio de al menos 256 bits); created_at, expires_at timestamptz; revoked_at timestamptz nullable; ultimo_envio_at timestamptz nullable.
CHECK expires_at > created_at. Servicio limita expires_at <= ends_at y comprueba estado de sala; una extensión del evento no revive tokens vencidos. Índice (sala_id). UNIQUE(sala_id,id) para relaciones compuestas de pertenencia.
Sin email, nombre, contraseña ni fingerprint. No guardar tokens originales ni IP permanente. Reingreso al mismo navegador puede reutilizar token vigente; una sesión distinta no equivale necesariamente a otra persona. Sesión de sala expirada/revocada devuelve error y no lista contenido.

## 4. generos
id smallint PK con IDs estables; nombre varchar(60) UNIQUE; activo boolean default true. Catálogo permanente no contiene historial musical.
Semilla propuesta: 1 Pop, 2 Rock, 3 Reguetón, 4 Regional mexicano, 5 Electrónica, 6 Salsa, 7 Cumbia, 8 Bachata, 9 Hip-hop/Rap, 10 R&B, 11 Otro. No crear No sé: dejar null. Es catálogo editable posteriormente, no clasificación exhaustiva.

## 5. solicitudes
| Columna | Tipo | Regla |
|---|---|---|
| id | uuid | PK |
| sala_id | uuid | FK salas ON DELETE CASCADE |
| sesion_id | uuid | Pertenencia mediante FK compuesta |
| nombre | varchar(200) | CHECK btrim(nombre) <> '' |
| artista | varchar(200) nullable | Vacíos normalizados a null |
| genero_id | smallint nullable | FK generos ON DELETE RESTRICT |
| enlace | varchar(2048) nullable | HTTP/HTTPS validado en API |
| estado | varchar(16) | pendiente, aceptada, reproducida, rechazada; default pendiente |
| idempotency_key | uuid | Generada por cliente para cada intento lógico |
| version | bigint | Concurrency token, inicial 1 |
| created_at / updated_at | timestamptz | UTC |

FK(sala_id,sesion_id) → sesiones_invitado(sala_id,id) ON DELETE CASCADE impide cruzar sesiones/salas incluso por SQL. PostgreSQL permite cascadas desde sala por ambas rutas; inspeccionar migración y probar borrado real. No convertirla en FK simple a sesion_id.
UNIQUE(sesion_id,idempotency_key) evita duplicar por reintento de red. Reintento con misma clave y mismo payload devuelve original sin consumir cuota; payload diferente devuelve conflicto. Rechazar claves inválidas. No UNIQUE(nombre), ni deduplicación automática por título.
Índices: (sala_id,created_at,id) y (sala_id,estado,created_at,id), (genero_id). Usar los índices existentes para counts por sesión antes de añadir otros.
DTO público incluye únicamente id, nombre, artista, género, enlace, estado y fecha. Se requiere sesión de esa sala para consultarlo. No filtrar playlists de una respuesta mezclada: consultas separadas.

## 6. playlists_compartidas
id uuid PK; sala_id uuid FK salas ON DELETE CASCADE; sesion_id uuid; nombre varchar(200) nullable; enlace varchar(2048) requerido/no vacío; idempotency_key uuid; created_at timestamptz.
FK compuesta (sala_id,sesion_id) → sesiones_invitado(sala_id,id) ON DELETE CASCADE. UNIQUE(sesion_id,idempotency_key). Índice(sala_id,created_at,id). Validación HTTP/HTTPS; no scraping ni importación. Invitado puede crear y recibe acuse mínimo, no endpoint de listado/detalle de playlists. Solo DJ propietario puede leerlas. No publicar sus datos en canal compartido.

## 7. administradores — base implementada, pendiente de aplicar en local
Cuenta interna separada de DJs/invitados. `AdminDbContext` usa Identity en otro contexto y tablas `admin_accounts`, `admin_user_claims`, `admin_user_logins` y `admin_user_tokens` en la misma base. `admin_accounts` conserva propiedades de Identity y añade `nombre` varchar(100) no vacío, `activo` boolean default true y `created_at` timestamptz; correo normalizado único (`IX_admin_accounts_normalized_email`) e índice de usuario normalizado propio (`IX_admin_accounts_normalized_username`) para no colisionar con `UserNameIndex` de `djs`. El historial de migraciones es `__EFMigrationsHistory_Admin`, distinto del de DJs.

Migración `AddAdminIdentity` generada y SQL de revisión en `backend/artifacts/AddAdminIdentity.sql`. **No aplicada a `dj_requests` por este agente**; debe revisarse y aplicarse explícitamente con `dotnet ef database update --context AdminDbContext ...`. No hay seed, roles ni passwords comunes en la migración. El alta es un procedimiento interno explícito: `dotnet run --project backend/src/DjRequests.Api -- --provision-admin`, con nombre, correo y contraseña solicitados de forma interactiva. No hay registro público de administradores. La política usa el esquema `AdminBearer`, distinto del bearer de DJs, y verifica cuenta activa y security stamp. Siguen pendientes segundo factor, auditoría, panels y revocación centralizada de refresh tokens.

## 8. auditoria_admin — fase 3, no migrar ahora
id uuid PK; administrador_id uuid nullable FK administradores ON DELETE SET NULL; accion varchar(80), tipo_recurso varchar(40), recurso_id uuid nullable (referencia informativa, no FK a sala); resultado varchar(20), created_at timestamptz. Índices por fecha y administrador/fecha. No almacenar payload libre, títulos, enlaces, correos ni tokens. Retención operativa separada propuesta de 90 días, revisar antes de producción.

## Concurrencia, limpieza y límites
Creación de contenido: transacción, bloquear sala para coordinar cierre/reprogramación/limpieza y sesión para cuota/intervalo. Orden de bloqueo consistente sala→sesión. Revalidar estado tras obtener bloqueo. Buscar idempotencia antes de consumir cuota. Cuenta total de solicitudes de sesión incluye rechazadas; no puede eludirse al cambiar estado.
Worker implementado (`Modules/Retention`): en transacción corta, una sola consulta `SELECT ... ORDER BY purge_at, id LIMIT BatchSize FOR UPDATE SKIP LOCKED` selecciona y bloquea las salas con `purge_at <= now` disponibles (las bloqueadas se omiten y el escaneo sigue con las siguientes); revalida `purge_at` con el reloj bajo bloqueo y borra con `ExecuteDeleteAsync`. Las cascadas existentes eliminan sesiones, solicitudes y playlists; se preservan DJ, perfil, foto y géneros. Configuración `Retention` (`Enabled=false` por defecto, `IntervalMinutes=15`, `BatchSize=100`) validada al arranque; el worker drena mientras el lote venga completo y se borre al menos una sala, y las filas bloqueadas quedan para la siguiente pasada; errores se registran y esperan al siguiente intervalo. Reloj inyectable para pruebas. No conservar snapshots; no logs de contenido. Pendiente: mostrar elegibilidad de borrado claramente al DJ.

## Pruebas de aceptación del esquema
1. Correo DJ normalizado duplicado falla; no hashes/credenciales en DTO.
2. Sala con ends_at <= starts_at falla; purge_at se recalcula al reprogramar.
3. Solicitud sin título o con estado desconocido falla.
4. Sesión de sala A referenciada por petición/playlist de sala B falla por FK.
5. Misma idempotency_key/sesión no crea segunda fila.
6. Borrar sala elimina contenido y sesiones; conserva DJ y catálogo.
7. Artista/género/enlace omitidos son null; playlist sin enlace falla.
8. Solo existe un perfil por DJ; borrar el DJ elimina su perfil; un invitado de sala A no consulta el perfil mediante una sesión de sala B.
Pruebas API cubiertas: acceso de otro DJ denegado (404), invitado no lee playlists (sin endpoint → 405), límite concurrente exacto (1×201 + 1×429), sala pausada no recibe (409) y a purge_at no hay acceso (404) aunque el worker esté detenido. Creación de playlist probada con idempotencia, cuota `max_playlists_sesion` e intervalo compartido con solicitudes.

## Migraciones y aplicación
`InitialCore` contiene las entidades iniciales e Identity auxiliar. `AddDjProfiles` añade únicamente `perfiles_dj`, su restricción, índice y FK. Revisar los SQL de `backend/artifacts/` antes de aplicar a `dj_requests`; la generación del archivo no significa que la migración haya sido aplicada. No usar EnsureCreated ni borrar volúmenes para resolver fallas. Tests en base independiente; jamás drop sobre dj_requests. Verificar backup antes de cambios con datos relevantes.
