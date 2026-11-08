import * as React from "react";
import { View, Text, TextInput, StyleSheet } from "react-native";
import {
  colors,
  inputTypography,
  radius,
  spacing,
  typography,
} from "../../theme/theme";
import type { DateTimeFieldProps } from "./types";

export type { DateTimeFieldProps };

export function DateTimeField({
  label,
  value,
  onChange,
  hasError,
  labelPosition = "above",
}: DateTimeFieldProps) {
  return (
    <View style={styles.field}>
      {labelPosition === "above" ? (
        <Text style={[styles.label, hasError && styles.labelFocus]}>
          {label}
        </Text>
      ) : null}
      <View
        style={[
          styles.inputShell,
          labelPosition === "inside" && styles.inputShellInside,
          hasError && styles.inputError,
        ]}
      >
        {labelPosition === "inside" ? (
          <Text style={styles.innerLabel}>{label}</Text>
        ) : null}
        <TextInput
          value={value}
          onChangeText={onChange}
          placeholder={labelPosition === "above" ? "AAAA-MM-DD HH:mm" : undefined}
          style={[
            styles.input,
            labelPosition === "inside" && styles.inputInside,
          ]}
          accessibilityLabel={label}
        />
      </View>
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
  inputShell: {
    width: "100%",
    minHeight: 54,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.control,
    backgroundColor: colors.surfaceRaised,
    justifyContent: "center",
  },
  inputShellInside: {
    minHeight: 62,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
  },
  innerLabel: {
    ...inputTypography,
    fontSize: 12,
    color: colors.textSecondary,
    marginBottom: 2,
  },
  input: {
    width: "100%",
    height: 52,
    paddingHorizontal: spacing.lg,
    ...inputTypography,
    backgroundColor: "transparent",
    color: colors.textPrimary,
  },
  inputInside: {
    height: 28,
    paddingHorizontal: 0,
  },
  inputError: {
    borderColor: colors.danger,
  },
});

export default DateTimeField;
