import * as React from "react";
import { View, ActivityIndicator, StyleSheet, Text } from "react-native";
import { Stack, Redirect } from "expo-router";
import { useAuth } from "../../src/auth/provider";
import { AppButton } from "../../src/components/AppButton";
import { colors, spacing } from "../../src/theme/theme";

export default function AppLayout() {
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

  if (isLoading || loginStatus === "checking") {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  if (loginStatus === "unauthenticated") {
    return <Redirect href="/(auth)/login" />;
  }

  if (restoreError === "network") {
    return (
      <View style={styles.centered}>
        <Text style={styles.errorText}>Error de conexión al restaurar la sesión. Revisa tu conexión.</Text>
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
      <Stack.Screen name="index" options={{ title: "Mis salas" }} />
      <Stack.Screen name="create" options={{ title: "Crear sala" }} />
      <Stack.Screen name="room/[id]" options={{ title: "Sala" }} />
    </Stack>
  );
}

const styles = StyleSheet.create({
  centered: {
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
