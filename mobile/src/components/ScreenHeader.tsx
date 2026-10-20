import * as React from "react";
import { View, Text, StyleSheet, TouchableOpacity } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { colors, spacing } from "../theme/theme";

interface ScreenHeaderProps {
  showBack?: boolean;
  onBack?: () => void;
  rightAction?: {
    title: string;
    onPress: () => void;
    iconName?: keyof typeof Ionicons.glyphMap;
  };
}

export function ScreenHeader({ showBack = true, onBack, rightAction }: ScreenHeaderProps) {
  return (
    <View style={styles.container}>
      <View style={styles.backArea}>
        {showBack && onBack && (
          <TouchableOpacity
            style={styles.backButton}
            onPress={onBack}
            accessibilityRole="button"
            accessibilityLabel="Volver"
          >
            <Ionicons name="arrow-back" size={20} color={colors.textSecondary} />
          </TouchableOpacity>
        )}
      </View>
      {rightAction && (
        <View style={styles.rightSlot}>
          <TouchableOpacity onPress={rightAction.onPress} accessibilityRole="button" accessibilityLabel={rightAction.title}>
            <Ionicons name={rightAction.iconName || "menu"} size={18} color={colors.textSecondary} />
            <Text style={styles.rightText}>{rightAction.title}</Text>
          </TouchableOpacity>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
    backgroundColor: "transparent",
  },
  backArea: {
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.sm,
  },
  backButton: {
    minWidth: 48,
    minHeight: 48,
    padding: 6,
    backgroundColor: "transparent",
  },
  rightSlot: {
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.xs,
  },
  rightText: {
    color: colors.textSecondary,
    fontSize: 12,
    marginLeft: spacing.xs,
  },
});