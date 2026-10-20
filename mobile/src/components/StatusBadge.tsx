import * as React from "react";
import { StyleSheet, Text, View } from "react-native";
import { colors, radius, spacing } from "../theme/theme";

interface StateConfig {
  accent: string;
  label: string;
}

const STATE_CONFIG: Record<string, StateConfig> = {
  abierta: { accent: colors.success, label: "Abierta" },
  pausada: { accent: colors.warning, label: "Pausada" },
  cerrada: { accent: colors.neutral, label: "Cerrada" },
};

interface StatusBadgeProps {
  estado: string;
  hasEnded?: boolean;
}

export function StatusBadge({ estado, hasEnded = false }: StatusBadgeProps) {
  const key = (estado || "").trim().toLowerCase();
  const config: StateConfig = STATE_CONFIG[key] ?? { accent: colors.neutral, label: estado || "Sin estado" };

  const a11yLabel = hasEnded ? `${config.label}, evento finalizado` : config.label;

  return (
    <View style={styles.row} accessible accessibilityLabel={a11yLabel}>
      <View style={[styles.badge, { borderColor: config.accent }]}>
        <View style={[styles.dot, { backgroundColor: config.accent }]} />
        <Text style={styles.text}>{config.label}</Text>
      </View>
      {hasEnded ? (
        <View style={[styles.badge, { borderColor: colors.neutral }]}>
          <View style={[styles.dot, { backgroundColor: colors.neutral }]} />
          <Text style={styles.text}>Finalizada</Text>
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: "row",
    flexWrap: "wrap",
    alignItems: "center",
    gap: spacing.sm,
  },
  badge: {
    flexDirection: "row",
    alignItems: "center",
    gap: 6,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs,
    borderRadius: radius.control,
    borderWidth: 1,
    backgroundColor: colors.background,
  },
  dot: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  text: {
    fontSize: 13,
    fontWeight: "600",
    color: colors.textPrimary,
  },
});
