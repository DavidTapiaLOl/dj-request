# Requisitos — versión 1, 29 de septiembre de 2026

## Confirmados por el usuario
Proyecto gratuito para DJs. Desarrollo individual asistido por OpenCode. Android e iPhone, web de invitados, administración para Windows y Mac. No reproduce música. Las solicitudes se escriben manualmente: nombre obligatorio, artista/género/enlace opcionales. Enlaces de cualquier plataforma musical, sin API de Spotify. Invitados sin registro, pueden ver las canciones individuales de su sala pero no interactuar con solicitudes ajenas ni ver playlists. Las playlists solo las ve el DJ. No se requiere análisis musical en la primera versión. Contenido temporal durante siete días después del evento, sin historial musical posterior.
Fase 1: 13/10/2026, móvil/API/BD. Fase 2: 29/10/2026, web. Entrega final: 29/11/2026, escritorio y publicación. Canal exacto de distribución en tiendas pendiente; no prometer fechas de aprobación.

## Decisiones técnicas propuestas para el MVP
Aplicar estos valores como configuración inicial documentada; no atribuirlos al usuario como requisitos originales.
- Una sala por evento, propiedad de un DJ. Preparación y en vivo son momentos del mismo evento.
- Estados: abierta, pausada, cerrada. Una sala abierta admite peticiones antes del evento; después de su fin programado ninguna las admite. Pausar/cerrar manualmente no adelanta su eliminación. Reabrir solo antes del fin. Reprogramar solo antes del fin y nunca una sala vencida.
- Solicitud: pendiente → aceptada o rechazada; aceptada → reproducida o rechazada. DJ marca reproducción manualmente. No edición por invitados en MVP.
- Playlist: URL requerida, nombre opcional de hasta 200 caracteres; referencia externa sin importación.
- Título hasta 200 caracteres, artista hasta 200, URL hasta 2048. Recortar espacios, convertir opcionales vacíos a null. Género desde catálogo opcional con Otro; omitir equivale a no informado.
- Límite inicial: 10 solicitudes y 2 playlists por sesión/sala; 30 segundos entre envíos de contenido. Aplicar de forma atómica, permitir configuración y devolver error claro. Rate limit adicional por IP con tolerancia a redes compartidas. La sesión anónima no garantiza una persona única.
- Invitados consultan canción, artista, género, enlace, estado y fecha de envío. No reciben identificadores privados de sesiones ni identidad del solicitante. Playlists tienen endpoints y eventos privados separados.
- Cada DJ dispone de un perfil público asociado a su cuenta: nombre artístico, descripción opcional, año de inicio opcional, foto opcional y enlaces opcionales de Instagram, Facebook, TikTok, YouTube y SoundCloud. El DJ lo consulta y edita autenticado; un invitado solo puede consultarlo desde una sesión válida de la sala. No se exponen correo, credenciales, playlists ni datos administrativos.
- Las fotos de perfil aceptan JPEG o PNG de hasta 5 MB, se validan por contenido, se redimensionan a un máximo de 2048 px y se publican sin metadatos. Los enlaces sociales deben ser HTTPS y pertenecer al dominio de la plataforma indicada; el servidor no descarga ni genera vistas previas.
- Eliminar sala completa y contenido a las 168 horas del fin programado. Cerrar acceso público al terminar el evento; DJ puede consultar hasta la caducidad. No exportar ni conservar rankings musicales después.
- Los administradores gestionan cuentas, estados y salud operativa; no reciben acceso automático a contenido musical privado.

## Criterios de aceptación transversales
RF01 Registro/login DJ sin elevación a administrador.
RF02 DJ crea sala con fecha de inicio y fin válidas, código y enlace QR.
RF03 Invitado entra con código, obtiene sesión temporal y envía petición válida.
RF04 Invitado consulta lista paginada de canciones de esa sala, sin votos.
RF05 Solo propietario gestiona solicitudes y consulta playlists.
RF06 Sala pausada/cerrada/finalizada rechaza envíos, incluso si interfaz quedó abierta.
RF07 Contenido vencido desaparece por limpieza; no se conserva en auditoría/logs.
RF08 Desconexión visible y recarga del estado desde API al reconectar.
RF09 Administradores autenticados, sin alta pública; acciones auditadas.
RF10 No hay audio, búsqueda externa ni importación de playlists.
RF11 DJ consulta y edita su propio perfil; invitado con sesión válida consulta únicamente el perfil del DJ de su sala.

## Pendientes que no bloquean el esquema inicial
Nombre comercial/dominio, proveedor de alojamiento, cuentas de tiendas, requisitos exactos de publicación, versiones de frontend y estrategia de autenticación/distribución móvil. Resolver antes de sus módulos, no inventar aprobaciones.
