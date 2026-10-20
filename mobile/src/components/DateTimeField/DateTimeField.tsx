import * as React from "react";
import { View, Text, TextInput, StyleSheet } from "react-native";
import { colors, radius, spacing, typography } from "../../theme/theme";
import type { DateTimeFieldProps } from "./types";

export type { DateTimeFieldProps };

export function DateTimeField({ label, value, onChange, hasError }: DateTimeFieldProps) {
  return (
    <View style={styles.field}>
      <Text style={[styles.label, hasError && styles.labelFocus]}>{label}</Text>
      <TextInput
        value={value}
        onChangeText={onChange}
        placeholder="AAAA-MM-DD HH:mm"
        style={[styles.input, hasError && styles.inputError]}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  field: { marginBottom: spacing.md },
  label: {
    ...typography.caption,
    color: colors.textSecondary,
    marginBottom: spacing.xs,
  },
  labelFocus: {
    ...typography.caption,
    color: colors.primary,
  },
  input: {
    width: "100%",
    height: 48,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.control,
    paddingHorizontal: spacing.lg,
    fontSize: 16,
    backgroundColor: colors.surfaceRaised,
    color: colors.textPrimary,
  },
  inputError: {
    borderColor: colors.danger,
  },
});

export default DateTimeField;
