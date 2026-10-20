import * as React from "react";
import { StyleProp, StyleSheet, View, ViewStyle, ScrollView } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { colors, spacing } from "../theme/theme";

interface AuthScreenContainerProps {
  children: React.ReactNode;
  style?: StyleProp<ViewStyle>;
}

export function AuthScreenContainer({ children, style }: AuthScreenContainerProps) {
  return (
    <View style={styles.root}>
      <SafeAreaView style={styles.safe}>
        <ScrollView
          contentContainerStyle={styles.scrollContent}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          <View style={[styles.form, style]}>{children}</View>
        </ScrollView>
      </SafeAreaView>
    </View>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: colors.background,
  },
  safe: {
    flex: 1,
    backgroundColor: colors.background,
  },
  scrollContent: {
    flexGrow: 1,
    justifyContent: "center",
  },
  form: {
    width: "100%",
    maxWidth: 400,
    alignSelf: "center",
    paddingHorizontal: spacing.xl,
    paddingVertical: spacing.xl,
  },
});
