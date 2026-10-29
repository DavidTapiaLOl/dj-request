import * as React from "react";
import { View, Text, StyleSheet } from "react-native";
import { colors, radius, spacing } from "../../theme/theme";
import type { DateTimeFieldProps } from "./types";

export type { DateTimeFieldProps };

function toDatetimeLocal(value: string): string {
  if (!value) return "";
  return value.replace(" ", "T");
}

function fromDatetimeLocal(value: string): string {
  if (!value) return "";
  return value.replace("T", " ");
}

export default function DateTimeField({ label, value, onChange, hasError }: DateTimeFieldProps) {
  const handleChange = React.useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const next = fromDatetimeLocal(event.target.value);
      onChange(next);
    },
    [onChange]
  );

  return (
    <View style={styles.field}>
      <Text style={styles.label}>{label}</Text>
      <input
        type="datetime-local"
        value={toDatetimeLocal(value)}
        onChange={handleChange}
        step={60}
        style={{
          width: "100%",
          height: 48,
          border: `1px solid ${hasError ? colors.danger : colors.border}`,
          borderRadius: radius.control,
          paddingLeft: spacing.lg,
          paddingRight: spacing.lg,
          fontSize: 16,
          backgroundColor: colors.surfaceRaised,
          color: colors.textPrimary,
          WebkitTextFillColor: colors.textPrimary,
          WebkitBoxShadow: `0 0 0 1000px ${colors.surfaceRaised} inset`,
          boxSizing: "border-box",
          fontFamily: "inherit",
          colorScheme: "dark",
        }}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  field: { marginBottom: 12 },
  label: {
    fontSize: 14,
    fontWeight: "500",
    color: colors.textSecondary,
    marginBottom: 6,
  },
});
