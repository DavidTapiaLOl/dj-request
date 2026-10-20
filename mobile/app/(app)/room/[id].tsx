import * as React from "react";
import {
  View,
  Text,
  Button,
  ActivityIndicator,
  RefreshControl,
  ScrollView,
  StyleSheet,
  Share,
  Platform,
} from "react-native";
import { useLocalSearchParams, useRouter } from "expo-router";
import { request, ApiError, NetworkError } from "@/api/client";
import { RoomResponse } from "@/api/dto";
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
  if (!id) {
    return (
      <View style={styles.centered}>
        <Text style={styles.errorText}>Identificador de sala inválido.</Text>
        <AppButton title="Volver" variant="secondary" onPress={() => router.back()} />
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
          <AppButton title="Volver" variant="secondary" onPress={() => router.back()} />
        </View>
      </ScreenContainer>
    );
  }

  if (!room) {
    return (
      <ScreenContainer>
        <View style={styles.centered}>
          <Text style={styles.errorText}>La sala no está disponible o ha sido eliminada.</Text>
          <AppButton title="Volver" variant="secondary" onPress={() => router.back()} />
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
      <ScreenHeader
        showBack={true}
        onBack={() => router.back()}
      />

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
});