# Sistema de diseño — DJ Requests

Documento de identidad visual y criterios de interfaz para las tres aplicaciones: móvil del DJ (`mobile/`), web de invitados (`web/`) y escritorio administrativo (`desktop/`). En español; nombres de tokens e identificadores en inglés.

## 1. Identidad

Herramienta profesional para DJs y eventos nocturnos. Estilo oscuro, sobrio, moderno y legible con poca luz. El violeta es el color de acento y de la marca, no un relleno general. Evitar neón excesivo, elementos decorativos sin función, gradientes innecesarios y saturación de color. Priorizar claridad operativa: el DJ revisa la sala con prisa, en un entorno oscuro y con una mano.

## 2. Tokens de color

Valores canónicos por nombre semántico:

| Token | Valor | Uso |
|---|---|---|
| `background` | `#0B0D12` | Fondo principal de pantalla |
| `surface` | `#161A23` | Superficie de tarjetas y paneles |
| `surfaceRaised` | `#202634` | Campos, cabeceras y superficies elevadas |
| `surfaceMuted` | `#202634` | Fondos atenuados (mismo valor que `surfaceRaised`) |
| `border` | `#303848` | Bordes y separadores |
| `primary` | `#8B5CF6` | Acento principal, acciones primarias, foco |
| `primaryPressed` | `#7C3AED` | Acento en estado presionado |
| `textPrimary` | `#F5F7FA` | Texto principal |
| `textSecondary` | `#A7B0C0` | Texto auxiliar y secundario |
| `success` | `#4ADE80` | Estado "abierta" y confirmaciones |
| `warning` | `#FBBF24` | Estado "pausada" y avisos |
| `neutral` | `#6B7280` | Estado "cerrada"; acento neutro, **no** es error |
| `danger` | `#F87171` | Errores y acciones destructivas |

Reglas:

- Todo estado se comunica con **texto o icono además del color**; la paleta por sí sola no acredita accesibilidad.
- Verificar el contraste real de las combinaciones (fondo + texto + acento) usadas, no el token aislado.
- `neutral` y `danger` no son intercambiables: cerrar una sala es una operación legítima, no un error.
- Los colores previos del prototipo móvil (`#8a2be2`, `#f0e6ff`, `#34D399`, `#EF4444`, `#6B7280` fuera de este documento) quedan superados por estos tokens y deben migrarse al centralizar el tema, en una tarea futura de diseño.

## 3. Tipografía y espaciado

- Tipografía del sistema de cada plataforma; sin familias externas salvo decisión explícita.
- Escala orientativa: títulos 28–32; títulos de tarjeta 18–20; texto 16; auxiliar 14.
- Espaciado base: 4, 8, 12, 16, 24, 32.
- Radio: tarjetas 16, controles 12.
- Altura mínima de control táctil: 48.
- Adoptar unidades de cada plataforma (dp/sp en móvil, rem en web/escritorio).
- Soportar ampliación de texto del sistema y contenido largo (nombres de artista y canción hasta 200 caracteres) sin recortes ni solapamientos.

## 4. Componentes

Apariencia y estados para cada componente. Estados: normal, presionado, foco, deshabilitado, carga.

- **Botón primario**: fondo `primary`, texto claro; presionado `primaryPressed`; deshabilitado atenuado y no interactivo.
- **Botón secundario**: fondo `surfaceRaised`, borde `border`, texto `textPrimary`.
- **Botón destructivo**: texto o acento `danger`; siempre con confirmación previa.
- **Campo de texto**: etiqueta permanente (no solo placeholder), texto de ayuda opcional, estado de error con mensaje. Fondo `surfaceRaised`, borde `border`, foco `primary`.
- **Selector de fecha/hora**: hereda la apariencia de campo de texto; conserva el contrato funcional de selección y apertura.
- **Tarjeta de sala**: fondo `surface`, radio 16, título (nombre), metadatos auxiliares (`textSecondary`), etiqueta de estado, área táctil completa que abre el detalle.
- **Etiqueta de estado** (siempre con texto visible, nunca solo color):
  - Abierta: fondo oscuro + acento `success`.
  - Pausada: fondo oscuro + acento `warning`.
  - Cerrada: fondo oscuro + acento `neutral` + texto claro; verificar el contraste de esta combinación real (p. ej. `textPrimary` sobre `surfaceRaised` con borde `neutral`). No usar `danger`.
- **Mensaje de error**: `danger`, breve, con acción de reintento cuando aplique.
- **Estado vacío**: ilustración o icono discreto, texto explicativo, acción principal si existe.
- **Indicador de carga**: animación discreta; prefiere estados inline sobre bloqueos totales.
- **Confirmación de acciones**: diálogo con acción cancelable; destructivas con `danger`.

Todo control mostrado debe ejecutar una acción real. No se dibujan botones decorativos ni estados sin datos reales.

## 5. Formularios

- Mostrar errores tras `blur` o al enviar, nunca al abrir el formulario.
- Actualizar el error en cuanto se corriga el valor.
- Si falla la petición, conservar lo escrito para corregir sin volver a teclear.
- Distinguir visualmente: validación de campo, credenciales inválidas, error de red y error del servidor.
- La navegación no depende de la validez de todos los campos salvo envío explícito.
- Bloquear envíos duplicados mientras haya una petición en curso.
- Los selectores de calendario y hora conservan el contrato funcional ya implementado (apertura, selección, confirmación, cancelación sin emitir cambios).

## 6. Estados de sala

Alineado con los contratos reales (`estado` ∈ `abierta`/`pausada`/`cerrada`, más `hasEnded` de `RoomResponse`):

- **Abierta**: verde (`success`) + texto "Abierta".
- **Pausada**: ámbar (`warning`) + texto "Pausada".
- **Cerrada**: gris (`neutral`) + texto "Cerrada"; fondo oscuro, acento neutral y texto claro; no confundir con error.
- **Evento finalizado** (`hasEnded`): indicación explícita adicional ("finalizada" o equivalente) según datos reales del backend; nunca inferida solo por la hora en el cliente.
- Cerrar o pausar manualmente **no** significa eliminación; la eliminación ocurre 7 días tras el fin programado y no se muestra como un estado de interfaz.

## 7. Adaptación por aplicación

- **Móvil del DJ (`mobile/`, Expo/React Native)**: patrón táctil, tarjetas, áreas seguras respetadas, "Crear sala" siempre accesible; gestos solo como complemento a botones visibles.
- **Web de invitados (`web/`, Angular)**: prioridad de ancho de teléfono, misma identidad visual, invitados sin registro; nunca exponer playlists ni datos privados en la interfaz.
- **Escritorio administrativo (`desktop/`, Electron+Angular)**: tablas y densidad de información, aprovechar pantalla ancha, no estirar el diseño móvil; mostrar solo funciones realmente implementadas.

Estado actual: `mobile/` existe; `web/` ya tiene la primera versión de la web de invitados (Angular); `desktop/` está vacía. Estas reglas se aplican cuando cada aplicación se cree.

## 8. Accesibilidad

- WCAG 2.2 nivel AA como objetivo verificable en contrastes y áreas táctiles.
- Foco visible en web y escritorio; navegación completa con teclado; orden lógico de tabulación.
- Etiquetas accesibles en campos y botones; los iconos informativos llevan texto o etiqueta.
- No comunicar estado solo por color (ver secciones 2 y 6).
- Soportar ampliación de texto sin recortar ni solapar contenido.
- Animaciones discretas; respetar la preferencia de reducción de movimiento del sistema.

## 9. Reglas de implementación

- Centralizar tokens en el tema de cada frontend (theme de React Native; variables CSS/tema en Angular); no repetir literales de color repartidos por pantallas.
- Reutilizar componentes existentes antes de crear uno nuevo.
- Compartir nombres y significado de tokens entre plataformas; **no** exige código compartido entre React Native y Angular.
- Iconos compatibles con cada plataforma (móvil: Ionicons ya disponible en `mobile/`).
- Añadir dependencias visuales solo si son necesarias; evitar librerías de UI nuevas sin requerimiento.
- Datos y estados falsos de maqueta: prohibidos en pantallas conectadas al backend; una acción mostrada debe estar implementada.
- Una tarea de rediseño **no** cambia API, autenticación, reglas de negocio, validaciones, permisos ni rutas de navegación.
- Adopción progresiva: se puede mejorar pantalla por pantalla sin reescrituras totales.

## 10. Verificación

Checklist para cambios visuales:

1. Uso de tokens coherente con la sección 2 (sin colores fuera de tabla).
2. Revisados estados de carga, vacío, error y éxito donde apliquen.
3. Nombres largos (hasta 200 caracteres) y texto ampliado sin romper el diseño.
4. Vista en ancho de teléfono y, si aplica, escritorio.
5. Navegación y acciones preservadas: el cambio es visual, la lógica no cambia.
6. Distinguir compilación correcta de revisión visual real en navegador o dispositivo: pasar `tsc`/export no acredita el aspecto final.

## 11. Estado de implementación

- **Reglas acordadas**: este documento.
- **Componentes implementados**:
  - `mobile/src/theme/theme.ts`: tokens de color, espaciado, radios, tipografía, movimiento y layout; incluye `onPrimary` para texto sobre el violeta primario.
  - `mobile/src/components/BrandLogo.tsx`: marca D violeta con tres barras, wordmark separado, orientación `inline`/`stacked` y arcos tenues opcionales. Exporta `AuthBrand` (símbolo 88, arcos, wordmark 26) para que Login y Registro usen el mismo bloque visual.
  - `mobile/src/components/FadeInView.tsx`: opacidad y desplazamiento de 8 px en 250 ms, sin reiniciar al escribir o refrescar; respeta reducción de movimiento.
  - `mobile/src/components/AppButton.tsx`: variantes `primary`, `secondary`, `outline` y `danger`; altura mínima 48, radio 12, respuesta de presión con escala, carga, deshabilitado y texto accesible sobre el color primario. `outline` usa borde y contenido violeta.
  - `mobile/src/components/AppTextField.tsx`: etiqueta exterior u oculta para usar placeholder, ayuda, error, foco visible, toggle de contraseña con área táctil de 48 y tipografía Arial/sans aplicada al input real en web.
  - `mobile/src/components/AuthScreenContainer.tsx`: contenedor de autenticación con teclado, scroll, ancho máximo y fondo oscuro.
  - `mobile/src/components/ScreenHeader.tsx`: flecha segura, marca compacta 32/20 opcional y acción derecha icon-only accesible.
  - `mobile/src/components/RoomCard.tsx`: nombre sin truncar, estado, un solo icono de calendario con el intervalo completo, código y acción "Ver sala" a ancho completo; sin zona horaria visible ni chevron superior.
  - `mobile/src/components/DateTimeField/`: selector web `datetime-local` y selector nativo, integrados con el tema oscuro; etiqueta exterior o interior, tipografía Arial/sans y valor en `textPrimary`.
  - `mobile/src/features/rooms/roomsScreenState.ts`: reglas excluyentes de carga inicial, error, vacío y listado.
- **Pantallas adaptadas**:
  - `Login` (`mobile/app/(auth)/login.tsx`): marca grande con arcos, "Bienvenido de nuevo", subtítulo, correo, contraseña, "Iniciar sesión" y enlace único a registro con área táctil de 48. Conserva los mensajes de error y la lógica de autenticación existentes.
  - `Registro` (`mobile/app/(auth)/register.tsx`): flecha arriba a la izquierda, `AuthBrand` centrada (mismo símbolo, arcos y wordmark que Login), "Crea tu cuenta", subtítulo, nombre artístico, correo, contraseña, ayuda "Mínimo 12 caracteres" y enlace único a login.
  - `Crear sala` (`mobile/app/(app)/create.tsx`): solo flecha en el header; `BrandLogo` 64/22 dentro del contenido, alineado arriba y centrado horizontalmente, "Crea una sala", subtítulo, campos "Nombre de la sala" (placeholder), "Inicio del evento" y "Fin del evento" (etiqueta interior), botón "Crear sala". **No muestra zona horaria**; conserva `timezone`, validaciones, payload y navegación al detalle mediante `router.replace`.
  - `Mis salas` (`mobile/app/(app)/index.tsx`): marca compacta, cierre de sesión icon-only, saludo real y "Mis salas". Carga inicial solo con indicador; sin salas solo "Crear mi primera sala"; con salas solo el botón superior "Crear sala"; error inicial solo error y "Reintentar". Al volver del detalle o tras crear, recupera el listado con `useFocusEffect` sin logout ni recarga manual.
  - `Detalle de sala` (`mobile/app/(app)/room/[id].tsx`): nombre en fila propia, estado, un solo horario, tarjeta "Invita a tu público" con código real, QR 120 sobre blanco, "Compartir enlace" outline violeta y acciones Pausar/Reabrir/Cerrar dentro de la misma tarjeta. La URL solo aparece como fallback copiable si compartir/copiar falla. Solicitudes sin carátula ni miniatura; "Abrir canción" solo con URL HTTP/HTTPS válida; acciones de aceptar, rechazar y marcar reproducida en filas completas.
- **Reglas de encabezado único**
- Los encabezados deben ser únicos por pantalla. El header nativo de Expo Router (`headerShown`) debe desactivarse cuando se presenta un `ScreenHeader` personalizado. Evitar superposición de dos barras. El botón "Volver" usa `router.canGoBack() ? router.back() : router.replace("/(app)")`.
- **Reglas de mensajes de error visibles**
- Los errores se comunican con `Text`/`View` visibles, nunca solo con `Alert.alert`. Un 401 de login muestra "Correo o contraseña incorrectos"; un fallo de red muestra un mensaje de conexión; un 429 indica "Demasiados intentos" y usa `Retry-After` cuando está disponible. No se desmonta el formulario durante el envío.
- **Reglas de calendario y hora**
- `DateTimeField/` conserva las implementaciones web (`type="datetime-local"`) y nativa. En Crear sala las etiquetas visibles son "Inicio del evento" y "Fin del evento", integradas dentro del control. Seleccionar fecha no borra la hora ni viceversa; cancelar no modifica el valor. No se actualiza la dependencia `@react-native-community/datetimepicker`.
- **Reglas de campos y errores compartidos**
- Valor de input en `textPrimary`, placeholder diferenciado, un mensaje por campo, ayuda separada del error, sin errores al abrir un formulario intacto, validación tras `blur` o envío y bloqueo de envíos duplicados. El control real usa Arial con fallback sans-serif en web y la sans del sistema en nativo; no se hereda una serif del navegador.
- **Criterios de privacidad visual**
- No se muestran carátulas, miniaturas ni espacios reservados para imágenes de canciones. El QR es estático y se dibuja sobre superficie blanca. Los enlaces se validan antes de ofrecer la acción.
- **Verificación ejecutada**:
  - `npx tsc --noEmit`: sin errores. Fue necesario retirar un `.expo/types/router.d.ts` generado y corrupto en Windows; es caché, no código fuente.
  - `npx expo export --platform web`: exportación correcta.
  - `npx expo lint`: 0 errores; permanecen 3 avisos previos en `src/api/client.ts` y `src/auth/provider.tsx`.
  - Prueba automatizada con Playwright y API simulada: estados vacío/listado, flujo crear sala → detalle → volver al listado, ausencia de desbordamiento horizontal a 390 y 320 px, acciones de detalle dentro de la tarjeta, URL oculta y estilos/fuentes aplicados a los inputs reales.
  - Capturas locales generadas a 390×844 y 320×844 en una carpeta temporal. Se compararon posiciones y textos contra los mockups con OCR y mediciones DOM. **No se realizó inspección visual humana ni del modelo sobre los PNG**; la compilación y estas comprobaciones no acreditan fidelidad visual completa.
- **Limitaciones**: no se añadieron dependencias ni se modificaron contratos de API, autenticación, validaciones, permisos, rutas funcionales ni manejo de concurrencia. La verificación usó respuestas simuladas, no una sesión contra la API real. `app.json` usa `userInterfaceStyle: "dark"`; la imagen de splash existente no se rediseñó.
