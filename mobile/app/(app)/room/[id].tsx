import * as React from "react";
import {
  View,
  Text,
  TextInput,
  Button,
  ActivityIndicator,
  TouchableOpacity,
  Alert,
  ScrollView,
  SafeAreaView,
  Share,
  Platform,
  ToastAndroid,
  Keyboard,
  StyleSheet,
  Image,
  Clipboard,
} from "react-native";
import QrCode from "react-native-qrcode-svg";
import { Ionicons } from "@expo/vector-icons";
import { useNavigation, useRoute } from "@react-navigation/native";
import { useEffect, useState } from "react";

import { request } from "../../../src/api/client";
import { useAuth } from "../../../src/auth/provider";
import { RoomResponse } from "../../../src/api/dto";

export default function RoomDetailScreen() {
  const navigation = useNavigation();
  const route = useRoute();
  const { id } = route.params || {};
  const { user } = useAuth();

  const [room, setRoom] = useState<RoomResponse | null>(null);
  const [state, setState] = React.useState<"abierta" | "pausada" | "cerrada">("abierta");
  const [version, setVersion] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showShare, setShowShare] = useState(false);
  const [isClosing, setIsClosing] = useState(false);

  // Load room detail
  useEffect(() => {
    if (!id) {
      setError("ID de sala no proporcionado");
      setLoading(false);
      return;
    }

    async function loadRoom() {
      try {
        const result = await request<RoomResponse>("GET", `/api/rooms/${id}`, {
          headers: await useAuth().getAuthHeaders(),
        });
        setRoom(result);
        setVersion(result.version);
        setState(result.estado);
      } catch (err: any) {
        setError(err.detail || err.message || "Error al cargar la sala");
      } finally {
        setLoading(false);
      }
    }

    loadRoom();
  }, [id, user]);

  if (loading) {
    return (
      <SafeAreaView style={{ flex: 1, justifyContent: "center", alignItems: "center" }}>
        <ActivityIndicator size="large" />
      </SafeAreaView>
    );
  }

  if (error) {
    return (
      <SafeAreaView style={{ flex: 1, justifyContent: "center", alignItems: "center" }}>
        <Text style={{ fontSize: 18, color: "#ef4444" }}>{error}</Text>
        <Button title="Volver" onPress={() => navigation.goBack()} />
      </SafeAreaView>
    );
  }

  if (!room) {
    return (
      <SafeAreaView style={{ flex: 1, justifyContent: "center", alignItems: "center" }}>
        <Text>Sala no encontrada</Text>
        <Button title="Volver" onPress={() => navigation.goBack()} />
      </SafeAreaView>
    );
  }

  // Disable state changes if room has ended
  const canModify = !room.hasEnded;

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: "#f0e6ff" }}>
      <ScrollView contentContainerStyle={{ padding: 20, maxWidth: 400 }}>
        <Text style={{ fontSize: 24, fontWeight: "bold", marginBottom: 20 }}>
          Sala: {room.nombre}
        </Text>

        {/* Room details */}
        <View style={{ marginBottom: 20 }}>
          <Text style={{ fontSize: 14, color: "#64748b" }}>Código:</Text>
          <TextInput
            style={{
              width: "100%", height: 50,
              borderWidth: 1, borderColor: "#c0c0c0",
              borderRadius: 8, paddingHorizontal: 12,
              backgroundColor: "#fff", fontSize: 16,
              fontFamily: "monospace",
            }}
            editable={false}
            value={room.codigo}
          />
        </View>

        <View style={{ marginBottom: 20 }}>
          <Text style={{ fontSize: 14, color: "#64748b" }}>Código de invitación:</Text>
          <Text style={{ fontSize: 12, color: "#8a2be2", fontFamily: "monospace" }}>
            {room.inviteUrl}
          </Text>
        </View>

        {/* QR Code from inviteUrl */}
        <View style={{ marginBottom: 20, alignItems: "center" }}>
          <QrCode
            value={room.inviteUrl}
            size={200}
          />
          <Text style={{ fontSize: 12, marginTop: 8, color: "#64748b" }}>
            Escanear para unirte
          </Text>
        </View>

        {/* Share button */}
        <TouchableOpacity
          onPress={() => setShowShare(true)}
          style={{
            marginBottom: 20,
            padding: 12,
            backgroundColor: "#8a2be2",
            borderRadius: 8,
            alignItems: "center",
          }}
        >
          <Text style={{ color: "white", fontSize: 16 }}>Compartir código</Text>
        </TouchableOpacity>

        {/* State management - only if canModify */}
        {canModify && (
          <View style={{ marginBottom: 20 }}>
            <Text style={{ fontSize: 14, marginBottom: 8 }}>Cambiar estado</Text>
            <View style={{ flexDirection: "row", gap: 12 }}>
              <TouchableOpacity
                disabled={isClosing}
                onPress={() => handleStateChange("pausada")}
                style={[
                  styles.stateButton,
                  { backgroundColor: state === "pausada" ? "#8a2be2" : "#e5e7eb" },
                  { color: state === "pausada" ? "white" : "#64748b" },
                ]}
              >
                <Ionicons name="pause-circle" size={20} color="currentColor" />
                <Text style={{ fontSize: 14, marginLeft: 4 }}>Pausar</Text>
              </TouchableOpacity>

              <TouchableOpacity
                disabled={isClosing}
                onPress={() => handleStateChange("abierta")}
                style={[
                  styles.stateButton,
                  { backgroundColor: state === "abierta" ? "#8a2be2" : "#e5e7eb" },
                  { color: state === "abierta" ? "white" : "#64748b" },
                ]}
              >
                <Ionicons name="ios-outline" size={20} color="currentColor" />
                <Text style={{ fontSize: 14, marginLeft: 4 }}>Reabrir</Text>
              </TouchableOpacity>

              <TouchableOpacity
                disabled={!canModify || isClosing}
                onPress={() => handleStateChange("cerrada")}
                style={[
                  styles.stateButton,
                  { backgroundColor: "#ef4444", color: "white" },
                ]}
              >
                <Ionicons name="trash" size={20} color="currentColor" />
                <Text style={{ fontSize: 14, marginLeft: 4 }}>Cerrar</Text>
              </TouchableOpacity>
            </View>
          </View>
        )}

        {/* Note about web guests */}
        <View style={{ marginTop: 20, padding: 12, backgroundColor: "#f3f4f6", borderRadius: 8 }}>
          <Text style={{ fontSize: 12, color: "#64748b" }}>
            ⚠ La web de invitados aún está pendiente. El QR no permite pedir canciones en esta entrega.
          </Text>
        </View>

        {/* Share sheet */}
        {showShare && (
          <View style={{ flex: 1, justifyContent: "center", alignItems: "center" }}>
            <TouchableOpacity
              onPress={() => Share.share({
                url: room.inviteUrl,
                message: `Únete a la sala ${room.nombre} con código ${room.codigo}`,
              })}
              style={{
                width: 300, height: 44,
                backgroundColor: "#8a2be2",
                borderRadius: 8,
                justifyContent: "center",
                alignItems: "center",
              }}
            >
              <Text style={{ color: "white", fontSize: 16 }}>Compartir enlace</Text>
            </TouchableOpacity>
            <TouchableOpacity
              onPress={() => setShowShare(false)}
              style={{
                width: 300, height: 44,
                backgroundColor: "#647474",
                borderRadius: 8,
                marginTop: 16,
                justifyContent: "center",
                alignItems: "center",
              }}
            >
              <Text style={{ color: "white", fontSize: 16 }}>Cancelar</Text>
            </TouchableOpacity>
          </View>
        )}
      </ScrollView>
    </SafeAreaView>
  );

  async function handleStateChange(newState: "abierta" | "pausada" | "cerrada") {
    if (!room || !canModify) return;

    setIsClosing(true);
    try {
      const result = await request<RoomResponse>("PATCH", `/api/rooms/${room.id}/state`, {
        body: { state: newState, version: room.version },
        headers: await useAuth().getAuthHeaders(),
      });

      setState(newState);
      setVersion(result.version);
      setRoom(result);
      Alert.alert(
        "Éxito",
        `Sala ${newState === "cerrada" ? "cerrada" : newState === "pausada" ? "pausada" : "reabierta"}`
      );
    } catch (err: any) {
      if (err.status === 409) {
        Alert.alert(
          "La sala cambió",
          "Los datos de la sala han sido actualizados por otro usuario. Por favor, recarga la sala.",
          [{ text: "Entendido", onPress: () => loadRoom() }]
        );
        await loadRoom();
      } else {
        Alert.alert("Error", err.detail || err.message);
      }
    } finally {
      setIsClosing(false);
    }
  }
}

const styles = StyleSheet.create({
  stateButton: {
    flex: 1,
    height: 48,
    borderRadius: 8,
    justifyContent: "center",
    alignItems: "center",
    paddingHorizontal: 4,
  },
});