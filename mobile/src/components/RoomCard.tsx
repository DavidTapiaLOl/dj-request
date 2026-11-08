import * as React from "react";
import { Pressable, StyleSheet, Text, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { RoomResponse } from "../api/dto";
import { StatusBadge } from "./StatusBadge";
import { formatRoomSchedule } from "../utils/dateFormat";
import { colors, radius, spacing, typography } from "../theme/theme";

interface RoomCardProps {
  room: RoomResponse;
  onPress: () => void;
}

export function RoomCard({ room, onPress }: RoomCardProps) {
  const schedule = React.useMemo(
    () => formatRoomSchedule(room.startsAt, room.endsAt, room.timezone),
    [room.startsAt, room.endsAt, room.timezone]
  );

  const scheduleLabel = schedule
    ? [schedule.primary, schedule.secondary].filter(Boolean).join(" – ")
    : null;

  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={`Ver sala ${room.nombre}`}
      style={({ pressed }) => [styles.card, pressed && styles.cardPressed]}
    >
      <View style={styles.nameRow}>
        <Text style={styles.name}>{room.nombre}</Text>
        <StatusBadge estado={room.estado} hasEnded={room.hasEnded} />
      </View>

      {scheduleLabel ? (
        <View style={styles.scheduleRow}>
          <Ionicons
            name="calendar-outline"
            size={16}
            color={colors.textSecondary}
          />
          <Text style={styles.scheduleText}>{scheduleLabel}</Text>
        </View>
      ) : null}

      <View style={styles.separator} />

      <View style={styles.codeRow}>
        <Ionicons
          name="ticket-outline"
          size={18}
          color={colors.primary}
        />
        <Text style={styles.codeLabel}>Código</Text>
        <Text style={styles.code}>{room.codigo}</Text>
      </View>

      <View style={styles.viewRoomButton}>
        <Text style={styles.viewRoomText}>Ver sala</Text>
        <Ionicons name="arrow-forward" size={18} color={colors.primary} />
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.card,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.lg,
    gap: spacing.md,
  },
  cardPressed: {
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.primary,
  },
  nameRow: {
    flexDirection: "row",
    alignItems: "flex-start",
    justifyContent: "space-between",
    gap: spacing.sm,
  },
  name: {
    ...typography.cardTitle,
    flex: 1,
    minWidth: 0,
    color: colors.textPrimary,
  },
  scheduleRow: {
    flexDirection: "row",
    alignItems: "flex-start",
    gap: spacing.sm,
  },
  scheduleText: {
    ...typography.caption,
    color: colors.textSecondary,
    flex: 1,
    minWidth: 0,
    lineHeight: 20,
  },
  separator: {
    height: 1,
    backgroundColor: colors.border,
  },
  codeRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.sm,
  },
  codeLabel: {
    ...typography.caption,
    color: colors.textSecondary,
  },
  code: {
    ...typography.caption,
    color: colors.textPrimary,
    fontWeight: "600",
    letterSpacing: 0.5,
  },
  viewRoomButton: {
    width: "100%",
    minHeight: 48,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    paddingHorizontal: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.control,
    backgroundColor: colors.surfaceRaised,
  },
  viewRoomText: {
    ...typography.body,
    fontWeight: "600",
    color: colors.textPrimary,
  },
});
