import * as React from "react";
import {
  View,
  Text,
  ActivityIndicator,
  ScrollView,
  StyleSheet,
  Share,
  Platform,
} from "react-native";
import { useLocalSearchParams, useRouter } from "expo-router";
import { request, listSolicitudes, updateSolicitudState, ApiError, NetworkError } from "@/api/client";
import { RoomResponse, SolicitudResponse, SolicitudPageResponse } from "@/api/dto";
import { StatusBadge } from "@/components/StatusBadge";
import { formatRoomSchedule } from "@/utils/dateFormat";
import { Ionicons } from "@expo/vector-icons";
import QRCode from "react-native-qrcode-svg";
import { ScreenContainer } from "@/components/ScreenContainer";
import { ScreenHeader } from "@/components/ScreenHeader";
import { AppButton } from "@/components/AppButton";
import { colors, radius, spacing, typography } from "@/theme/theme";

const UUID_REGEX =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function isValidUuid(value: string): boolean {
  return UUID_REGEX.test(value);
}

function SolicitudBadge({ estado }: { estado: string }) {
  let backgroundColor: string = colors.surfaceRaised;
  let textColor: string = colors.textSecondary;
  let label = estado;

  switch (estado) {
    case "pendiente":
      backgroundColor = "rgba(251, 191, 36, 0.15)";
      textColor = colors.warning;
      label = "Pendiente";
      break;
    case "aceptada":
      backgroundColor = "rgba(74, 222, 128, 0.15)";
      textColor = colors.success;
      label = "Aceptada";
      break;
    case "reproducida":
      backgroundColor = "rgba(107, 114, 128, 0.15)";
      textColor = colors.neutral;
      label = "Reproducida";
      break;
    case "rechazada":
      backgroundColor = "rgba(248, 113, 113, 0.15)";
      textColor = colors.danger;
      label = "Rechazada";
      break;
  }

  return (
    <View style={[styles.solicitudBadge, { backgroundColor }]}>
      <Text style={[styles.solicitudBadgeText, { color: textColor }]}>{label}</Text>
    </View>
  );
}

export default function RoomDetailScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ id: string | string[] }>();

  const rawId = Array.isArray(params.id) ? params.id[0] : params.id;
  const id = rawId && isValidUuid(rawId) ? rawId : null;

  const [room, setRoom] = React.useState<RoomResponse | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [confirmClose, setConfirmClose] = React.useState(false);
  const conflictReloadRef = React.useRef(false);

  // --- Estado de solicitudes ---
  const [solicitudes, setSolicitudes] = React.useState<SolicitudResponse[]>([]);
  const [solicitudesPage, setSolicitudesPage] = React.useState(1);
  const [solicitudesHasMore, setSolicitudesHasMore] = React.useState(false);
  const [solicitudesLoading, setSolicitudesLoading] = React.useState(false);
  const [solicitudesError, setSolicitudesError] = React.useState<string | null>(null);
  const [actionLoadingId, setActionLoadingId] = React.useState<string | null>(null);

  const loadRoom = React.useCallback(async () => {
    if (!id) {
      setIsLoading(false);
      setError("Identificador de sala inválido.");
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const data = await request<RoomResponse>("GET", `/api/rooms/${id}`);
      setRoom(data);
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 404) {
          setError("Sala no encontrada, ajena o caducada.");
        } else if (err.status === 401 || err.status === 403) {
          setError("Tu sesión no tiene permiso para ver esta sala.");
        } else {
          setError(err.message || "No se pudo cargar la sala.");
        }
      } else if (err instanceof NetworkError) {
        setError("Sin conexión con el servidor. Revisa tu red.");
      } else {
        setError("Error inesperado al cargar la sala.");
      }
    } finally {
      setIsLoading(false);
    }
  }, [id]);

  React.useEffect(() => {
    loadRoom();
  }, [loadRoom]);

  const handleStateChange = async (newState: string) => {
    if (!id || isSaving || !room) return;

    setIsSaving(true);
    setError(null);

    try {
      const updated = await request<RoomResponse>(
        "PATCH",
        `/api/rooms/${id}/state`,
        {
          body: { state: newState, version: room.version },
        }
      );
      setRoom(updated);
      setConfirmClose(false);
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setError(
          err.message ||
            "La sala cambió en el servidor. Recargando los datos actuales…"
        );
        if (!conflictReloadRef.current) {
          conflictReloadRef.current = true;
          await loadRoom();
          conflictReloadRef.current = false;
        }
      } else if (err instanceof ApiError && err.status === 404) {
        setError("La sala ya no existe o fue eliminada.");
      } else if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
        setError("Tu sesión no tiene permiso para modificar esta sala.");
      } else if (err instanceof NetworkError) {
        setError("Sin conexión con el servidor. Revisa tu red.");
      } else {
        setError(
          err instanceof Error ? err.message : "Error al cambiar el estado."
        );
      }
    } finally {
      setIsSaving(false);
    }
  };

  const handleClose = () => {
    setConfirmClose(true);
  };

  // --- Carga de solicitudes ---
  const loadSolicitudes = React.useCallback(
    async (page: number = 1, append: boolean = false) => {
      if (!id) return;

      setSolicitudesLoading(true);
      setSolicitudesError(null);

      try {
        const data: SolicitudPageResponse = await listSolicitudes(id, { page, pageSize: 20 });
        if (append) {
          setSolicitudes(prev => [...prev, ...data.items]);
        } else {
          setSolicitudes(data.items);
        }
        setSolicitudesPage(data.page);
        setSolicitudesHasMore(data.hasMore);
      } catch (err) {
        if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
          setSolicitudesError("Tu sesión no tiene permiso para ver las solicitudes.");
        } else if (err instanceof ApiError && err.status === 404) {
          setSolicitudesError("Sala no encontrada.");
        } else if (err instanceof NetworkError) {
          setSolicitudesError("Sin conexión con el servidor.");
        } else {
          setSolicitudesError("No se pudieron cargar las solicitudes.");
        }
      } finally {
        setSolicitudesLoading(false);
      }
    },
    [id]
  );

  React.useEffect(() => {
    if (id && room) {
      loadSolicitudes(1);
    }
  }, [id, room, loadSolicitudes]);

  // --- Acciones sobre solicitudes ---
  const handleSolicitudAction = async (solicitud: SolicitudResponse, nuevoEstado: string) => {
    if (!id || actionLoadingId) return;

    setActionLoadingId(solicitud.id);
    setSolicitudesError(null);

    try {
      const updated = await updateSolicitudState(id, solicitud.id, nuevoEstado, solicitud.version);
      setSolicitudes(prev =>
        prev.map(s => (s.id === updated.id ? updated : s))
      );
    } catch (err) {
      if (err instanceof ApiError && err.status === 409) {
        setSolicitudesError("La solicitud cambió en el servidor. Recargando…");
        await loadSolicitudes(1);
      } else if (err instanceof ApiError && err.status === 404) {
        setSolicitudesError("Solicitud no encontrada.");
        await loadSolicitudes(1);
      } else if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
        setSolicitudesError("Tu sesión no tiene permiso para modificar solicitudes.");
      } else if (err instanceof NetworkError) {
        setSolicitudesError("Sin conexión con el servidor.");
      } else {
        setSolicitudesError("No se pudo actualizar la solicitud.");
      }
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleRefreshSolicitudes = () => {
    loadSolicitudes(1);
  };

  const handleLoadMoreSolicitudes = () => {
    if (!solicitudesLoading && solicitudesHasMore) {
      loadSolicitudes(solicitudesPage + 1, true);
    }
  };

  const handleShare = async () => {
    if (!room) return;

    const message = `Únete a la sala "${room.nombre}" con el código ${room.codigo}: ${room.inviteUrl}`;

    try {
      await Share.share(
        Platform.OS === "web"
          ? { message }
          : { message, url: room.inviteUrl }
      );
    } catch {
      setError("No se pudo abrir el diálogo de compartir.");
    }
  };

  

  // --- Early returns ---
  const handleBack = () => {
    if (router.canGoBack()) router.back();
    else router.replace("/(app)");
  };

  if (!id) {
    return (
      <View style={styles.centered}>
        <Text style={styles.errorText}>Identificador de sala inválido.</Text>
        <AppButton title="Volver" variant="secondary" onPress={handleBack} />
      </View>
    );
  }

  if (isLoading && !room) {
    return (
      <ScreenContainer>
        <View style={styles.centered}>
          <ActivityIndicator size="large" color={colors.primary} />
        </View>
      </ScreenContainer>
    );
  }

  if (error && !room) {
    return (
      <ScreenContainer>
        <View style={styles.stateBox}>
          <Ionicons name="cloud-offline-outline" size={40} color={colors.danger} />
          <Text style={styles.errorText}>{error}</Text>
          <AppButton title="Reintentar" variant="secondary" onPress={loadRoom} />
          <AppButton title="Volver" variant="secondary" onPress={handleBack} />
        </View>
      </ScreenContainer>
    );
  }

  if (!room) {
    return (
      <ScreenContainer>
        <View style={styles.centered}>
          <Text style={styles.errorText}>La sala no está disponible o ha sido eliminada.</Text>
          <AppButton title="Volver" variant="secondary" onPress={handleBack} />
        </View>
      </ScreenContainer>
    );
  }

  // Room is definitely defined after the early returns above
  const roomSafe = room;
  const canModify = !roomSafe.hasEnded && !isSaving;

  const schedule = formatRoomSchedule(roomSafe.startsAt, roomSafe.endsAt, roomSafe.timezone);

return (
    <ScreenContainer>
      <ScreenHeader showBack={true} />

      {/* Nombre complete in its own row */}
      <View style={styles.nombreRow}>
        <Text style={styles.nombreText}>{roomSafe.nombre}</Text>
      </View>

      {/* Etiqueta de estado debajo */}
      <View style={styles.estadoRow}>
        <StatusBadge estado={roomSafe.estado} hasEnded={roomSafe.hasEnded} />
      </View>

      {/* Código de acceso */}
      <View style={styles.codigoBlock}>
        <Text style={styles.codigoLabel}>Código de acceso</Text>
        <Text style={styles.codigoValue}>{roomSafe.codigo}</Text>
      </View>

      {/* Fecha, horario y zona */}
      {schedule ? (
        <View style={styles.scheduleSection}>
          <View style={styles.schedule}>
            <View style={styles.scheduleRow}>
              <Ionicons name="calendar-outline" size={15} color={colors.textSecondary} />
              <Text style={styles.scheduleText}>{schedule.primary}</Text>
            </View>
            {schedule.secondary ? (
              <View style={styles.scheduleRow}>
                <Ionicons name="calendar-outline" size={15} color={colors.textSecondary} />
                <Text style={styles.scheduleText}>{schedule.secondary}</Text>
              </View>
            ) : null}
            {schedule.timeZone && (
              <Text style={styles.timeZone}>Zona horaria: {schedule.timeZone}</Text>
            )}
          </View>
        </View>
      ) : null}

      {/* Acciones de estado */}
      {!confirmClose && (
        <View style={styles.actions}>
          {roomSafe.estado === "abierta" && (
            <>
              <AppButton
                title={isSaving ? "Guardando…" : "Pausar"}
                onPress={() => handleStateChange("pausada")}
                disabled={!canModify}
                variant="secondary"
              />
              <AppButton
                title="Cerrar"
                onPress={handleClose}
                variant="danger"
                disabled={!canModify}
              />
            </>
          )}

          {roomSafe.estado === "pausada" && (
            <>
              <AppButton
                title={isSaving ? "Guardando…" : "Reabrir"}
                onPress={() => handleStateChange("abierta")}
                disabled={!canModify}
                variant="secondary"
              />
              <AppButton
                title="Cerrar"
                onPress={handleClose}
                variant="danger"
                disabled={!canModify}
              />
            </>
          )}

          {roomSafe.estado === "cerrada" && (
            <AppButton
              title={isSaving ? "Guardando…" : "Reactivar"}
              onPress={() => handleStateChange("abierta")}
              disabled={!canModify}
            />
          )}
        </View>
      )}

      {confirmClose && (
        <View style={styles.confirmBox}>
          <Text style={styles.confirmText}>
            ¿Seguro que quieres cerrar esta sala? Los invitados ya no podrán enviar peticiones.
          </Text>
          <View style={styles.confirmActions}>
            <AppButton
              title="Confirmar cierre"
              onPress={() => handleStateChange("cerrada")}
              variant="danger"
              disabled={isSaving}
            />
            <AppButton
              title="Cancelar"
              onPress={() => setConfirmClose(false)}
              disabled={isSaving}
            />
          </View>
        </View>
      )}

      {/* Compartir */}
      <View style={styles.shareSection}>
        <View style={{ marginBottom: spacing.xl, alignItems: "center" }}>
          <QRCode value={roomSafe.inviteUrl} size={150} color="#000000" backgroundColor="#ffffff" />
        </View>
        <AppButton title="Compartir enlace" onPress={handleShare} variant="secondary" style={{ marginBottom: spacing.lg }} />
        <Text style={styles.webNote}>
          Nota: la vista de invitados para web aún no está disponible.
        </Text>
      </View>

      {/* Sección de solicitudes */}
      <View style={styles.solicitudesSection}>
        <View style={styles.solicitudesHeader}>
          <Text style={styles.solicitudesTitle}>Solicitudes</Text>
          <AppButton
            title="Actualizar"
            onPress={handleRefreshSolicitudes}
            variant="secondary"
            iconName="refresh-outline"
            disabled={solicitudesLoading}
            style={styles.refreshButton}
          />
        </View>

        {solicitudesError && (
          <View style={styles.solicitudesErrorBanner}>
            <Text style={styles.solicitudesErrorText}>{solicitudesError}</Text>
          </View>
        )}

        {solicitudesLoading && solicitudes.length === 0 ? (
          <View style={styles.solicitudesCentered}>
            <ActivityIndicator size="small" color={colors.primary} />
          </View>
        ) : solicitudes.length === 0 && !solicitudesError ? (
          <View style={styles.solicitudesCentered}>
            <Ionicons name="musical-notes-outline" size={32} color={colors.neutral} />
            <Text style={styles.solicitudesEmptyText}>No hay solicitudes todavía.</Text>
          </View>
        ) : (
          <>
            {solicitudes.map(s => (
              <View key={s.id} style={styles.solicitudCard}>
                <View style={styles.solicitudTopRow}>
                  <Text style={styles.solicitudNombre} numberOfLines={2}>{s.nombre}</Text>
                  <SolicitudBadge estado={s.estado} />
                </View>

                {(s.artista || s.generoNombre) && (
                  <View style={styles.solicitudMetaRow}>
                    {s.artista && (
                      <Text style={styles.solicitudMeta}>{s.artista}</Text>
                    )}
                    {s.artista && s.generoNombre && (
                      <Text style={styles.solicitudMetaDot}>·</Text>
                    )}
                    {s.generoNombre && (
                      <Text style={styles.solicitudMeta}>{s.generoNombre}</Text>
                    )}
                  </View>
                )}

                {s.enlace && (
                  <Text style={styles.solicitudEnlace} numberOfLines={1}>{s.enlace}</Text>
                )}

                <View style={styles.solicitudActions}>
                  {s.estado === "pendiente" && (
                    <>
                      <AppButton
                        title="Aceptar"
                        onPress={() => handleSolicitudAction(s, "aceptada")}
                        variant="primary"
                        disabled={actionLoadingId !== null}
                        loading={actionLoadingId === s.id}
                        style={styles.solicitudActionBtn}
                      />
                      <AppButton
                        title="Rechazar"
                        onPress={() => handleSolicitudAction(s, "rechazada")}
                        variant="danger"
                        disabled={actionLoadingId !== null}
                        style={styles.solicitudActionBtn}
                      />
                    </>
                  )}

                  {s.estado === "aceptada" && (
                    <>
                      <AppButton
                        title="Marcar reproducida"
                        onPress={() => handleSolicitudAction(s, "reproducida")}
                        variant="primary"
                        disabled={actionLoadingId !== null}
                        loading={actionLoadingId === s.id}
                        style={styles.solicitudActionBtn}
                      />
                      <AppButton
                        title="Rechazar"
                        onPress={() => handleSolicitudAction(s, "rechazada")}
                        variant="danger"
                        disabled={actionLoadingId !== null}
                        style={styles.solicitudActionBtn}
                      />
                    </>
                  )}

                  {(s.estado === "reproducida" || s.estado === "rechazada") && (
                    <Text style={styles.solicitudNoActions}>Sin acciones disponibles</Text>
                  )}
                </View>
              </View>
            ))}

            {solicitudesLoading && (
              <View style={styles.solicitudesCentered}>
                <ActivityIndicator size="small" color={colors.primary} />
              </View>
            )}

            {solicitudesHasMore && !solicitudesLoading && (
              <AppButton
                title="Cargar más"
                onPress={handleLoadMoreSolicitudes}
                variant="secondary"
                style={styles.loadMoreButton}
              />
            )}
          </>
        )}
      </View>
    </ScreenContainer>
  );
}

const styles = StyleSheet.create({
  container: {
    flexGrow: 1,
    padding: spacing.lg,
    backgroundColor: colors.surface,
  },
  centered: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: spacing.xl,
  },
  header: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    marginBottom: spacing.lg,
    paddingBottom: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  title: {
    ...typography.title,
    color: colors.textPrimary,
  },
  code: {
    ...typography.caption,
    color: colors.textSecondary,
  },
  info: {
    marginBottom: spacing.lg,
  },
  schedule: {
    marginBottom: spacing.lg,
  },
  scheduleRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.sm,
  },
  scheduleText: {
    ...typography.caption,
    color: colors.textSecondary,
    flexShrink: 1,
  },
  timeZone: {
    fontSize: 12,
    color: colors.textSecondary,
    marginTop: spacing.sm,
  },
  errorBanner: {
    padding: 12,
    backgroundColor: colors.danger,
    borderRadius: radius.control,
    marginBottom: spacing.lg,
  },
  errorBannerText: {
    color: colors.textPrimary,
    fontSize: 14,
  },
  endedBanner: {
    padding: 12,
    backgroundColor: colors.surfaceRaised,
    borderRadius: radius.control,
    marginBottom: spacing.lg,
  },
  endedBannerText: {
    color: colors.textSecondary,
    fontSize: 14,
  },
  actions: {
    flexDirection: "row",
    gap: spacing.sm,
    marginBottom: spacing.lg,
    flexWrap: "wrap",
  },
  confirmBox: {
    padding: spacing.lg,
    backgroundColor: colors.surface,
    borderRadius: radius.control,
    marginBottom: spacing.lg,
  },
  confirmText: {
    ...typography.caption,
    color: colors.textSecondary,
    marginBottom: spacing.sm,
  },
  confirmActions: {
    flexDirection: "row",
    gap: spacing.sm,
    flexWrap: "wrap",
  },
  qrSection: {
    marginTop: spacing.lg,
    padding: spacing.lg,
    backgroundColor: colors.background,
    borderRadius: radius.control,
  },
  qrTitle: {
    ...typography.caption,
    color: colors.textSecondary,
    fontSize: 14,
    fontWeight: "bold",
    marginBottom: spacing.sm,
    textAlign: "center",
  },
  inviteUrl: {
    fontSize: 13,
    color: colors.textSecondary,
    textAlign: "center",
    marginBottom: spacing.sm,
  },
  webNote: {
    marginTop: spacing.sm,
    fontSize: 12,
    color: colors.textSecondary,
    textAlign: "center",
  },
  errorText: {
    fontSize: 16,
    marginBottom: 12,
    textAlign: "center",
    color: colors.danger,
  },
  stateBox: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: spacing.xl,
  },
  nombreRow: {
    flexDirection: "column",
    alignItems: "flex-start",
    marginBottom: spacing.md,
  },
  nombreText: {
    ...typography.cardTitle,
    color: colors.textPrimary,
    fontWeight: "600",
    flexShrink: 1,
    minWidth: 0,
  },
  estadoRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.sm,
    marginBottom: spacing.md,
  },
  codigoBlock: {
    backgroundColor: colors.surfaceRaised,
    borderRadius: radius.control,
    padding: spacing.lg,
    marginBottom: spacing.md,
  },
  codigoLabel: {
    ...typography.caption,
    color: colors.textSecondary,
    marginBottom: spacing.xs,
  },
  codigoValue: {
    ...typography.caption,
    color: colors.textPrimary,
  },
  scheduleSection: {
    marginBottom: spacing.lg,
  },
  shareSection: {
    flexDirection: "column",
    alignItems: "center",
    marginTop: spacing.lg,
    padding: spacing.lg,
    backgroundColor: colors.background,
    borderRadius: radius.control,
  },
  solicitudesSection: {
    marginTop: spacing.xl,
    padding: spacing.lg,
    backgroundColor: colors.surface,
    borderRadius: radius.card,
  },
  solicitudesHeader: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    marginBottom: spacing.lg,
  },
  solicitudesTitle: {
    ...typography.cardTitle,
    color: colors.textPrimary,
  },
  refreshButton: {
    minHeight: 36,
    paddingHorizontal: spacing.md,
  },
  solicitudesErrorBanner: {
    padding: spacing.md,
    backgroundColor: "rgba(248, 113, 113, 0.12)",
    borderColor: colors.danger,
    borderWidth: 1,
    borderRadius: radius.control,
    marginBottom: spacing.md,
  },
  solicitudesErrorText: {
    color: colors.danger,
    fontSize: 13,
    textAlign: "center",
  },
  solicitudesCentered: {
    alignItems: "center",
    padding: spacing.xl,
    gap: spacing.sm,
  },
  solicitudesEmptyText: {
    color: colors.textSecondary,
    fontSize: 14,
  },
  solicitudCard: {
    backgroundColor: colors.surfaceRaised,
    borderRadius: radius.control,
    padding: spacing.md,
    marginBottom: spacing.sm,
  },
  solicitudTopRow: {
    flexDirection: "row",
    alignItems: "flex-start",
    justifyContent: "space-between",
    gap: spacing.sm,
    marginBottom: spacing.xs,
  },
  solicitudNombre: {
    ...typography.body,
    color: colors.textPrimary,
    fontWeight: "600",
    flex: 1,
    minWidth: 0,
  },
  solicitudBadge: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: 8,
    minWidth: 80,
    alignItems: "center",
  },
  solicitudBadgeText: {
    fontSize: 11,
    fontWeight: "600",
  },
  solicitudMetaRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.xs,
    marginBottom: spacing.xs,
  },
  solicitudMeta: {
    fontSize: 13,
    color: colors.textSecondary,
  },
  solicitudMetaDot: {
    fontSize: 13,
    color: colors.neutral,
  },
  solicitudEnlace: {
    fontSize: 12,
    color: colors.primary,
    marginBottom: spacing.sm,
  },
  solicitudActions: {
    flexDirection: "row",
    gap: spacing.sm,
    marginTop: spacing.xs,
    flexWrap: "wrap",
    alignItems: "center",
  },
  solicitudActionBtn: {
    minHeight: 36,
    paddingHorizontal: spacing.md,
    flex: 0,
  },
  solicitudNoActions: {
    fontSize: 12,
    color: colors.neutral,
    fontStyle: "italic",
  },
  loadMoreButton: {
    marginTop: spacing.md,
  },
});