# Web de invitados (Angular)

Aplicación web para invitados de DJ Requests. Permite entrar con el código de la sala (o con el enlace/QR), enviar una canción y ver todas las solicitudes de esa sala. No incluye registro, login, votos, edición de solicitudes ajenas ni playlists.

## Requisitos

- Node.js 22.22.0 (probado con esa versión).
- API en `http://localhost:5000` con PostgreSQL y las migraciones aplicadas.
- `Frontend:BaseUrl` en `backend/src/DjRequests.Api/appsettings.Development.json` igual a `http://localhost:4200` para que el enlace de invitación apunte a esta aplicación.

## Instalación

Desde `web/`, con la versión de npm usada para generar el `package-lock.json`:

```cmd
npx --yes npm@11.6.2 install
```

No regenerar el proyecto con `ng new` ni actualizar dependencias de forma incidental.

## Desarrollo

```cmd
npm start
```

Abre `http://localhost:4200/`. El script ejecuta `ng serve --proxy-config proxy.conf.json`, que reenvía `/api` a `http://127.0.0.1:5000` durante el desarrollo. El proxy es solo de desarrollo: no configura el despliegue de producción.

Para la beta LAN, con la API escuchando en `0.0.0.0:5000`:

```cmd
npm run start:lan
```

Esto abre el servidor en `0.0.0.0:4200` con el mismo proxy y rutas relativas `/api`. Consulta `../docs/DEMO_LAN.md` para la configuración del backend, Expo, firewall y prueba en teléfonos.

## Build y pruebas

```cmd
npm run build
npm test -- --watch=false
```

- Build: `dist/dj-requests-web`.
- Pruebas: Vitest, 3 archivos y 14 pruebas (sesión por sala, formulario, normalización, idempotencia, cuota y paginación).

## Rutas

- `/`: entrada manual del código.
- `/join/:code`: entrada por enlace o QR; intenta unirse automáticamente.
- `/room/:roomId`: sala del invitado; exige una sesión válida para esa sala.
- `*`: página no encontrada.

## Sesión e idempotencia

- `POST /api/guest-sessions` con el código crea la sesión; el token se guarda en `sessionStorage` por sala.
- Solo los endpoints de invitado envían `X-Guest-Token`.
- Una sesión caducada se borra y se pide reingreso explícito; no se crean sesiones automáticamente.
- Cada envío genera `idempotencyKey`. Si la red falla de forma incierta, el reintento reutiliza la clave; cambiar los datos genera una clave nueva.
- El formulario se limpia solo cuando el servidor confirma el envío.
- Se respetan `canReceiveRequests`, la cuota de la sesión y el `429` con `Retry-After`.

## API utilizada

- `POST /api/guest-sessions`
- `GET /api/guest/rooms/{roomId}`
- `POST /api/guest/rooms/{roomId}/requests`
- `GET /api/guest/rooms/{roomId}/requests?page&pageSize`
- `GET /api/generos` (catálogo activo, solo lectura)

## Límites conocidos

- El enlace/QR usa `http://localhost:4200/join/CODIGO`; `localhost` no es accesible desde otro teléfono. La configuración LAN queda pendiente y no se desactivaron los validadores de seguridad.
- No hay WebSockets ni polling: la lista se actualiza con "Actualizar" y "Cargar más".
- No hay despliegue de producción configurado para esta aplicación.
