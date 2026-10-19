import * as React from "react";
import {
  View,
  Text,
  FlatList,
  ActivityIndicator,
  TouchableOpacity,
  Alert,
  Keyboard,
  StyleSheet,
} from "react-native";
import { useNavigation, useRoute } from "@react-navigation/native";
import { useEffect, useState } from "react";

import { request } from "../../src/api/client";
import { useAuth } from "../../src/auth/provider";
import { useForm } from "react-hook-form";

export default function MyRoomsScreen() {
  const navigation = useNavigation();
  const route = useRoute();
  const { user } = useAuth();

  const [rooms, setRooms] = useState<RoomResponse[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [refreshing, setRefreshing] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);

  const [form] = useForm();
  const { watch } = form;

  const loadRooms = async (pageNum: number, reset = false) => {
    try {
      const result = await request<ListRoomsResponse>("GET", "/api/rooms", {
        headers: await getAuthHeaders(),
        // Note: The backend supports pagination via query params
        // but we need to pass them as part of the URL or handle differently
        // For now, we'll get the first page and the frontend will manage pagination
      });

      if (reset) {
        setRooms(result);
      } else {
        setRooms((prev) => [...prev, ...result]);
      }
    } catch (err) {
      console.error("Error loading rooms:", err);
    } finally {
      setRefreshing(false);
      setLoadingMore(false);
    }
  };

  useEffect(() => {
    loadRooms(1, true);
  }, [user]);

  const onRefresh = async () => {
    setRefreshing(true);
    setPage(1);
    await loadRooms(1, true);
  };

  const loadMore = async () => {
    setPage((prev) => prev + 1);
    setLoadingMore(true);
    await loadRooms(page);
  };

  if (user?.isLoading || user?.loginStatus === "checking") {
    return (
      <View style={{ flex: 1, justifyContent: "center", alignItems: "center" }}>
        <ActivityIndicator size="large" />
      </View>
    );
  }

  if (!user) {
    return (
      <View style={{ flex: 1, justifyContent: "center", alignItems: "center" }}>
        <Text>No hay sesión activa</Text>
        <Button title="Volver al login" onPress={() => navigation.navigate("Login")} />
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <FlatList
        data={rooms}
        keyExtractor={(item) => item.id}
        renderItem={({ item }) => (
          <TouchableOpacity
            style={styles.roomItem}
            onPress={() => navigation.navigate("(app)/room", { id: item.id })}
          >
            <View style={styles.roomInfo}>
              <Text style={styles.roomName}>{item.nombre}</Text>
              <Text style={styles.roomCode}>Código: {item.codigo}</Text>
              <Text style={styles.roomState}>
                Estado: {item.estado}{" "}
                {item.hasEnded ? " (finalizada)" : ""}
              </Text>
            </View>
            <ArrowForwardIos style={styles.arrow} />
          </TouchableOpacity>
        )}
        refreshControl={
          <RefreshControl
            refreshing={refreshing}
            onRefresh={onRefresh}
            title="Actualizando..."
          />
        }
        inverted
        horizontal
        showsHorizontalScrollIndicator={false}
      />
      {/* Empty state when no rooms */}
      {rooms.length === 0 && (
        <View style={styles.emptyState}>
          <Text style={styles.emptyText}>
            No tienes salas aún. <Button title="Crear sala" onPress={() => navigation.navigate("CreateRoom")} />
          </Text>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, padding: 20 },
  roomItem: {
    backgroundColor: "#fff",
    borderRadius: 12,
    padding: 16,
    marginRight: 12,
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 2,
    overflow: "hidden",
    minWidth: 150,
  },
  roomInfo: {
    flexDirection: "column",
    alignItems: "flex-start",
  },
  roomName: { fontSize: 18, fontWeight: "bold", marginBottom: 4 },
  roomCode: { fontSize: 14, color: "#64748b", marginBottom: 2 },
  roomState: { fontSize: 12, color: "#64748b" },
  arrow: { fontSize: 16, marginLeft: 8 },
  emptyState: {
    padding: 40,
    alignItems: "center",
    justifyContent: "center",
  },
  emptyText: { fontSize: 16, color: "#64748b", textAlign: "center" },
});