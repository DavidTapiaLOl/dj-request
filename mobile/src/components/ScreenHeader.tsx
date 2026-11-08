import * as React from "react";
import { Pressable, StyleSheet, Text, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useRouter } from "expo-router";
import { colors, layout, radius, spacing } from "../theme/theme";
import { BrandLogo } from "./BrandLogo";

interface ScreenHeaderProps {
  showBack?: boolean;
  showBrand?: boolean;
  rightAction?: {
    title: string;
    onPress: () => void;
    iconName?: keyof typeof Ionicons.glyphMap;
    showTitle?: boolean;
  };
}

export function ScreenHeader({
  showBack = true,
  showBrand = false,
  rightAction,
}: ScreenHeaderProps) {
  const router = useRouter();

  const handleBack = () => {
    if (router.canGoBack()) {
      router.back();
    } else {
      router.replace("/(app)");
    }
  };

  return (
    <View style={styles.container}>
      <View style={styles.leftSlot}>
        {showBack ? (
          <Pressable
            style={({ pressed }) => [
              styles.iconButton,
              pressed && styles.iconButtonPressed,
            ]}
            onPress={handleBack}
            accessibilityRole="button"
            accessibilityLabel="Volver"
          >
            <Ionicons
              name="arrow-back"
              size={22}
              color={colors.textPrimary}
            />
          </Pressable>
        ) : showBrand ? (
          <BrandLogo
            size={32}
            orientation="inline"
            showWordmark
            wordmarkSize={20}
          />
        ) : null}
      </View>

      {showBack && showBrand ? (
        <View style={styles.centerSlot}>
          <BrandLogo
            size={32}
            orientation="inline"
            showWordmark
            wordmarkSize={20}
          />
        </View>
      ) : (
        <View style={styles.flexSpacer} />
      )}

      <View style={styles.rightSlot}>
        {rightAction ? (
          <Pressable
            onPress={rightAction.onPress}
            accessibilityRole="button"
            accessibilityLabel={rightAction.title}
            style={({ pressed }) => [
              styles.iconButton,
              rightAction.showTitle && styles.textButton,
              pressed && styles.iconButtonPressed,
            ]}
          >
            <Ionicons
              name={rightAction.iconName || "menu"}
              size={21}
              color={colors.textSecondary}
            />
            {rightAction.showTitle ? (
              <Text style={styles.rightText}>{rightAction.title}</Text>
            ) : null}
          </Pressable>
        ) : null}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    minHeight: 56,
    flexDirection: "row",
    alignItems: "center",
    paddingVertical: spacing.sm,
  },
  leftSlot: {
    minWidth: layout.minTouchTarget,
    alignItems: "flex-start",
    justifyContent: "center",
  },
  centerSlot: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
  },
  flexSpacer: {
    flex: 1,
  },
  rightSlot: {
    minWidth: layout.minTouchTarget,
    alignItems: "flex-end",
    justifyContent: "center",
  },
  iconButton: {
    minWidth: layout.minTouchTarget,
    minHeight: layout.minTouchTarget,
    alignItems: "center",
    justifyContent: "center",
    borderRadius: radius.control,
  },
  textButton: {
    flexDirection: "row",
    gap: spacing.xs,
    paddingHorizontal: spacing.sm,
  },
  iconButtonPressed: {
    backgroundColor: colors.surface,
  },
  rightText: {
    color: colors.textSecondary,
    fontSize: 13,
    fontWeight: "600",
  },
});
