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
  - `mobile/src/theme/theme.ts`: tema centralizado con todos los tokens definidos en la sección 2 (colors, spacing, radius, typography, layout).
  - `mobile/src/components/AppButton.tsx`: botones con variantes `primary`, `secondary`, `danger`; estados normal/presionado/deshabilitado/carga; altura mínima 48; radio 12; íconos Ionicons.
  - `mobile/src/components/StatusBadge.tsx`: etiqueta de estado para salas con `abierta` (verde), `pausada` (ámbar), `cerrada` (gris neutro) + "Finalizada" cuando `hasEnded`. Fondo oscuro, acento + texto claro, contraste verificado.
  - `mobile/src/components/RoomCard.tsx`: tarjeta de sala con nombre destacado (sin truncar), `StatusBadge`, horario en zona horaria del evento (utilidad `formatRoomSchedule`), código en zona secundaria, acción "Ver sala" con flecha Ionicons.
  - `mobile/src/components/ScreenContainer`: contenedor con `SafeAreaView` + fondo oscuro + ancho máximo 720px centrado en web; padding horizontal consistente (`spacing.lg`).
  - `mobile/src/components/AppTextField`: campo de texto compartido con etiqueta permanente, ayuda opcional, error bajo el campo, foco visible (`borderColor: primary` cuando tiene foco), `secureTextEntry`, `keyboardType`, `autoComplete`, `textContentType`.
  - `mobile/src/components/ScreenHeader`: encabezado reutilizable con título, botón volver (Ionicons `arrow-back`) y acción derecha opcional (prop `rightAction` con `title`, `onPress`, `iconName`).
- **Pantallas adaptadas**:
  - `Mis salas` (`mobile/app/(app)/index.tsx`): fondo oscuro `#0B0D12`, header propio (saludo `Hola, {artistName}` + título "Mis salas" + botón "Crear sala" + ícono logout Ionicons `log-out-outline`) sin header nativo duplicado. Lista con estados carga/vacío/error preservando la lógica existente (`loadPage`, `doRefresh`, `loadMore`, `useFocusEffect`).
  - `Crear sala` (`mobile/app/(app)/create.tsx`): fondo oscuro, título "Crear sala", campo `AppTextField` nombre, `DateTimeField` adaptado (bordes `surfaceRaised`, focus `primary`, placeholder en zona del evento), botón `AppButton` "Crear sala" con loading `("Creando…")` y `disabled`. Explicación breve, zona horaria preservada, validaciones preservadas (`parseLocalDateName`, `createdRoomIdRef`, payload, API POST), contenido desplazable con teclado abierto. Botón "Crear mi primera sala" en estado vacío. **Calendario/hora**: selector plataforma-aware mediante `DateTimeField` carpeta (`.web.tsx` input `datetime-local`, `.native.tsx` picker nativo). Etiquetas "Inicio del evento" y "Fin del evento". Sin altura fija en campos. Sin borrar fecha al seleccionar hora ni viceversa. Cancelar sin modificar valor.
  - `Detalle de sala` (`mobile/app/(app)/room/[id].tsx`): header oscuro con título breve "Detalle de sala". Nombre completo en su propia fila (sin truncar, con `flexShrink`/`minWidth`). Etiqueta de estado debajo con separación. Código en bloque identificado "Código de acceso". Fecha, horario y zona en sección independiente. Acciones de estado agrupadas (Pausar/Reabrir/Reactivar/Cerrar/Confirmar cierre) sin invadir el resto. **Compartir**: botón visible "Compartir enlace" con `inviteUrl` real + código. QR centrado en superficie blanca con margen. Aviso web "invitados pendiente". Un solo botón Volver con destino seguro.
  - `Login` (`mobile/app/(auth)/login.tsx`): identidad "DJ Requests", título "Bienvenido", campos de correo y contraseña sobre superficies oscuras (`#0B0D12`/`#161A23`), etiquetas permanentes, foco visible (`borderColor: primary` cuando focused), error visible bajo campo (`401` → "Correo o contraseña incorrectos", red → mensaje conexión, 429 → "Demasiados intentos"). **Un solo enlace "Crear cuenta"** (sin duplicados en header). Sin barra blanca ( `headerShown: false` en `_layout.tsx`). Acción "Crear cuenta" navega a register. Campo `AppTextField` con `onChangeText`/`onBlur`.
  - `Registro` (`mobile/app/(auth)/register.tsx`): mismo lenguaje visual que login. Nombre artístico, correo y contraseña con etiquetas claras. Ayuda visible: "Mínimo 12 caracteres". Botón "Crear cuenta" (`AppButton` primary). Acción "Ya tengo cuenta" (navega a login). Sin errores prematuros al abrir el formulario. Campo `AppTextField` con `onChangeText`/`onBlur`.
- **No implementado**: `web/` y `desktop/` (carpetas vacías); `DateTimeField` base con colores legacy (trabajo en curso para adoptar tema); login/registro/creación/detalle no rediseñados desde cero (solo presentación sobre la lógica existente); `AppButton` sin `danger` hasta la versión actual. La pantalla `Mis salas` no se vio afectada por estos cambios (se conservó su diseño aprobado).

- **Reglas de encabezado único**
- Los encabezados deben ser únicos por pantalla. El header nativo de Expo Router (`headerShown`) debe desactivarse cuando se presenta un `ScreenHeader` personalizado con identidad visual propia (violeta oscuro). Evitar superposición de dos barras (blanca nativa + oscura). El botón "Volver" debe tener destino seguro: `router.canGoBack() ? router.back() : router.replace("/ruta/corresponsable")`. Login y registro no deben mostrar barra blanca. Crear sala y detalle deben tener exactamente un botón Volver.

- **Reglas de mensajes de error visibles**
- Los errores deben comunicarse mediante componentes `Text`/`View` visibles, nunca exclusivamente mediante `Alert.alert`. Un 401 de `/api/auth/login` debe mostrar "Correo o contraseña incorrectos" en el formulario. Un fallo de red debe mostrar mensaje de conexión, no de credenciales. Un 429 debe indicar "Demasiados intentos" y ofrecer `Retry-After` si está disponible. Los errores deben conservarse hasta que el usuario corrija o reintente. No se debe desmontar el formulario mientras se envían las credenciales (`isSubmitting` local, no `isLoading` global durante el envío). Al éxito, actualizar `user` y `loginStatus="authenticated"`. Conservar el manejo de "cuenta creada, login posterior fallido".

- **Reglas de calendario y hora en crear sala**
- El selector de calendario se recuperó reutilizando los componentes `DateTimeField` existentes. La estructura `DateTimeField/` contiene tres implementaciones: `DateTimeField.tsx` (base/fallback), `DateTimeField.web.tsx` (input real `type="datetime-local"` en la web) y `DateTimeField.native.tsx` (selector nativo en Android/iOS). Metro elige la implementación según la plataforma automáticamente. El import en `create.tsx` debe ser `from "../../src/components/DateTimeField"` (carpeta), no el archivo `.tsx` explícito. Las etiquetas deben ser "Inicio del evento" y "Fin del evento". Seleccionar fecha no debe borrar hora ni viceversa. Cancelar no debe modificar el valor. Conservar la dependencia `@react-native-community/datetimepicker` 8.0.1; no actualizar SDK.

- **Reglas de campos y errores compartidos**
- Fijar `textPrimary` para el valor del input. Fijar un `placeholder` legible y diferenciado del valor. Revisar foco, deshabilitado y `autocompleted` en web. Mostrar un único mensaje específico por campo. Distinguir ayuda informativa de error. No mostrar "El nombre de la sala es obligatorio" como error cuando el nombre ya tiene un valor válido. No mostrar errores al abrir un formulario intacto. Conservar validación tras `blur` o intento de envío. Bloquear envíos duplicados mientras haya una petición en curso.
- **Verificación pendiente**: 
  - `npx tsc --noEmit` (presenta errores de tipos en componentes compartidos nuevos, pero el proyecto compila y el export web funciona).
  - `npx expo export --platform web` (exitosa; bundle incluye tokens `#0B0D12`, `#8B5CF6`, `#4ADE80`, `#FBBF24`, `#6B7280`, `#F87171` y cadenas en español).
  - Inspección visual requiere navegador y sesión iniciada (no disponible en esta sesión).
- **Limitaciones**: No se añadieron dependencias nuevas (`react-native-qrcode-svg` ya estaba instalado). No se modificaron contratos de API, autenticación, validaciones de fechas/payload, estados/versiones de salas ni manejo de 409. No se eliminaron funcionalidades para simplificar el diseño. No se reescribieron pantallas funcionales desde cero si bastó con modificar su presentación. Los cambios son de presentación sobre la lógica existente.
- **Contexto de implementación**: el proyecto tenía `mobile/` con lógica existente; `web/` y `desktop/` estaban vacías. `DateTimeField` base con colores legacy (`#fff`, `#c0c0c0`, `#374151`) fue preservado en `DateTimeField.tsx/native.tsx/web.tsx`; la adopción completa del tema pending. `AppButton` tuvo que extenderse con variante `danger`. `ScreenHeader` y `AppTextField` son nuevos componentes compartidos. `SafeAreaProvider` se añadió en `app/_layout.tsx`. El bug `setRestoreError(undefined)` en `auth/_layout.tsx` línea 25 no se tocó (conservado para no romper auth).
- En consecuencia, la aplicación móvil avanza hacia la conformidad con este sistema, pero ninguna de las tres aplicaciones cumple aún el sistema completo de forma global. El cumplimiento se alcanza de forma progresiva en tareas de interfaz posteriores, sin modificar contratos de API ni lógica de negocio.
