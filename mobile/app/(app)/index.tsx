import * as React from "react";
import {
  View,
  Text,
  RefreshControl,
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
import { ScreenHeader } from "@/components/ScreenHeader";
import { AppButton } from "@/components/AppButton";
import { RoomCard } from "@/components/RoomCard";
import { FadeInView } from "@/components/FadeInView";
import { resolveRoomsScreenState } from "@/features/rooms/roomsScreenState";
import { colors, radius, spacing, typography } from "@/theme/theme";

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
  const [hasCompletedInitialLoad, setHasCompletedInitialLoad] =
    React.useState(false);

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
          if (reset) {
            setHasCompletedInitialLoad(true);
          }
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

      loadPage(1, true);
    }, [loginStatus, loadPage])
  );

  const screenState = resolveRoomsScreenState({
    hasCompletedInitialLoad,
    isLoading,
    error,
    roomCount: rooms.length,
  });

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

  const artistLabel = (user?.artistName ?? "").trim() || user?.email || "";

  const handleLogout = () => {
    logout().catch(() => {
      // El estado de sesión local se conserva si el cierre falla.
    });
  };

  return (
    <ScreenContainer>
      <ScreenHeader
        showBack={false}
        showBrand
        rightAction={{
          title: "Cerrar sesión",
          onPress: handleLogout,
          iconName: "log-out-outline",
        }}
      />

      <FadeInView style={styles.fade}>
        <View style={styles.headerTexts}>
          {artistLabel ? (
            <Text style={styles.greeting}>Hola, {artistLabel}</Text>
          ) : null}
          <Text style={styles.title}>Mis salas</Text>
        </View>

        {screenState.showCreateBar ? (
          <View style={styles.createBar}>
            <AppButton
              title="Crear sala"
              iconName="add"
              onPress={() => router.push("/(app)/create")}
              style={styles.createButton}
            />
          </View>
        ) : null}

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
            const { layoutMeasurement, contentOffset, contentSize } =
              nativeEvent;
            const isCloseToBottom =
              layoutMeasurement.height + contentOffset.y >=
              contentSize.height - 64;
            if (isCloseToBottom) {
              loadMore();
            }
          }}
          scrollEventThrottle={16}
        >
          {screenState.mode === "initialLoading" ? (
            <View style={styles.centeredFill}>
              <ActivityIndicator size="large" color={colors.primary} />
              <Text style={styles.loadingText}>Cargando tus salas…</Text>
            </View>
          ) : screenState.mode === "error" ? (
            <View style={styles.stateBox}>
              <Ionicons
                name="cloud-offline-outline"
                size={40}
                color={colors.danger}
              />
              <Text style={styles.errorText}>{error}</Text>
              <AppButton
                title="Reintentar"
                variant="secondary"
                loading={isLoading || isRefreshing}
                onPress={doRefresh}
                style={styles.retryButton}
              />
            </View>
          ) : screenState.mode === "empty" ? (
            <View style={styles.stateBox}>
              <Ionicons
                name="musical-notes-outline"
                size={44}
                color={colors.primary}
              />
              <Text style={styles.emptyTitle}>Aún no tienes salas</Text>
              <AppButton
                title="Crear mi primera sala"
                onPress={() => router.push("/(app)/create")}
                style={styles.emptyButton}
              />
            </View>
          ) : (
            <>
              {screenState.showInlineError && error ? (
                <View style={styles.inlineError}>
                  <Text style={styles.inlineErrorText}>{error}</Text>
                </View>
              ) : null}

              {rooms.map((room) => (
                <RoomCard
                  key={room.id}
                  room={room}
                  onPress={() => router.push(`/(app)/room/${room.id}`)}
                />
              ))}

              {isLoadingMore ? (
                <View style={styles.loadingMore}>
                  <ActivityIndicator size="small" color={colors.primary} />
                </View>
              ) : null}

              {!hasMore && rooms.length > 0 ? (
                <Text style={styles.endOfList}>No hay más salas.</Text>
              ) : null}
            </>
          )}
        </ScrollView>
      </FadeInView>
    </ScreenContainer>
  );
}

const styles = StyleSheet.create({
  fade: {
    flex: 1,
  },
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
  headerTexts: {
    paddingTop: spacing.sm,
    paddingBottom: spacing.lg,
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
  createBar: {
    paddingBottom: spacing.lg,
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
    textAlign: "center",
  },
  emptyButton: {
    width: "100%",
    maxWidth: 320,
    marginTop: spacing.sm,
  },
  retryButton: {
    minWidth: 180,
  },
  errorText: {
    fontSize: 15,
    color: colors.danger,
    textAlign: "center",
    maxWidth: 360,
  },
  inlineError: {
    padding: spacing.md,
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.danger,
    borderWidth: 1,
    borderRadius: radius.control,
  },
  inlineErrorText: {
    color: colors.danger,
    fontSize: 13,
    textAlign: "center",
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
