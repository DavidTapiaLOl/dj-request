import * as React from "react";
import {
  ActivityIndicator,
  Image,
  Linking,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useFocusEffect, useRouter } from "expo-router";
import { Ionicons } from "@expo/vector-icons";
import {
  ApiError,
  NetworkError,
  getMyProfile,
  resolveMediaUrl,
} from "@/api/client";
import type { ProfileResponse } from "@/api/dto";
import {
  socialPlatforms,
  type SocialPlatformKey,
} from "@/features/profile/profileForm";
import { ScreenContainer } from "@/components/ScreenContainer";
import { ScreenHeader } from "@/components/ScreenHeader";
import { AppButton } from "@/components/AppButton";
import { FadeInView } from "@/components/FadeInView";
import { colors, radius, spacing, typography } from "@/theme/theme";

const socialIcons: Record<SocialPlatformKey, keyof typeof Ionicons.glyphMap> = {
  instagram: "logo-instagram",
  facebook: "logo-facebook",
  tiktok: "logo-tiktok",
  youtube: "logo-youtube",
  soundcloud: "cloud-outline",
};

export default function ProfileScreen() {
  const router = useRouter();

  const [profile, setProfile] = React.useState<ProfileResponse | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);
  const [linkError, setLinkError] = React.useState<string | null>(null);

  const requestIdRef = React.useRef(0);

  const load = React.useCallback(async () => {
    const requestId = ++requestIdRef.current;
    setIsLoading(true);
    setError(null);

    try {
      const data = await getMyProfile();
      if (requestId !== requestIdRef.current) {
        return;
      }
      setProfile(data);
    } catch (err) {
      if (requestId !== requestIdRef.current) {
        return;
      }

      if (err instanceof NetworkError) {
        setError("Sin conexión con el servidor. Revisa tu red.");
      } else if (
        err instanceof ApiError &&
        (err.status === 401 || err.status === 403)
      ) {
        setError("Tu sesión no es válida. Inicia sesión de nuevo.");
      } else if (err instanceof ApiError) {
        setError(err.message || "No pudimos cargar tu perfil.");
      } else {
        setError("Error inesperado al cargar tu perfil.");
      }
    } finally {
      if (requestId === requestIdRef.current) {
        setIsLoading(false);
      }
    }
  }, []);

  useFocusEffect(
    React.useCallback(() => {
      load();
    }, [load])
  );

  const openLink = async (url: string) => {
    setLinkError(null);
    try {
      await Linking.openURL(url);
    } catch {
      setLinkError("No pudimos abrir ese enlace en este dispositivo.");
    }
  };

  const photoUrl = resolveMediaUrl(profile?.photoUrl);

  const socialRows = profile
    ? socialPlatforms.flatMap((platform) => {
        const url = profile.socialLinks?.[platform.key];
        return url ? [{ ...platform, url }] : [];
      })
    : [];

  return (
    <ScreenContainer>
      <ScreenHeader showBack />

      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.scrollContent}
        showsVerticalScrollIndicator={false}
      >
        {isLoading ? (
          <View style={styles.stateBox}>
            <ActivityIndicator size="large" color={colors.primary} />
            <Text style={styles.stateText}>Cargando tu perfil…</Text>
          </View>
        ) : error ? (
          <View style={styles.stateBox}>
            <Ionicons
              name="cloud-offline-outline"
              size={44}
              color={colors.danger}
            />
            <Text style={styles.errorText}>{error}</Text>
            <AppButton
              title="Reintentar"
              variant="secondary"
              onPress={load}
              style={styles.retryButton}
            />
          </View>
        ) : profile ? (
          <FadeInView style={styles.fade}>
            <View style={styles.hero}>
              {photoUrl ? (
                <Image
                  source={{ uri: photoUrl }}
                  style={styles.avatar}
                  accessibilityLabel={`Foto de ${profile.artistName}`}
                />
              ) : (
                <View style={styles.avatarPlaceholder}>
                  <Ionicons
                    name="person"
                    size={52}
                    color={colors.textSecondary}
                  />
                </View>
              )}

              <Text style={styles.name}>{profile.artistName}</Text>
              {profile.startYear ? (
                <Text style={styles.meta}>DJ desde {profile.startYear}</Text>
              ) : null}
            </View>

            <View style={styles.card}>
              <Text style={styles.sectionTitle}>Sobre mí</Text>
              <Text
                style={profile.description ? styles.description : styles.muted}
              >
                {profile.description || "Aún no has agregado una descripción."}
              </Text>
            </View>

            <View style={styles.card}>
              <Text style={styles.sectionTitle}>Redes</Text>
              {socialRows.length === 0 ? (
                <Text style={styles.muted}>
                  Aún no has agregado redes sociales.
                </Text>
              ) : (
                socialRows.map((row) => (
                  <Pressable
                    key={row.key}
                    onPress={() => openLink(row.url)}
                    accessibilityRole="link"
                    accessibilityLabel={`Abrir ${row.label}`}
                    style={({ pressed }) => [
                      styles.socialRow,
                      pressed && styles.socialRowPressed,
                    ]}
                  >
                    <Ionicons
                      name={socialIcons[row.key]}
                      size={22}
                      color={colors.primary}
                    />
                    <Text style={styles.socialLabel}>{row.label}</Text>
                    <Ionicons
                      name="open-outline"
                      size={18}
                      color={colors.textSecondary}
                    />
                  </Pressable>
                ))
              )}
            </View>

            {linkError ? (
              <Text style={styles.inlineError}>{linkError}</Text>
            ) : null}

            <AppButton
              title="Editar perfil"
              iconName="create-outline"
              onPress={() => router.push("/(app)/profile/edit")}
              style={styles.editButton}
            />

            <Text style={styles.visibilityNote}>
              Tu perfil es visible para los invitados de tus salas.
            </Text>
          </FadeInView>
        ) : null}
      </ScrollView>
    </ScreenContainer>
  );
}

const styles = StyleSheet.create({
  scrollView: {
    flex: 1,
  },
  scrollContent: {
    flexGrow: 1,
    paddingTop: spacing.sm,
    paddingBottom: spacing.xxl,
  },
  fade: {
    width: "100%",
    alignItems: "stretch",
  },
  stateBox: {
    flexGrow: 1,
    minHeight: 280,
    justifyContent: "center",
    alignItems: "center",
    gap: spacing.md,
    padding: spacing.xl,
  },
  stateText: {
    ...typography.caption,
    color: colors.textSecondary,
  },
  errorText: {
    fontSize: 15,
    color: colors.danger,
    textAlign: "center",
    maxWidth: 360,
  },
  retryButton: {
    minWidth: 180,
  },
  hero: {
    alignItems: "center",
    paddingTop: spacing.md,
    paddingBottom: spacing.xl,
  },
  avatar: {
    width: 120,
    height: 120,
    borderRadius: 60,
    borderWidth: 2,
    borderColor: colors.primary,
    backgroundColor: colors.surfaceRaised,
  },
  avatarPlaceholder: {
    width: 120,
    height: 120,
    borderRadius: 60,
    borderWidth: 2,
    borderColor: colors.border,
    backgroundColor: colors.surfaceRaised,
    alignItems: "center",
    justifyContent: "center",
  },
  name: {
    ...typography.title,
    color: colors.textPrimary,
    textAlign: "center",
    marginTop: spacing.lg,
  },
  meta: {
    ...typography.caption,
    color: colors.textSecondary,
    marginTop: spacing.xs,
  },
  card: {
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.card,
    padding: spacing.lg,
    marginBottom: spacing.lg,
  },
  sectionTitle: {
    ...typography.cardTitle,
    color: colors.textPrimary,
    marginBottom: spacing.sm,
  },
  description: {
    ...typography.body,
    color: colors.textPrimary,
    lineHeight: 23,
  },
  muted: {
    ...typography.body,
    color: colors.textSecondary,
  },
  socialRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: spacing.md,
    minHeight: 48,
    borderRadius: radius.control,
    paddingHorizontal: spacing.sm,
  },
  socialRowPressed: {
    backgroundColor: colors.surfaceRaised,
  },
  socialLabel: {
    ...typography.body,
    color: colors.textPrimary,
    flex: 1,
  },
  inlineError: {
    ...typography.small,
    color: colors.danger,
    textAlign: "center",
    marginBottom: spacing.md,
  },
  editButton: {
    width: "100%",
    marginTop: spacing.sm,
  },
  visibilityNote: {
    ...typography.small,
    color: colors.textSecondary,
    textAlign: "center",
    marginTop: spacing.md,
  },
});
