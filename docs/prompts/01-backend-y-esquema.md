# Tarea 01 — estructura del backend y migración inicial

Lee AGENTS.md, docs/REQUISITOS.md, docs/ARQUITECTURA.md y docs/BASE_DE_DATOS.md completos. Inspecciona global.json, compose.yaml, .gitignore y git status. No muestres .env ni credenciales. Estamos en Windows, CMD, .NET SDK 10.0.401. PostgreSQL ya funciona en Docker y dj_requests existe, sin tablas de negocio.

Implementa esta tarea de principio a fin, con un plan breve inicial:
1. Crea la solución backend/DjRequests.slnx, proyecto net10.0 backend/src/DjRequests.Api y proyecto de pruebas backend/tests/DjRequests.IntegrationTests. Conserva las demás carpetas.
2. Verifica en fuentes oficiales y en restore las versiones estables compatibles de EF Core 10, Identity EF 10 y Npgsql EF 10. Fija versiones concretas. Configura dotnet-ef como herramienta local de backend. No actualices global.json ni Node.
3. Configura DbContext, mappings explícitos y entidades 1–6 del diseño, con tablas Identity necesarias. Documenta cualquier diferencia técnica antes de alterar una regla funcional. No implementes cuentas de administradores en esta tarea.
4. Usa ConnectionStrings:DefaultConnection y User Secrets de desarrollo. Proporciona instrucciones CMD para que el usuario configure el secreto local sin enviarlo al chat. No hagas que API asuma que lee .env automáticamente. No escribas contraseña real en archivos versionados.
5. Genera InitialCore y exporta SQL de revisión a backend/artifacts/InitialCore.sql. No apliques la migración a dj_requests: este paso lo hará el usuario después de revisar. No ejecutes migración en arranque.
6. Implementa GET /health/live y GET /health/ready; live comprueba proceso y ready conectividad DB sin detalles sensibles. Ready no acredita que ya se aplicó el esquema; documentarlo. No crear auth ficticia ni endpoints públicos de entidades.
7. Añade pruebas de integración PostgreSQL para las siete condiciones del esquema. Se pueden ejecutar con una instancia/base desechable aislada mediante una herramienta compatible como Testcontainers; nunca contra dj_requests. Si Docker no está disponible, explica el bloqueo y no presentes los tests como aprobados.
8. Ejecuta restore, build y tests que permita el entorno. Revisa que migración incluya índices, checks, FK compuestas y cascadas. Añade docs/ESTADO.md y backend/README.md con instrucciones reproducibles de configuración, revisión, aplicación manual futura y ejecución.

No implementar todavía: login/registro endpoints, JWT, frontend, WebSockets, worker de limpieza, publicación, importación musical, API Spotify o pantallas administrativas. El esquema y documentación deben quedar preparados para tareas siguientes. No crear scripts SQL alternativos al modelo EF; el SQL exportado es solo salida de la migración.

Criterios de finalización:
- Build real exitoso o error exacto documentado; versiones fijadas y herramientas locales restaurables.
- Modelo 1–6 + soporte Identity concuerda con documento; correo único, pertenencia sala/sesión, nullables y cascadas verificables.
- Migración y SQL generados, NO aplicados a dj_requests.
- Sin secretos, datos reales ni cambios en Docker/Node ajenos a la tarea.
- Pruebas con resultado real o explicación precisa de por qué no pudieron ejecutarse.

Entrega un resumen en español con archivos cambiados, versiones elegidas, comandos y resultados, divergencias, y pasos manuales pendientes. No hagas commit/push ni despliegue. No pidas autorización para crear archivos y compilar dentro de este alcance; detente solo ante una contradicción material o una acción fuera del alcance.
