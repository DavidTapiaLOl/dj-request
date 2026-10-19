import * as React from "react";
import { View, ActivityIndicator, Button, StyleSheet, Text } from "react-native";
import { Stack, Redirect } from "expo-router";
import { useAuth } from "../../src/auth/provider";

export default function AppLayout() {
  const { isLoading, loginStatus, restoreError } = useAuth();

  if (isLoading) {
    return (
      <View style={styles.loading}>
        <ActivityIndicator size="large" />
      </View>
    );
  }

  if (restoreError) {
    return (
      <View style={styles.error}>
        <Text style={styles.errorText}>Error al restaurar la sesión. Por favor, inicia sesión de nuevo.</Text>
        <Button title="Reintentar" onPress={() => {}} />
      </View>
    );
  }

  if (loginStatus === "unauthenticated") {
    return <Redirect href="/(auth)/login" />;
  }

  return (
    <Stack>
      <Stack.Screen name="index" options={{ title: "Mis salas" }} />
      <Stack.Screen name="create" options={{ title: "Crear sala" }} />
      <Stack.Screen name="room/[id]" options={{ title: "Sala" }} />
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