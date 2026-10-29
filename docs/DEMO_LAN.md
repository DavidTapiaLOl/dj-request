# Demo LAN de DJ Requests (beta)

Guía para demostrar DJ Requests desde teléfonos conectados al mismo Wi-Fi que el equipo Windows. Es una beta de red local: no es un despliegue público, no incluye APK y no abre PostgreSQL ni el router.

## Red de este equipo

- IP Wi-Fi actual: `192.168.68.107`.
- Máscara: `255.255.252.0`; subred `192.168.68.0/22`.
- Perfil de red activo: **Público** (`DECO.FTF`, interfaz `Wi-Fi`).
- La API escucha en `0.0.0.0:5000`; Angular en `0.0.0.0:4200`; Expo web en `8081`.
- Los teléfonos deben estar en la misma subred y el firewall debe permitir los puertos TCP `4200`, `5000` y `8081`.

Comprobar la IP y el perfil antes de empezar:

```powershell
ipconfig
Get-NetConnectionProfile | Select-Object Name,InterfaceAlias,NetworkCategory
Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -eq "192.168.68.107" }
```

No cambies el perfil de red ni desactives el firewall. Si la IP cambia, actualiza la configuración como se indica más abajo.

## Configuración

La IP **no** está hardcodeada en el código de la aplicación. Se inyecta en tiempo de ejecución mediante variables de entorno o User Secrets; Angular usa rutas relativas `/api` y su proxy local; Expo lee `mobile/.env`.

### 1. API (PowerShell)

En una terminal independiente, desde la raíz del repositorio:

```powershell
Set-Location C:\Users\juand\dj-requests
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Frontend__BaseUrl = "http://192.168.68.107:4200"
$env:Frontend__AllowHttpLan = "true"
$env:Cors__AdditionalOrigins__0 = "http://192.168.68.107:8081"
dotnet run --project backend\src\DjRequests.Api --launch-profile http-lan
```

- `http-lan` fija `http://0.0.0.0:5000` y `Development`.
- `Frontend__AllowHttpLan` habilita HTTP LAN solo en `Development`, solo para IPv4 privadas RFC1918 (`10/8`, `172.16/12`, `192.168/16`). Fuera de `Development`, HTTPS sigue siendo obligatorio.
- `Cors__AdditionalOrigins__0` añade el origen exacto de Expo web; `http://localhost:8081` se conserva. No se usa `AllowAnyOrigin`.
- Si prefieres User Secrets:

```powershell
dotnet user-secrets set "Frontend:BaseUrl" "http://192.168.68.107:4200" --project backend\src\DjRequests.Api
dotnet user-secrets set "Frontend:AllowHttpLan" "true" --project backend\src\DjRequests.Api
dotnet user-secrets set "Cors:AdditionalOrigins:0" "http://192.168.68.107:8081" --project backend\src\DjRequests.Api
```

No imprimas el archivo de User Secrets ni la cadena de conexión.

### 2. Web Angular (PowerShell)

En otra terminal:

```powershell
Set-Location C:\Users\juand\dj-requests\web
npm run start:lan
```

El script ejecuta:

```text
ng serve --proxy-config proxy.conf.json --host 0.0.0.0 --port 4200
```

El navegador llama rutas relativas `/api/...`; el proxy reenvía a `http://127.0.0.1:5000` en la computadora. No cambies el destino del proxy a la IP LAN.

La validación de hosts de Vite se conserva y permite el acceso por dirección IP; no se desactiva ni se usa `--disable-host-check`. No uses `--allowed-hosts` sin valor: eso permitiría todos los hosts y no es necesario.

### 3. App del DJ / Expo web (PowerShell)

En otra terminal:

```powershell
Set-Location C:\Users\juand\dj-requests\mobile
npm run web:lan
```

Equivale a:

```text
expo start --web --host lan --port 8081
```

`mobile/.env` debe contener únicamente la URL de la API para esta demo:

```dotenv
EXPO_PUBLIC_API_URL=http://192.168.68.107:5000
```

Comprobación sin mostrar el archivo completo:

```powershell
Select-String -Path C:\Users\juand\dj-requests\mobile\.env -Pattern "^EXPO_PUBLIC_API_URL="
```

Si Expo ya estaba abierto, deténlo con `Ctrl+C` y vuelve a ejecutar `npm run web:lan` para que cargue el `.env` nuevo.

## Orden de arranque

1. **Docker / PostgreSQL**: desde la raíz, `docker compose up -d`. Comprueba con `docker compose ps` que `postgres:17` está healthy.
2. **API**: ejecuta la terminal de la API y espera a que `/health/ready` responda.
3. **Angular**: ejecuta `npm run start:lan` y espera a que compile.
4. **Expo**: ejecuta `npm run web:lan` y espera a que Metro termine.

No cambies los puertos de forma silenciosa. Si un puerto está ocupado, identifica el proceso con `Get-NetTCPConnection -State Listen -LocalPort 5000,4200,8081` y detén la terminal anterior del proyecto con `Ctrl+C`.

## URLs

| Uso | URL |
| --- | --- |
| API (salud) | `http://192.168.68.107:5000/health/live` y `/health/ready` |
| Web de invitados | `http://192.168.68.107:4200/` |
| Entrada por QR/enlace | `http://192.168.68.107:4200/join/CODIGO` |
| App del DJ (Expo web) | `http://192.168.68.107:8081/` |

En teléfonos **no uses** `localhost` ni `127.0.0.1`: apuntan al propio teléfono. El enlace y el QR de la sala usan el `inviteUrl` que devuelve el backend, construido con `Frontend:BaseUrl`; no se arma con una IP fija en el frontend.

## Verificación desde la computadora

```powershell
Invoke-RestMethod http://192.168.68.107:5000/health/live -TimeoutSec 5
Invoke-RestMethod http://192.168.68.107:5000/health/ready -TimeoutSec 5
Invoke-RestMethod http://192.168.68.107:4200/api/generos -TimeoutSec 5
Invoke-WebRequest http://192.168.68.107:8081 -UseBasicParsing -TimeoutSec 5 | Select-Object StatusCode
```

El endpoint `/api/generos` a través de Angular debe devolver JSON, no HTML.

Comprobar CORS para Expo web:

```powershell
$headers = @{
  Origin = "http://192.168.68.107:8081"
  "Access-Control-Request-Method" = "GET"
  "Access-Control-Request-Headers" = "authorization,content-type"
}
$response = Invoke-WebRequest -Method Options -Uri http://192.168.68.107:5000/api/generos -Headers $headers -UseBasicParsing -TimeoutSec 5
$response.Headers["Access-Control-Allow-Origin"]
```

Debe devolver `http://192.168.68.107:8081`.

## Si cambia la IP

1. Obtén la nueva IPv4 con `ipconfig`.
2. Actualiza `Frontend__BaseUrl` y `Cors__AdditionalOrigins__0` (o los valores equivalentes en User Secrets).
3. Actualiza `EXPO_PUBLIC_API_URL` en `mobile/.env`.
4. Reinicia API y Expo. Angular no necesita cambiar el proxy: sigue apuntando a `127.0.0.1:5000`.
5. Si cambia la subred, retira las reglas de firewall anteriores y créalas con el nuevo `remoteip`.

## Aislamiento entre dispositivos (red escolar)

Si el teléfono carga la web pero no conecta con la API, o no abre `8081`:

1. Confirma que ambos dispositivos están en el mismo SSID y en `192.168.68.0/22`.
2. Desde el teléfono, prueba `ping 192.168.68.107` (o una app de red equivalente).
3. Si hay internet pero no hay comunicación entre dispositivos, la red puede tener **AP isolation / client isolation**. No lo resuelve el firewall del PC.
4. Alternativas: usar un hotspot del propio equipo o un router/red doméstica sin aislamiento.
5. Evita redes de invitados escolares que bloquean tráfico entre clientes.

## Firewall de Windows

El perfil activo es **Público** (`DECO.FTF`). Las reglas deben limitarse a `192.168.68.0/22` y a TCP `4200`, `5000` y `8081`. No abras `5432` ni `5433` (PostgreSQL) y no configures port forwarding en el router.

Abre **PowerShell como administrador** y ejecuta:

```powershell
netsh advfirewall firewall add rule name="DJ Requests LAN TCP 5000" dir=in action=allow protocol=TCP localport=5000 remoteip=192.168.68.0/22 profile=public
netsh advfirewall firewall add rule name="DJ Requests LAN TCP 4200" dir=in action=allow protocol=TCP localport=4200 remoteip=192.168.68.0/22 profile=public
netsh advfirewall firewall add rule name="DJ Requests LAN TCP 8081" dir=in action=allow protocol=TCP localport=8081 remoteip=192.168.68.0/22 profile=public
```

Si el perfil cambia a Privado, vuelve a comprobarlo con `Get-NetConnectionProfile` y usa `profile=private` en lugar de `profile=public`. No cambies el perfil para “arreglarlo”.

Verificar:

```powershell
netsh advfirewall firewall show rule name="DJ Requests LAN TCP 5000"
Get-NetFirewallRule -DisplayName "DJ Requests LAN TCP *" | Select-Object DisplayName,Enabled,Direction,Action,Profile
```

Retirar las reglas al terminar la demo:

```powershell
netsh advfirewall firewall delete rule name="DJ Requests LAN TCP 5000"
netsh advfirewall firewall delete rule name="DJ Requests LAN TCP 4200"
netsh advfirewall firewall delete rule name="DJ Requests LAN TCP 8081"
```

Estas reglas requieren administrador. Si no las aplicaste, no aparecerán en la verificación. No afirmes que están activas sin comprobarlo.

## Prueba manual en teléfonos (pendiente)

1. Abrir `http://192.168.68.107:8081` en el teléfono del DJ.
2. Iniciar sesión y crear una sala abierta con fin futuro.
3. Escanear el QR de la sala desde otro teléfono.
4. Enviar una canción desde Angular.
5. Actualizar solicitudes en la app del DJ.
6. Aceptar la solicitud y marcarla como reproducida.
7. Actualizar la web del invitado y comprobar el estado.

## Límites de esta beta

- Solo red local; no es un despliegue público.
- Sin APK en esta tarea.
- Sin port forwarding, sin exponer PostgreSQL, sin desactivar el firewall.
- El enlace usa HTTP LAN solo en `Development`; producción sigue exigiendo HTTPS.
