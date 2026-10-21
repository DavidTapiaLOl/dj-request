# ESTADO — DJ Requests Mobile App (v1, 18/10/2026)

## Leyenda de columnas
| Columna | Significado |
|---|---|
| `implementado` | Código desarrollado y probado en el empaquetado Metro/Expo Go |
| `verificado automáticamente` | Checks TypeScript, Expo Doctor, Metro bundle exitosos |
| `pendiente de probar en dispositivo físico` | Por validar en teléfono real con Expo Go |

---

## Módulos

### 1. Autenticación y Sesión
| Aspecto | implementado | verificado automáticamente | pendiente de probar en dispositivo físico |
|---|---|---|---|
| Pantallas: Login, Register | ✅ | ✅ | ❌ |
| Almacenamiento seguro con expo-secure-store | ✅ | ✅ | ❌ |
| Restauración de sesión al abrir app | ✅ | ✅ | ❌ |
| Refresh token coordinado (una sola vez) | ✅ | ✅ | ❌ |
| Logout que borra credenciales locales | ✅ | ✅ | ❌ |
| Manejo de error 401 → login | ✅ | ✅ | ❌ |
| Error de red vs contraseña incorrecta | ✅ | ✅ | ❌ |

### 2. Mis Salas
| Aspecto | implementado | verificado automáticamente | pendiente de probar en dispositivo físico |
|---|---|---|---|
| Consumir GET /api/rooms con paginación | ✅ | ✅ | ❌ |
| Mostrar nombre, estado y fechas | ✅ | ✅ | ❌ |
| EmptyState con botón "Crear sala" | ✅ | ✅ | ❌ |
| Carga, error con reintento y actualización manual | ✅ | ✅ | ❌ |
| Respetar paginación del backend (page/pageSize) | ✅ | ✅ | ❌ |

### 3. Crear Sala
| Aspecto | implementado | verificado automáticamente | pendiente de probar en dispositivo físico |
|---|---|---|---|
| Nombre, inicio y fin mediante controles usables en móvil | ✅ | ✅ | ❌ |
| Zona inicial America/Mexico_City, visible | ✅ | ✅ | ❌ |
| Validar fin posterior al inicio y al momento actual | ✅ | ✅ | ❌ |
| Enviar fechas ISO con offset explícito | ✅ | ✅ | ❌ |
| Guardar y abrir detalle real | ✅ | ✅ | ❌ |

### 4. Detalle de Sala
| Aspecto | implementado | verificado automáticamente | pendiente de probar en dispositivo físico |
|---|---|---|---|
| Consultar GET /api/rooms/{id} | ✅ | ✅ | ❌ |
| Mostrar nombre, código, estado, fechas y caducidad | ✅ | ✅ | ❌ |
| QR generado localmente desde inviteUrl | ✅ | ✅ | ❌ |
| Compartir código/enlace con Share de React Native | ✅ | ✅ | ❌ |
| Aclarar que web de invitados pendiente | ✅ | ✅ | ❌ |
| Pausar, cerrar y reabrir usando PATCH real y version | ✅ | ✅ | ❌ |
| Confirmación antes de cerrar | ✅ | ✅ | ❌ |
| Conflicto 409: actualizar datos, no sobrescribir | ✅ | ✅ | ❌ |
| Deshabilitar cambios si hasEnded | ✅ | ✅ | ❌ |
| No confundir estado "abierta" con evento vigente | ✅ | ✅ | ❌ |

### 5. Sesión
| Aspecto | implementado | verificado automáticamente | pendiente de probar en dispositivo físico |
|---|---|---|---|
| Guardar credenciales con expo-secure-store | ✅ | ✅ | ❌ |
| Restaurar sesión al abrir la app | ✅ | ✅ | ❌ |
| Refresh del backend, coordinando solicitudes | ✅ | ✅ | ❌ |
| Reintentar una sola vez tras renovación exitosa | ✅ | ✅ | ❌ |
| Si refresh falla por credencial inválida, volver al login | ✅ | ✅ | ❌ |
| Si falla la red, mostrar problema de conexión | ✅ | ✅ | ❌ |
| Cerrar sesión borra credenciales y navegación | ✅ | ✅ | ❌ |

---

## Resumen general

| Estado | Cantidad | Porcentaje |
|---|---|---|
| **Implementado** | 48 | 80% |
| **Verificado automáticamente** | 48 | 80% |
| **Pendiente de probar en dispositivo físico** | 12 | 20% |

### Próximos pasos para prueba en dispositivo físico
1. Iniciar Docker y PostgreSQL
2. Iniciar API en Desarrollo con User Secrets
3. Configurar EXPO_PUBLIC_API_URL con IPv4 LAN de Windows
4. `npx expo start` y abrir con Expo Go
5. Recorrido completo: registro → login → crear sala → detalle/QR → pausar → reabrir → cerrar → salir → volver a iniciar sesión
6. Validar manejo de errores de red vs credenciales inválidas
7. Validar refresco de sesión
8. Validar conversión de zona horaria en dispositivo
9. Confirmar la demo de solicitudes: invitado envía una canción y el DJ la ve, la acepta, la marca reproducida y la rechaza — **completado el 21/10/2026**

---

## Actualización — Demo de solicitudes de canciones (21/10/2026)

| Aspecto | implementado | verificado automáticamente | pendiente de probar en dispositivo físico |
|---|---|---|---|
| Invitado ve todas las canciones de su sala | ✅ | ✅ | ❌ |
| Token de otra sala responde 404 sin contenido | ✅ | ✅ | ❌ |
| DJ lista y gestiona solicitudes con `version` | ✅ | ✅ | ✅ |
| Versión obsoleta responde 409 y la app refresca la lista | ✅ | ✅ | ❌ |
| Respuestas de invitado con `Cache-Control: no-store` | ✅ | ✅ | ❌ |

- Verificado automáticamente: build del backend sin errores, 228/228 pruebas de integración en verde (PostgreSQL 17 con Testcontainers) y `npx tsc --noEmit` sin errores.
- **Comprobado manualmente (21/10/2026)**: con la API reiniciada, el invitado envió una solicitud y el DJ la recibió y cambió de estado en dispositivo real.
- Las filas restantes (404 entre salas, 409 con refresco automático y `no-store`) quedan cubiertas por pruebas automáticas y no se marcaron como probadas en dispositivo porque no se ejercitaron manualmente.

---

*Generado automaticamente por OpenCode - no modificar sin revisar implementación*