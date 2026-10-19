import * as React from "react";
import { View, ActivityIndicator, Button, StyleSheet, Text } from "react-native";
import { Stack, Redirect } from "expo-router";
import { useAuth } from "../../src/auth/provider";

export default function AuthLayout() {
  const { isLoading, loginStatus, restoreError, setRestoreError } = useAuth();

  if (isLoading) {
    return (
      <View style={styles.loading}>
        <ActivityIndicator size="large" />
      </View>
    );
  }

  if (loginStatus === "authenticated") {
    return <Redirect href="/(app)" />;
  }

  if (restoreError) {
    return (
      <View style={styles.error}>
        <Text style={styles.errorText}>Error al restaurar la sesión. Por favor, inicia sesión de nuevo.</Text>
        <Button title="Reintentar" onPress={() => setRestoreError(undefined)} />
      </View>
    );
  }

  return (
    <Stack>
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
  },
  error: {
    padding: 20,
    textAlign: "center",
    color: "red",
  },
  errorText: {
    fontSize: 16,
    marginBottom: 10,
  },
});