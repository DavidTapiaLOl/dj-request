import * as React from "react";
import { ActivityIndicator, Pressable, StyleProp, StyleSheet, Text, ViewStyle } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { colors, layout, radius, spacing } from "../theme/theme";

export type AppButtonVariant = "primary" | "secondary" | "danger";

export interface AppButtonProps {
  title: string;
  onPress: () => void;
  variant?: AppButtonVariant;
  disabled?: boolean;
  loading?: boolean;
  iconName?: keyof typeof Ionicons.glyphMap;
  style?: StyleProp<ViewStyle>;
}

export function AppButton({
  title,
  onPress,
  variant = "primary",
  disabled = false,
  loading = false,
  iconName,
  style,
}: AppButtonProps) {
  const isBlocked = disabled || loading;
  const contentColor = variant === "primary" ? colors.textPrimary : colors.primary;

  return (
    <Pressable
      onPress={onPress}
      disabled={isBlocked}
      accessibilityRole="button"
      accessibilityLabel={title}
      accessibilityState={{ disabled, busy: loading }}
      style={({ pressed }) => [
        styles.base,
        variant === "primary" ? styles.primary : styles.secondary,
        pressed && !isBlocked && (variant === "primary" ? styles.primaryPressed : variant === "secondary" ? styles.secondaryPressed : styles.dangerPressed),
        isBlocked && styles.blocked,
        style,
      ]}
    >
      {loading ? (
        <ActivityIndicator size="small" color={contentColor} />
      ) : (
        <>
          {iconName ? <Ionicons name={iconName} size={18} color={contentColor} /> : null}
          <Text style={styles.label}>{title}</Text>
        </>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    gap: spacing.sm,
    minHeight: layout.minTouchTarget,
    paddingHorizontal: spacing.lg,
    borderRadius: radius.control,
    borderWidth: 1,
  },
  primary: {
    backgroundColor: colors.primary,
    borderColor: colors.primary,
  },
  primaryPressed: {
    backgroundColor: colors.primaryPressed,
    borderColor: colors.primaryPressed,
  },
  secondary: {
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.border,
  },
  secondaryPressed: {
    backgroundColor: colors.surface,
    borderColor: colors.primary,
  },
  danger: {
    backgroundColor: colors.danger,
    borderColor: colors.danger,
  },
  dangerPressed: {
    backgroundColor: colors.danger,
    borderColor: colors.danger,
  },
  blocked: {
    opacity: 0.6,
  },
  label: {
    fontSize: 16,
    fontWeight: "600",
    color: colors.textPrimary,
    textAlign: "center",
  },
});
