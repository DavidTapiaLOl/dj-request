import * as React from "react";
import { View, Text, StyleSheet } from "react-native";
import {
  colors,
  inputTypography,
  radius,
  spacing,
} from "../../theme/theme";
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

export default function DateTimeField({
  label,
  value,
  onChange,
  hasError,
  labelPosition = "above",
}: DateTimeFieldProps) {
  const [focused, setFocused] = React.useState(false);

  const handleChange = React.useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const next = fromDatetimeLocal(event.target.value);
      onChange(next);
    },
    [onChange]
  );

  const borderColor = hasError
    ? colors.danger
    : focused
      ? colors.primary
      : colors.border;

  const inputStyle: React.CSSProperties = {
    width: "100%",
    minHeight: 54,
    border: `1px solid ${borderColor}`,
    borderRadius: radius.control,
    paddingLeft: spacing.lg,
    paddingRight: spacing.md,
    fontSize: inputTypography.fontSize,
    fontWeight: inputTypography.fontWeight,
    fontFamily: inputTypography.fontFamily,
    backgroundColor: colors.surfaceRaised,
    color: colors.textPrimary,
    WebkitTextFillColor: colors.textPrimary,
    WebkitBoxShadow: `0 0 0 1000px ${colors.surfaceRaised} inset`,
    boxSizing: "border-box",
    colorScheme: "dark",
    outline: "none",
  };

  const input = (
    <input
      type="datetime-local"
      value={toDatetimeLocal(value)}
      onChange={handleChange}
      onFocus={() => setFocused(true)}
      onBlur={() => setFocused(false)}
      step={60}
      aria-label={label}
      style={
        labelPosition === "inside"
          ? {
              ...inputStyle,
              minHeight: 28,
              border: "none",
              borderRadius: 0,
              padding: 0,
              backgroundColor: "transparent",
              WebkitBoxShadow: "none",
            }
          : inputStyle
      }
    />
  );

  return (
    <View style={styles.field}>
      {labelPosition === "above" ? (
        <Text style={[styles.label, focused && styles.labelFocus]}>
          {label}
        </Text>
      ) : null}
      {labelPosition === "inside" ? (
        <View
          style={[
            styles.shell,
            {
              borderColor,
              backgroundColor: colors.surfaceRaised,
            },
          ]}
        >
          <Text style={styles.innerLabel}>{label}</Text>
          {input}
        </View>
      ) : (
        input
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  field: { marginBottom: spacing.lg },
  label: {
    fontSize: 14,
    fontWeight: "500",
    color: colors.textSecondary,
    marginBottom: spacing.sm,
  },
  labelFocus: {
    color: colors.primary,
  },
  shell: {
    width: "100%",
    minHeight: 62,
    borderWidth: 1,
    borderRadius: radius.control,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
    justifyContent: "center",
  },
  innerLabel: {
    ...inputTypography,
    fontSize: 12,
    color: colors.textSecondary,
    marginBottom: 2,
  },
});
