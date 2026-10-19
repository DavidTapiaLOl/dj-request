import * as React from "react";
import { useEffect } from "react";
import {
  View,
  Text,
  TextInput,
  Button,
  Alert,
  ActivityIndicator,
  RefreshControl,
  TouchableOpacity,
  ScrollView,
  StyleSheet,
  Linking,
} from "react-native";
import QRCode from "react-native-qrcode-svg";
import { Ionicons } from "@expo/vector-icons";
import { useAuth } from "@/auth/provider";
import { useRouter } from "expo-router";
import { request } from "@/api/client";
import { RoomResponse } from "@/api/dto";
import { getAuthHeaders } from "@/api/client";
export default function RoomDetailScreen() {
  const router = useRouter();
  const [id, setId] = React.useState<string | null>(null);

  useEffect(() => {
    // Try to get id from router params or URL
    const params: any = router;
    if (params?.params?.id) {
      setId(params.params.id);
    }
    // Fallback: try to extract from URL
    if (!id) {
      Linking.getInitialURL().then((initialURL) => {
        if (initialURL) {
          const url = new URL(initialURL, "http://localhost");
          const searchParams = new URLSearchParams(url.search);
          const initialId = searchParams.get("id");
          if (initialId) {
            setId(initialId);
          }
        }
      });
    }
  }, []);

  const [state, setState] = React.useState<string | null>(null);
  const [room, setRoom] = React.useState<RoomResponse | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [isSaving, setIsSaving] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const loadRoom = React.useCallback(
    async () => {
      if (!id) return;
      try {
        const headers = await getAuthHeaders();
        const response = await fetch(
          `${process.env.EXPO_PUBLIC_API_URL || "http://localhost:5000"}/api/rooms/${id}`,
          {
            method: "GET",
            headers: {
              "Content-Type": "application/json",
              ...headers,
            },
          }
        );

        if (response.status === 409) {
          const errorData = await response.json().catch(() => ({}));
          setError(errorData.detail || "Conflicto de estado. Recargando...");
          setTimeout(() => loadRoom(), 1500);
          return;
        }

        if (!response.ok) {
          const errorData = await response.json().catch(() => ({}));
          setError(errorData.detail || "No se pudo cargar la sala");
          return;
        }

        const data = await response.json();
        setRoom(data);
        setState(data.estado);
      } catch (err: any) {
        setError(err.message || "Error de red");
      } finally {
        setIsLoading(false);
      }
    },
    [id]
  );

  React.useEffect(() => {
    loadRoom();
  }, [loadRoom]);

  const handleStateChange = async (newState: string) => {
    if (!id || isSaving) return;
    setIsSaving(true);
    setError(null);
    try {
      const headers = await getAuthHeaders();
      const response = await fetch(
        `${process.env.EXPO_PUBLIC_API_URL || "http://localhost:5000"}/api/rooms/${id}/state`,
        {
          method: "PATCH",
          headers: {
            "Content-Type": "application/json",
            ...headers,
          },
          body: JSON.stringify({ estado: newState }),
        }
      );

      if (response.status === 409) {
        const errorData = await response.json().catch(() => ({}));
        setError(errorData.detail || "Conflicto de estado");
        setTimeout(() => loadRoom(), 1500);
        setIsSaving(false);
        return;
      }

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}));
        setError(errorData.detail || "Error al cambiar estado");
        setIsSaving(false);
        return;
      }

      const data = await response.json();
      setState(newState);
      setIsSaving(false);
    } catch (err: any) {
      if (err instanceof Error && err.message.includes("Network error")) {
        Alert.alert("Error de red", "No se puede conectar con el servidor");
      } else {
        Alert.alert("Error", "Error al cambiar el estado de la sala");
      }
      setIsSaving(false);
    }
  };

  const handleClose = async () => {
    const confirmed = await Alert.alert(
      "Confirmar cierre",
      "¿Estás seguro de que quieres cerrar la sala? Esto desconectará a los participantes.",
      [
        { text: "Cancelar", style: "cancel" },
        {
          text: "Cerrar",
          style: "destructive",
          onPress: async () => {
            await handleStateChange("cerrada");
          },
        },
      ]
    );
  };

  const handleShareQR = async () => {
    if (!room?.inviteUrl) {
      Alert.alert("Error", "No hay URL de invitación disponible");
      return;
    }

    try {
      await Linking.openURL(room.inviteUrl);
    } catch (err) {
      Alert.alert("Error", "No se pudo compartir la QR");
    }
  };

  if (!room) {
    return isLoading ? (
      <ActivityIndicator size="large" />
    ) : (
      <Text>Sala no encontrada</Text>
    );
  }

  return (
    <ScrollView
      refreshControl={
        <RefreshControl
          refreshing={isLoading}
          onRefresh={() => loadRoom()}
          tintColor="#8a2be2"
        />
      }
      style={{ flexGrow: 1 }}
    >
      <View style={styles.container}>
        <View style={styles.header}>
          <Text style={styles.title}>{room.nombre}</Text>
          <Text style={styles.code}>Código: {room.codigo}</Text>
        </View>

        <View style={styles.info}>
          <Text style={styles.label}>Estado:</Text>
          <Text style={styles.value}>{state || "cargando..."}</Text>
        </View>

        {state === "abierta" && (
          <View style={styles.actions}>
            <Button title="Pausar" onPress={() => handleStateChange("pausada") } />
            <Button title="Cerrar" onPress={handleClose} color="red" />
          </View>
        )}

        {state === "pausada" && (
          <View style={styles.actions}>
            <Button title="Reabrir" onPress={() => handleStateChange("abierta") } />
            <Button title="Cerrar" onPress={handleClose} color="red" />
          </View>
        )}

        {state === "cerrada" && (
          <View style={styles.actions}>
            <Button title="Reactivar" onPress={() => handleStateChange("abierta")} />
          </View>
        )}

        <View style={styles.qrSection}>
          <Text style={styles.qrTitle}>Código QR</Text>
          <QRCode
            value={room.inviteUrl}
            size={200}
            color="#8a2be2"
          />
          <Button title="Compartir QR" onPress={handleShareQR} />
        </View>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flexGrow: 1,
    padding: 20,
    backgroundColor: "#f8f9fa",
  },
  header: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 20,
    paddingBottom: 15,
    borderBottomWidth: 1,
    borderBottomColor: "#dee2e6",
  },
  title: {
    fontSize: 22,
    fontWeight: "bold",
  },
  code: {
    fontSize: 14,
    color: "#666",
  },
  info: {
    marginBottom: 20,
  },
  label: {
    fontSize: 14,
    color: "#666",
    marginBottom: 4,
  },
  value: {
    fontSize: 18,
    marginTop: 2,
  },
  actions: {
    flexDirection: "row",
    gap: 10,
    marginBottom: 15,
  },
  qrSection: {
    marginTop: 20,
    padding: 15,
    backgroundColor: "#fff",
    borderRadius: 8,
  },
  qrTitle: {
    fontSize: 14,
    fontWeight: "bold",
    marginBottom: 8,
    textAlign: "center",
  },
});