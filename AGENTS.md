# DJ Requests — reglas para OpenCode

## Antes de actuar
Lee `docs/REQUISITOS.md` y `docs/ARQUITECTURA.md`. Para cambios de datos lee también `docs/BASE_DE_DATOS.md`. Estos archivos no se cargan solos: ábrelos. Inspecciona el repositorio y su estado Git antes de modificarlo. Trabaja solo en la tarea solicitada y conserva los cambios del usuario.

## Contexto
Un desarrollador trabaja en Windows, con OpenCode en terminal. El proyecto está en `C:\Users\juand\dj-requests`. Tiene acceso a Android, iPhone y Mac. Explica comandos para la terminal indicada (CMD por defecto); no mezcles sintaxis de Bash o PowerShell. Responde y documenta en español; usa identificadores de C# y TypeScript en inglés, tablas y columnas según el diseño acordado.

## Reglas de producto
- DJs registrados; invitados sin cuentas ni contraseñas.
- Nombre de canción obligatorio; artista, género y enlace opcionales.
- Invitados ven canciones de su sala, sin votos ni edición de peticiones ajenas.
- Playlists privadas para el DJ propietario: proteger consultas, respuestas y eventos, no solamente pantallas.
- No reproducir audio, integrar Spotify, importar canciones de playlists ni inferir géneros.
- No agrupar títulos manuales automáticamente. No mantener historial musical tras la eliminación.
- Sala y contenido temporal se eliminan siete días después del fin programado; no desde su creación.
- El backend valida permisos, propiedad, límites y fechas. Nunca confiar en IDs o estados enviados por clientes.

## Arquitectura
API ASP.NET Core .NET 10, EF Core 10 y proveedor Npgsql compatible; PostgreSQL 17. SDK fijado en global.json: 10.0.401, latestPatch. No actualizar versiones ni global.json incidentalmente. Dependencias estables, sin comodines de versión ni previews; verificar compatibilidad y fijar versiones concretas restaurables.
React Native/TypeScript para móvil; Angular soportado para web; Electron+Angular es propuesta para escritorio, pendiente de fijación. No crear estas aplicaciones durante una tarea de base de datos.
Un backend modular y una base compartida. Sin microservicios, Redis, Kubernetes, CQRS ni repositorios genéricos por anticipado.

## Datos y seguridad
EF Core migrations es la fuente ejecutable del esquema. No duplicar DDL en scripts de init de Docker. No usar EnsureCreated. No ejecutar migraciones automáticamente al arrancar la API. Proponer y revisar cambios destructivos antes de aplicarlos.
Usar Identity para credenciales de DJs; no implementar hash de contraseñas propio. No inventar tokens ni autenticación simplificada para completar una demo. Administradores sin registro público.
No mostrar secretos, archivos .env completos, cadenas de conexión o tokens en respuestas, logs o commits. .env es local, .env.example solo contiene placeholders. No ejecutar `docker compose config` sin `--quiet` al reportar configuración.
No borrar volúmenes ni ejecutar `docker compose down -v`, resets o force pushes sin autorización concreta. No publicar, hacer push ni crear cuentas externas salvo instrucción explícita.
DTOs explícitos; nunca devolver entidades de EF directamente. Validar enlaces HTTP/HTTPS; no descargarlos ni generar previews desde el servidor. No cargar HTML de usuarios.

## Método de trabajo y finalización
Expón un plan breve y ejecuta el alcance autorizado. Solo pregunta si una ambigüedad cambia el producto o implica riesgo real; resuelve convenciones normales con estos documentos.
Haz pruebas de restricciones, aislamiento de salas, privacidad, caducidad y concurrencia cuando correspondan. No sustituir PostgreSQL por EF InMemory para verificar su esquema.
Reporta archivos modificados, comandos ejecutados, resultados reales, pendientes y siguiente paso. Distingue código generado de código compilado, migración generada de aplicada y pruebas ejecutadas de propuestas. Si falla una herramienta, conserva el error y no inventes un resultado exitoso.
