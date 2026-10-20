# Demo: Primera entrega — Solicitudes de canciones

Guía de 5 minutos para recorrer el flujo de solicitudes de canciones de extremo a extremo: DJ crea sala, invitado envía solicitudes, DJ las gestiona.

## Requisitos previos

- Docker Desktop en ejecución (PostgreSQL 17 vía Testcontainers o el compose local del proyecto).
- .NET SDK 10.0.401 (fijado en `global.json`).
- Node.js 20+ con npm.
- Una terminal CMD en la raíz del repositorio (`C:\Users\juand\dj-requests`).

## Paso 1: levantar la base de datos

```cmd
docker compose up -d postgres
```

La API **no** aplica migraciones automáticamente. Si la base está vacía, aplica las migraciones una vez:

```cmd
dotnet tool restore
dotnet ef database update --project backend\src\DjRequests.Api --startup-project backend\src\DjRequests.Api
```

> `dj_requests` es la base de datos local de desarrollo. Las pruebas de integración usan PostgreSQL aislado por Testcontainers y nunca tocan esta base.

## Paso 2: arrancar la API

```cmd
dotnet run --project backend\src\DjRequests.Api
```

La API queda disponible en `http://localhost:5025` (puerto de `launchSettings.json`).

## Paso 3: arrancar la app móvil (Expo)

```cmd
cd mobile
npm start
```

Escanea el QR con Expo Go (Android/iPhone) o pulsa `w` para la vista web. En la app:

1. Registra un DJ nuevo o inicia sesión.
2. Crea una sala con inicio y fin futuros; anota el **código** de 10 caracteres.
3. Abre la sala: verás la sección **Solicitudes**.

## Paso 4: recorrer el flujo (5 minutos)

### 4.1 Invitado envía solicitudes

En un navegador o desde la app de invitado, únete a la sala con el código. No requiere registro. Envía una canción:

- **Nombre obligatorio.**
- Artista, género y enlace (HTTP/HTTPS) opcionales.
- El cliente genera un `idempotencyKey` por intento lógico: si la respuesta se pierde y se reintenta, el servidor devuelve la solicitud original (200) en lugar de duplicarla.

Límites que puedes demostrar:

- **30 segundos** entre envíos del mismo invitado → segundo envío inmediato responde `429` con `Retry-After`.
- **10 solicitudes** máximo por sesión (configurable por sala) → al superarla responde `429`.
- Enlace no HTTP/HTTPS o género inexistente → `400` con detalle del error.

### 4.2 Invitado ve su lista

El invitado ve **solo sus propias** solicitudes (`GET /api/guest/rooms/{id}/requests`). No ve votos, ni ediciones, ni solicitudes ajenas.

### 4.3 DJ gestiona solicitudes

En la pantalla de sala del DJ, la sección **Solicitudes** muestra la lista en tiempo real (con pull-to-refresh). Para cada solicitud:

- **Aceptar**: `pendiente → aceptada`.
- **Reproducir**: `aceptada → reproducida`.
- **Rechazar**: `pendiente/aceptada → rechazada`.

Transiciones inválidas (por ejemplo, `reproducida → aceptada`) responden `400`. Cada actualización requiere el `version` actual (token de concurrencia optimista); si otro cambio llegó primero, responde `400` por versión desactualizada y la lista se refresca.

### 4.4 Aislamiento y permisos

- Un DJ distinto **no** puede listar ni modificar solicitudes de la sala de otro (`404`, sin revelar existencia).
- Un invitado con token de otra sala recibe `404`.
- Sin token o con token malformado: `401`.

## Paso 5 (opcional): probar vía HTTP

Usa `song-requests-demo.http` en la raíz del repositorio (compatible con la extensión REST Client de VS Code). Sustituye las variables `PEGAR_AQUÍ_...` con los valores reales (access token, código, roomId, guestToken). El archivo cubre el flujo completo más los casos de error.

Nota: el puerto es `5025` (el de `launchSettings.json`), no 5000.

## Implementado en esta entrega

| Área | Detalle |
| --- | --- |
| API invitado | `POST/GET /api/guest/rooms/{roomId}/requests` con `X-Guest-Token`, rate limit por IP (`guest-create`/`guest-get`). |
| API DJ | `GET /api/rooms/{roomId}/requests` (filtros `estado`, `generoId`, paginación) y `PATCH .../requests/{id}` con transiciones validadas. |
| Servicio | Validación de nombre/enlace/género, idempotencia, cuota por sesión, intervalo entre envíos, bloqueo `FOR UPDATE` en el orden sala → sesión → solicitud, token de concurrencia `Version`. |
| Móvil (DJ) | Sección "Solicitudes" en el detalle de sala: lista, aceptar/reproducir/rechazar, estados vacíos/carga/error, pull-to-refresh y paginación. |
| Pruebas | 26 pruebas de integración de solicitudes sobre PostgreSQL aislado (creación, idempotencia, límites, token, aislamiento, transiciones, concurrencia). |

## No implementado (fuera de esta entrega)

- Aplicación web Angular para invitados (la demo de invitado usa la app Expo web o el `.http`).
- WebSockets / actualización en tiempo real: el DJ usa pull-to-refresh.
- Votos o edición de solicitudes por invitados (no contemplado en el MVP).
- Playlists (entrega posterior).
- Grabación de historial musical (por diseño: no se conserva tras la eliminación).

## Comandos de verificación ejecutados

```cmd
dotnet build backend\src\DjRequests.Api\DjRequests.Api.csproj --no-restore
dotnet test backend\tests\DjRequests.IntegrationTests\DjRequests.IntegrationTests.csproj --filter "FullyQualifiedName~Modules.Requests"
cd mobile
npx tsc --noEmit
npx expo export --platform web
```

Resultado de la suite completa de integración: 219/220 correctas. La única falla (`GetRoom_UnknownToken_Returns401`) es preexistente: se reprodujo también contra el código en `main` sin cambios de esta rama.
