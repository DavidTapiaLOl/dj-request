# DJ Requests - App Móvil

## Descripción
App móvil para DJs construida con React Native + Expo + TypeScript, conectada al backend real. 
Demo para presentación mañana usando Expo Go, sin APK ni publicación.

## Estado de implementación
- [x] Pantallas de autenticación (Login, Registro)
- [x] Gestión de sesiones con expo-secure-store
- [x] Listado de salas con paginación
- [x] Crear sala con validación de fechas y zona horaria
- [x] Detalle de sala con QR y compartir
- [x] Cambio de estado (pausar/reabrir/cerrar)
- [ ] Probar en dispositivo físico
- [ ] Validar refresco de token

## Requisitos previos

### 1. Iniciar Docker/PostgreSQL
```cmd
cd C:\Users\juand\dj-requests
docker compose up -d
```
Verificar que el contenedor esté saludable:
```cmd
docker ps
```

### 2. Iniciar API en Development (con User Secrets)
```cmd
cd C:\Users\juand\dj-requests\backend\src\DjRequests.Api
dotnet run --environment Development
```
La API escuchará en http://0.0.0.0:5000 (configured en Program.cs con User Secrets).

### 3. Identificar IPv4 de Windows (para configurar el acceso desde el teléfono)
```cmd
ipconfig
```
Buscar "Dirección IPv4" del adaptador Wi-Fi o Ethernet (ej. 192.168.1.42).
Anote esta dirección - la usará en el siguiente paso.

### 4. Configurar variable de entorno en mobile
```cmd
cd C:\Users\juand\dj-requests\mobile
copy .env.example .env
```
Editar el archivo `.env` y reemplazar:
```
EXPO_PUBLIC_API_URL=http://192.168.1.42:5000
```

### 5. Iniciar Expo y abrir con Expo Go
```cmd
cd C:\Users\juand\dj-requests\mobile
npx expo start
```
- Escanear el código QR con el teléfono (misma red Wi-Fi)
- Usar la app Expo Go instalada

### 6. Verificar conectividad desde el teléfono
Abrir el navegador del teléfono y acceder:
```
http://192.168.1.42:5000/health/live
```
Debe responder con `"live"`.

## Recorrido de la demo

### 1. Registro
1. Abrir la app en Expo Go
2. Ir a "Registrarse"
3. Llenar: Nombre artístico, correo, contraseña (mín. 12 chars)
4. Presionar "Crear cuenta"
5. Se vuelve automáticamente al login con mensaje de éxito
6. Iniciar sesión con los datos registrados

### 2. Login
1. En la pantalla de login, ingresar correo y contraseña
2. Presionar "Entrar"
3. Si es exitoso, navega a "Mis salas"
4. Errores del backend se muestran en español (401 = credenciales inválidas, 429 = rate limit)

### 3. Mis salas
1. Se muestra lista de salas del DJ (paginada)
2. Si no hay salas, muestra botón "Crear sala"
3. Pull-to-refresh para actualizar
4. Error con botón "Reintentar" en caso de fallo de red

### 4. Crear sala
1. Presionar "Crear sala" en la lista o en el menú
2. Llenar el formulario:
   - **Nombre**: ej. "Mi Fiesta"
   - **Fecha de inicio**: seleccionar usando DatePicker (mostrará hora local del dispositivo)
   - **Fecha de fin**: debe ser POSTERIOR al inicio y al momento actual
   - **Zona horaria**: default "America/Mexico_City", visible al DJ
3. Las fechas se envían con offset explícito (ej. `2026-12-01T20:00:00-06:00`)
4. Al guardar, navega al detalle de la sala creada

### 5. Detalle de sala
1. Muestra: nombre, código, estado, fechas, caducidad
2. QR código generado localmente desde `inviteUrl`
3. Botón "Compartir código" - usa Share API de React Native
4. Nota discreta: "La web de invitados aún está pendiente. El QR no permite pedir canciones en esta entrega."
5. Botones de estado:
   - **Pausar**: PATCH /state con state="pausada"
   - **Reabrir**: PATCH /state con state="abierta" (solo antes del fin)
   - **Cerrar**: PATCH /state con state="cerrada" (confirma antes de ejecutar)
6. Si hay conflicto 409: "La sala cambió, datos actualizados" - recargar datos
7. Deshabilitar todos los cambios si `hasEnded` es true
8. No confundir estado "abierta" con evento todavía vigente

### 6. Cerrar sesión
1. En cualquier pantalla, ir a la opción de logout
2. Borra credenciales de expo-secure-store
3. Navega de vuelta a la pantalla de login
4. No afirma revocación del token en servidor

## Comandos útiles

### Ver IPv4 LAN de Windows
```cmd
ipconfig
```
Buscar en la sección de adaptadores "Wi-Fi" o "Ethernet" la "Dirección IPv4".

### Verificar salud de la API
Desde el navegador del teléfono:
```
http://<IPv4>:5000/health/live → "live"
http://<IPv4>:5000/health/ready → "ready" (si BD conectada)
```

### Probar endpoints con curl (desde el PC)
```cmd
curl http://127.0.0.1:5000/health/live
curl -X POST http://127.0.0.1:5000/api/auth/register -H "Content-Type: application/json" -d "{\"email\":\"dj@test.com\",\"password\":\"pass1234pass\",\"artistName\":\"DJ Test\"}"
curl -X POST http://127.0.0.1:5000/api/auth/login -H "Content-Type: application/json" -d "{\"email\":\"dj@test.com\",\"password\":\"pass1234pass\"}"
```

## Solución de problemas

### Problema: El teléfono no se conecta al API
- Verificar que ambos (PC y teléfono) estén en la misma red Wi-Fi
- Usar la IPv4 LAN del PC, NO localhost
- Desactivar temporalmente el firewall de Windows (o crear regla para red privada)
- Verificar que la API esté corriendo: `http://<IPv4>:5000/health/live`

### Problema: Error de autenticación (401)
- Verificar que el correo y contraseña sean correctos
- Si el token expiró, el sistema intenta refresh automático
- Si el refresh falla, navega de vuelta al login

### Problema: No se pueden crear salas
- Verificar que `endsAt > startsAt` y `endsAt > now`
- Que la fecha tenga offset explícito (Z o ±HH:mm)
- Que la zona horaria sea un identificador IANA válido

## Entrega

**Archivos creados en `mobile/`:**
- `app/` - Pantallas de React Navigation con Expo Router
- `src/api/` - Cliente API con DTOs y refresh coordinado
- `src/auth/` - Almacenamiento seguro con expo-secure-store
- `src/components/` - Componentes UI reutilizables
- `src/utils/` - Utilidades de fecha y manejo de errores
- `.env.example` - Ejemplo de configuración de variables de entorno
- `app.json` - Configuración de Expo
- `tsconfig.json` - Configuración de TypeScript
- `README.md` - Este documento

**Pendientes de probar en dispositivo físico:**
- [ ] Flujo completo: registro → login → crear sala → detalle/QR → pausar → reabrir → cerrar → logout → login
- [ ] Manejo de errores de red vs credenciales inválidas
- [ ] Refresco de token en tiempo real
- [ ] Conversión de zona horaria en el dispositivo

**Verificado automáticamente:**
- [x] Estructura de proyecto Expo + TypeScript + Expo Router
- [x] TypeScript compilation (con excepciones esperadas de tipos nativos)
- [x] Expo Doctor - 13/16 checks passed
- [x] Dependencias instaladas y compatibles con Expo Go
- [x] API client con refresh coordinado
- [x] Almacenamiento seguro de tokens
- [x] Todas las pantallas implementadas según DTOs del backend
- [x] Validación de fechas con offset explícito
- [x] QR code local desde inviteUrl
- [x] Share API para compartir enlaces
- [x] Manejo de conflicto 409 en estado de sala
- [x] Estados de sala: abierta, pausada, cerrada
- [x] Paginación en listado de salas
- [x] Validaciones backend alineadas (email, password minlength, artistName)