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

  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={`Ver sala ${room.nombre}`}
      style={({ pressed }) => [styles.card, pressed && styles.cardPressed]}
    >
      <View style={styles.topRow}>
        <Text style={styles.name}>{room.nombre}</Text>
        <Ionicons name="chevron-forward" size={22} color={colors.textSecondary} style={styles.chevron} />
      </View>

      <StatusBadge estado={room.estado} hasEnded={room.hasEnded} />

      {schedule ? (
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
          {schedule.timeZone ? (
            <Text style={styles.timeZone}>Zona horaria: {schedule.timeZone}</Text>
          ) : null}
        </View>
      ) : null}

      <View style={styles.footer}>
        <Text style={styles.code}>Código {room.codigo}</Text>
        <View style={styles.openRow}>
          <Text style={styles.openText}>Ver sala</Text>
          <Ionicons name="arrow-forward" size={16} color={colors.primary} />
        </View>
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
  topRow: {
    flexDirection: "row",
    alignItems: "flex-start",
    justifyContent: "space-between",
    gap: spacing.sm,
  },
  name: {
    ...typography.cardTitle,
    flex: 1,
    color: colors.textPrimary,
  },
  chevron: {
    marginTop: 2,
  },
  schedule: {
    gap: spacing.xs,
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
    marginTop: 2,
  },
  footer: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border,
    paddingTop: spacing.md,
  },
  code: {
    ...typography.caption,
    color: colors.textSecondary,
    flexShrink: 1,
  },
  openRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.xs,
    minHeight: 24,
  },
  openText: {
    fontSize: 14,
    fontWeight: "600",
    color: colors.primary,
  },
});
