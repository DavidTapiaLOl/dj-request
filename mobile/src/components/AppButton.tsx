import * as React from "react";
import {
  ActivityIndicator,
  Animated,
  Platform,
  Pressable,
  StyleProp,
  StyleSheet,
  Text,
  ViewStyle,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { colors, layout, motion, radius, spacing } from "../theme/theme";
import { useReducedMotion } from "../theme/useReducedMotion";

export type AppButtonVariant = "primary" | "secondary" | "outline" | "danger";

export interface AppButtonProps {
  title: string;
  onPress: () => void;
  variant?: AppButtonVariant;
  disabled?: boolean;
  loading?: boolean;
  iconName?: keyof typeof Ionicons.glyphMap;
  style?: StyleProp<ViewStyle>;
}

const AnimatedPressable = Animated.createAnimatedComponent(Pressable);

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
  const reduceMotion = useReducedMotion();
  const scale = React.useRef(new Animated.Value(1)).current;
  const [isPressed, setIsPressed] = React.useState(false);

  const contentColor =
    variant === "primary"
      ? colors.onPrimary
      : variant === "secondary"
        ? colors.textPrimary
        : variant === "outline"
          ? colors.primary
          : colors.danger;

  const animateScale = React.useCallback(
    (toValue: number) => {
      if (reduceMotion !== false) {
        scale.setValue(1);
        return;
      }

      Animated.timing(scale, {
        toValue,
        duration: motion.durationFast,
        useNativeDriver: Platform.OS !== "web",
      }).start();
    },
    [reduceMotion, scale]
  );

  return (
    <AnimatedPressable
      onPress={onPress}
      onPressIn={() => {
        setIsPressed(true);
        animateScale(motion.pressScale);
      }}
      onPressOut={() => {
        setIsPressed(false);
        animateScale(1);
      }}
      disabled={isBlocked}
      accessibilityRole="button"
      accessibilityLabel={title}
      accessibilityState={{ disabled, busy: loading }}
      style={[
        styles.base,
        variant === "primary"
          ? styles.primary
          : variant === "secondary"
            ? styles.secondary
            : variant === "outline"
              ? styles.outline
              : styles.danger,
        isPressed && !isBlocked
          ? variant === "primary"
            ? styles.primaryPressed
            : variant === "secondary"
              ? styles.secondaryPressed
              : variant === "outline"
                ? styles.outlinePressed
                : styles.dangerPressed
          : null,
        isBlocked && styles.blocked,
        { transform: [{ scale }] },
        style,
      ]}
    >
      {loading ? (
        <ActivityIndicator size="small" color={contentColor} />
      ) : (
        <>
          {iconName ? (
            <Ionicons name={iconName} size={18} color={contentColor} />
          ) : null}
          <Text style={[styles.label, { color: contentColor }]}>{title}</Text>
        </>
      )}
    </AnimatedPressable>
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
  outline: {
    backgroundColor: "transparent",
    borderColor: colors.primary,
  },
  outlinePressed: {
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.primary,
  },
  danger: {
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.danger,
  },
  dangerPressed: {
    backgroundColor: colors.background,
    borderColor: colors.danger,
  },
  blocked: {
    opacity: 0.6,
  },
  label: {
    fontSize: 16,
    fontWeight: "600",
    textAlign: "center",
  },
});
