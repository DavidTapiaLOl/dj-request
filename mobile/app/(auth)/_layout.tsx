import * as React from "react";
import { View, ActivityIndicator, StyleSheet, Text } from "react-native";
import { Stack, Redirect } from "expo-router";
import { useAuth } from "../../src/auth/provider";
import { AppButton } from "../../src/components/AppButton";
import { colors, spacing } from "../../src/theme/theme";

export default function AuthLayout() {
  const { isLoading, loginStatus, restoreError, restoreSession } = useAuth();
  const [isRetrying, setIsRetrying] = React.useState(false);

  const handleRetry = async () => {
    setIsRetrying(true);
    try {
      await restoreSession();
    } finally {
      setIsRetrying(false);
    }
  };

  if (isLoading) {
    return (
      <View style={styles.loading}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  if (loginStatus === "authenticated") {
    return <Redirect href="/(app)" />;
  }

  if (restoreError === "network") {
    return (
      <View style={styles.error}>
        <Text style={styles.errorText}>Error de conexión al restaurar la sesión. Revisa tu conexión e inténtalo de nuevo.</Text>
        <AppButton
          title={isRetrying ? "Reintentando..." : "Reintentar"}
          onPress={handleRetry}
          disabled={isRetrying}
          loading={isRetrying}
          style={styles.retryButton}
        />
      </View>
    );
  }

  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="login" options={{ title: "Login" }} />
      <Stack.Screen name="register" options={{ title: "Registro" }} />
    </Stack>
  );
}

const styles = StyleSheet.create({
  loading: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    backgroundColor: colors.background,
  },
  error: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
    padding: spacing.xl,
    backgroundColor: colors.background,
  },
  errorText: {
    fontSize: 16,
    marginBottom: spacing.lg,
    textAlign: "center",
    color: colors.textPrimary,
  },
  retryButton: {
    minWidth: 160,
  },
});