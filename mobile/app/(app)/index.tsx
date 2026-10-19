import * as React from "react";
import {
  View,
  Text,
  TextInput,
  Button,
  RefreshControl,
  ScrollView,
  StyleSheet,
  ActivityIndicator,
} from "react-native";
import { useRouter } from "expo-router";
import { useAuth } from "../../src/auth/provider";
import { RoomResponse, ListRoomsResponse } from "../../src/api/dto";
import { Ionicons } from "@expo/vector-icons";
import { getAuthHeaders } from "@/api/client";

export default function RoomsScreen() {
  const router = useRouter();
  const { user, isLoading: authIsLoading, loginStatus } = useAuth();

  if (authIsLoading) {
    return null;
  }

  if (loginStatus === "unauthenticated") {
    return null;
  }

  const [page, setPage] = React.useState(1);
  const [pageSize, setPageSize] = React.useState(20);
  const [rooms, setRooms] = React.useState<RoomResponse[]>([]);
  const [isLoading, setIsLoading] = React.useState(false);
  const [isLoadingMore, setIsLoadingMore] = React.useState(false);
  const [isRefreshing, setIsRefreshing] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);

  const loadRooms = async (pageNum: number, reset: boolean) => {
    setIsLoading(true);
    setError(null);
    try {
      const headers = await getAuthHeaders();
      const url = `${process.env.EXPO_PUBLIC_API_URL || "http://localhost:5000"}/api/rooms?page=${pageNum}&pageSize=${pageSize}`;
      const response = await fetch(url, {
        method: "GET",
        headers: {
          "Content-Type": "application/json",
          ...headers,
        },
      });
      const data: ListRoomsResponse = await response.json();
      setRooms(reset ? data : [...rooms, ...data]);
      setPage(pageNum + 1);
    } catch (err: any) {
      setError(err.message || "Error al cargar salas");
    } finally {
      setIsLoading(false);
    }
  };

  const doRefresh = async () => {
    setIsRefreshing(true);
    setPage(1);
    await loadRooms(1, true);
    setIsRefreshing(false);
  };

  const loadMore = async () => {
    if (isLoadingMore || isRefreshing) return;
    setIsLoadingMore(true);
    await loadRooms(page, false);
    setIsLoadingMore(false);
  };

  React.useEffect(() => {
    loadRooms(1, true);
  }, [loginStatus]);

  return (
    <ScrollView
      refreshControl={
        <RefreshControl
          refreshing={isRefreshing}
          onRefresh={doRefresh}
        />
      }
      style={{ flexGrow: 1 }}
    >
      {authIsLoading || loginStatus === "checking" ? (
        <ActivityIndicator size="large" />
      ) : error ? (
        <View style={styles.error}>
          <Text>{error}</Text>
          <Button title="Reintentar" onPress={doRefresh} />
        </View>
      ) : rooms.length === 0 ? (
        <View style={styles.empty}>
          <Text>No hay salas programadas. ¡Sé el primero en crear una!</Text>
          <Button title="Crear sala" onPress={() => router.push("/(app)/create")} />
        </View>
      ) : (
        rooms.map((room) => (
          <View key={room.id} style={styles.room}>
            <Text style={styles.roomTitle}>{room.nombre}</Text>
            <Text style={styles.roomCode}>Código: {room.codigo}</Text>
            <Ionicons name="chevron-forward" size={20} color="#888" />
          </View>
        ))
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  error: {
    padding: 20,
    textAlign: "center",
    color: "red",
  },
  empty: {
    padding: 40,
    textAlign: "center",
  },
  room: {
    padding: 15,
    borderBottomWidth: 1,
    borderBottomColor: "#ccc",
    alignItems: "center",
  },
  roomTitle: {
    fontSize: 18,
    fontWeight: "bold",
    marginBottom: 4,
  },
  roomCode: {
    fontSize: 14,
    color: "#666",
  },
});