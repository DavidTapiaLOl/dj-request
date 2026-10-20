import * as React from "react";
import {
  View,
  Text,
  RefreshControl,
  Pressable,
  ScrollView,
  StyleSheet,
  ActivityIndicator,
} from "react-native";
import { useRouter, useFocusEffect } from "expo-router";
import { Ionicons } from "@expo/vector-icons";
import { useAuth } from "../../src/auth/provider";
import { RoomResponse } from "../../src/api/dto";
import { request, ApiError, NetworkError } from "@/api/client";
import { ScreenContainer } from "@/components/ScreenContainer";
import { AppButton } from "@/components/AppButton";
import { RoomCard } from "@/components/RoomCard";
import { colors, layout, radius, spacing, typography } from "@/theme/theme";

const PAGE_SIZE = 20;

export default function RoomsScreen() {
  const router = useRouter();
  const { loginStatus, user, logout } = useAuth();

  const [page, setPage] = React.useState(1);
  const [rooms, setRooms] = React.useState<RoomResponse[]>([]);
  const [isLoading, setIsLoading] = React.useState(false);
  const [isLoadingMore, setIsLoadingMore] = React.useState(false);
  const [isRefreshing, setIsRefreshing] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [hasMore, setHasMore] = React.useState(true);

  const requestIdRef = React.useRef(0);
  const mountedRef = React.useRef(true);

  React.useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const loadPage = React.useCallback(
    async (pageNum: number, reset: boolean) => {
      const requestId = ++requestIdRef.current;

      if (reset) {
        setIsLoading(true);
      } else {
        setIsLoadingMore(true);
      }
      setError(null);

      try {
        const data = await request<RoomResponse[]>(
          "GET",
          `/api/rooms?page=${pageNum}&pageSize=${PAGE_SIZE}`
        );

        if (!mountedRef.current || requestId !== requestIdRef.current) {
          return;
        }

        setRooms((prev) => (reset ? data : [...prev, ...data]));
        setHasMore(data.length === PAGE_SIZE);
        setPage(pageNum + 1);
      } catch (err) {
        if (!mountedRef.current || requestId !== requestIdRef.current) {
          return;
        }

        if (err instanceof ApiError) {
          if (err.status === 401 || err.status === 403) {
            setError("Tu sesión no es válida. Inicia sesión de nuevo.");
          } else {
            setError(err.message || "No se pudieron cargar las salas.");
          }
        } else if (err instanceof NetworkError) {
          setError("Sin conexión con el servidor. Revisa tu red.");
        } else {
          setError("Error inesperado al cargar las salas.");
        }
      } finally {
        if (mountedRef.current && requestId === requestIdRef.current) {
          setIsLoading(false);
          setIsLoadingMore(false);
        }
      }
    },
    []
  );

  const doRefresh = React.useCallback(async () => {
    setIsRefreshing(true);
    await loadPage(1, true);
    if (mountedRef.current) {
      setIsRefreshing(false);
    }
  }, [loadPage]);

  const loadMore = React.useCallback(async () => {
    if (isLoadingMore || isRefreshing || isLoading || !hasMore) return;
    await loadPage(page, false);
  }, [isLoadingMore, isRefreshing, isLoading, hasMore, page, loadPage]);

  const initialLoadDoneRef = React.useRef(false);

  useFocusEffect(
    React.useCallback(() => {
      if (loginStatus !== "authenticated") {
        return;
      }

      if (!initialLoadDoneRef.current) {
        initialLoadDoneRef.current = true;
        loadPage(1, true);
        return;
      }

      // Al volver del foco (creación o detalle), recargar la primera página.
      loadPage(1, true);
    }, [loginStatus, loadPage])
  );

  if (loginStatus === "checking") {
    return (
      <ScreenContainer>
        <View style={styles.centeredFill}>
          <ActivityIndicator size="large" color={colors.primary} />
          <Text style={styles.loadingText}>Cargando tus salas…</Text>
        </View>
      </ScreenContainer>
    );
  }

  const showInitialLoading = isLoading && rooms.length === 0 && !error;
  const showEmpty = !isLoading && !error && rooms.length === 0;
  const artistLabel = (user?.artistName ?? "").trim() || user?.email || "";

  const handleLogout = () => {
    logout().catch(() => {
      // El estado de sesión local se conserva si el cierre falla.
    });
  };

  return (
    <ScreenContainer>
      <View style={styles.header}>
        <View style={styles.headerTexts}>
          {artistLabel ? <Text style={styles.greeting}>Hola, {artistLabel}</Text> : null}
          <Text style={styles.title}>Mis salas</Text>
        </View>
        <Pressable
          onPress={handleLogout}
          accessibilityRole="button"
          accessibilityLabel="Cerrar sesión"
          hitSlop={8}
          style={({ pressed }) => [styles.logoutButton, pressed && styles.logoutPressed]}
        >
          <Ionicons name="log-out-outline" size={22} color={colors.textSecondary} />
        </Pressable>
      </View>

      <View style={styles.createBar}>
        <AppButton
          title="Crear sala"
          iconName="add"
          onPress={() => router.push("/(app)/create")}
          style={styles.createButton}
        />
      </View>

      <ScrollView
        style={styles.list}
        contentContainerStyle={styles.listContent}
        refreshControl={
          <RefreshControl
            refreshing={isRefreshing}
            onRefresh={doRefresh}
            tintColor={colors.primary}
            colors={[colors.primary]}
          />
        }
        onScroll={({ nativeEvent }) => {
          const { layoutMeasurement, contentOffset, contentSize } = nativeEvent;
          const isCloseToBottom =
            layoutMeasurement.height + contentOffset.y >= contentSize.height - 64;
          if (isCloseToBottom) {
            loadMore();
          }
        }}
        scrollEventThrottle={16}
      >
        {showInitialLoading ? (
          <View style={styles.centeredFill}>
            <ActivityIndicator size="large" color={colors.primary} />
            <Text style={styles.loadingText}>Cargando tus salas…</Text>
          </View>
        ) : error ? (
          <View style={styles.stateBox}>
            <Ionicons name="cloud-offline-outline" size={40} color={colors.danger} />
            <Text style={styles.errorText}>{error}</Text>
            <AppButton
              title="Reintentar"
              variant="secondary"
              loading={isRefreshing}
              onPress={doRefresh}
            />
          </View>
        ) : showEmpty ? (
          <View style={styles.stateBox}>
            <Ionicons name="musical-notes-outline" size={44} color={colors.primary} />
            <Text style={styles.emptyTitle}>Aún no tienes salas</Text>
            <Text style={styles.emptyText}>
              Crea tu primera sala para empezar a recibir peticiones.
            </Text>
            <AppButton
              title="Crear mi primera sala"
              iconName="add"
              onPress={() => router.push("/(app)/create")}
            />
          </View>
        ) : (
          rooms.map((room) => (
            <RoomCard
              key={room.id}
              room={room}
              onPress={() => router.push(`/(app)/room/${room.id}`)}
            />
          ))
        )}

        {isLoadingMore && (
          <View style={styles.loadingMore}>
            <ActivityIndicator size="small" color={colors.primary} />
          </View>
        )}

        {!hasMore && rooms.length > 0 && (
          <Text style={styles.endOfList}>No hay más salas.</Text>
        )}
      </ScrollView>
    </ScreenContainer>
  );
}

const styles = StyleSheet.create({
  centeredFill: {
    flexGrow: 1,
    minHeight: 240,
    justifyContent: "center",
    alignItems: "center",
    gap: spacing.md,
    padding: spacing.xl,
  },
  loadingText: {
    ...typography.caption,
    color: colors.textSecondary,
  },
  header: {
    flexDirection: "row",
    alignItems: "flex-start",
    justifyContent: "space-between",
    gap: spacing.sm,
    paddingTop: spacing.lg,
    paddingBottom: spacing.md,
  },
  headerTexts: {
    flex: 1,
  },
  greeting: {
    ...typography.caption,
    color: colors.textSecondary,
    marginBottom: spacing.xs,
  },
  title: {
    ...typography.title,
    color: colors.textPrimary,
  },
  logoutButton: {
    minWidth: layout.minTouchTarget,
    minHeight: layout.minTouchTarget,
    alignItems: "center",
    justifyContent: "center",
    borderRadius: radius.control,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface,
    paddingHorizontal: spacing.sm,
  },
  logoutPressed: {
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.primary,
  },
  createBar: {
    paddingBottom: spacing.md,
  },
  createButton: {
    width: "100%",
  },
  list: {
    flex: 1,
  },
  listContent: {
    flexGrow: 1,
    gap: spacing.md,
    paddingBottom: spacing.xxl,
  },
  stateBox: {
    flexGrow: 1,
    minHeight: 280,
    justifyContent: "center",
    alignItems: "center",
    gap: spacing.md,
    padding: spacing.xl,
  },
  emptyTitle: {
    ...typography.cardTitle,
    color: colors.textPrimary,
    marginTop: spacing.sm,
  },
  emptyText: {
    ...typography.caption,
    color: colors.textSecondary,
    textAlign: "center",
    maxWidth: 320,
  },
  errorText: {
    fontSize: 15,
    color: colors.danger,
    textAlign: "center",
    maxWidth: 360,
  },
  loadingMore: {
    alignItems: "center",
    paddingVertical: spacing.md,
  },
  endOfList: {
    textAlign: "center",
    ...typography.caption,
    color: colors.textSecondary,
    paddingVertical: spacing.lg,
  },
});
