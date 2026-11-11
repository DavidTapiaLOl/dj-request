import * as React from "react";
import {
  ActivityIndicator,
  Image,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  StyleSheet,
  Text,
  View,
} from "react-native";
import { useRouter } from "expo-router";
import { Ionicons } from "@expo/vector-icons";
import * as ImagePicker from "expo-image-picker";
import {
  ApiError,
  NetworkError,
  deleteProfilePhoto,
  getMyProfile,
  resolveMediaUrl,
  updateMyProfile,
  uploadProfilePhoto,
} from "@/api/client";
import type { ProfileResponse } from "@/api/dto";
import { useAuth } from "@/auth/provider";
import {
  MAX_ARTIST_NAME_LENGTH,
  MAX_DESCRIPTION_LENGTH,
  mapServerErrors,
  profileToForm,
  socialPlatforms,
  toUpdateProfileRequest,
  validateProfileForm,
  type ProfileFormErrors,
  type ProfileFormField,
  type ProfileFormValues,
  type SocialPlatformKey,
} from "@/features/profile/profileForm";
import { buildProfilePhotoUpload } from "@/features/profile/profilePhoto";
import { ScreenContainer } from "@/components/ScreenContainer";
import { ScreenHeader } from "@/components/ScreenHeader";
import { AppButton } from "@/components/AppButton";
import { AppTextField } from "@/components/AppTextField";
import { FadeInView } from "@/components/FadeInView";
import { colors, radius, spacing, typography } from "@/theme/theme";

const MAX_PHOTO_BYTES = 5 * 1024 * 1024;

type ScalarProfileField = "artistName" | "description" | "startYear";

const emptyForm: ProfileFormValues = {
  artistName: "",
  description: "",
  startYear: "",
  socialLinks: {
    instagram: "",
    facebook: "",
    tiktok: "",
    youtube: "",
    soundcloud: "",
  },
};

const allFields: ProfileFormField[] = [
  "artistName",
  "description",
  "startYear",
  ...socialPlatforms.map((platform) => platform.key),
];

function mapPhotoError(err: unknown): string {
  if (err instanceof ApiError) {
    const fileError = err.errors?.File?.[0];
    return fileError || err.message || "No pudimos actualizar tu foto.";
  }

  if (err instanceof NetworkError) {
    return "Sin conexión con el servidor. Revisa tu red.";
  }

  return "Error inesperado al actualizar tu foto.";
}

export default function EditProfileScreen() {
  const router = useRouter();
  const { updateArtistName } = useAuth();

  const [profile, setProfile] = React.useState<ProfileResponse | null>(null);
  const [values, setValues] = React.useState<ProfileFormValues>(emptyForm);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loadError, setLoadError] = React.useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = React.useState<ProfileFormErrors>({});
  const [touched, setTouched] = React.useState<
    Partial<Record<ProfileFormField, boolean>>
  >({});
  const [isSaving, setIsSaving] = React.useState(false);
  const [saveError, setSaveError] = React.useState<string | null>(null);
  const [savedMessage, setSavedMessage] = React.useState<string | null>(null);
  const [isPhotoUploading, setIsPhotoUploading] = React.useState(false);
  const [isRemovingPhoto, setIsRemovingPhoto] = React.useState(false);
  const [photoError, setPhotoError] = React.useState<string | null>(null);
  const [confirmRemovePhoto, setConfirmRemovePhoto] = React.useState(false);

  const currentYear = new Date().getFullYear();

  const load = React.useCallback(async () => {
    setIsLoading(true);
    setLoadError(null);

    try {
      const data = await getMyProfile();
      setProfile(data);
      setValues(profileToForm(data));
      setFieldErrors({});
      setTouched({});
    } catch (err) {
      if (err instanceof NetworkError) {
        setLoadError("Sin conexión con el servidor. Revisa tu red.");
      } else if (
        err instanceof ApiError &&
        (err.status === 401 || err.status === 403)
      ) {
        setLoadError("Tu sesión no es válida. Inicia sesión de nuevo.");
      } else if (err instanceof ApiError) {
        setLoadError(err.message || "No pudimos cargar tu perfil.");
      } else {
        setLoadError("Error inesperado al cargar tu perfil.");
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  React.useEffect(() => {
    load();
  }, [load]);

  const clearFieldError = (field: ProfileFormField) => {
    setFieldErrors((current) => {
      if (!current[field]) {
        return current;
      }
      const next = { ...current };
      delete next[field];
      return next;
    });
  };

  const updateField = (field: ScalarProfileField, value: string) => {
    setValues((current) => ({ ...current, [field]: value }));
    clearFieldError(field);
    setSavedMessage(null);
    setSaveError(null);
  };

  const updateSocial = (key: SocialPlatformKey, value: string) => {
    setValues((current) => ({
      ...current,
      socialLinks: { ...current.socialLinks, [key]: value },
    }));
    clearFieldError(key);
    setSavedMessage(null);
    setSaveError(null);
  };

  const validateField = (field: ProfileFormField) => {
    const errors = validateProfileForm(values, currentYear);
    setTouched((current) => ({ ...current, [field]: true }));
    setFieldErrors((current) => {
      const next = { ...current };
      if (errors[field]) {
        next[field] = errors[field];
      } else {
        delete next[field];
      }
      return next;
    });
  };

  const markAllTouched = () => {
    setTouched(
      Object.fromEntries(allFields.map((field) => [field, true])) as Partial<
        Record<ProfileFormField, boolean>
      >
    );
  };

  const handleSubmit = async () => {
    setSavedMessage(null);
    setSaveError(null);

    const errors = validateProfileForm(values, currentYear);
    markAllTouched();
    setFieldErrors(errors);

    if (Object.keys(errors).length > 0) {
      setSaveError("Revisa los campos marcados.");
      return;
    }

    setIsSaving(true);

    try {
      const updated = await updateMyProfile(toUpdateProfileRequest(values));
      setProfile(updated);
      setValues(profileToForm(updated));
      setFieldErrors({});
      setTouched({});
      updateArtistName(updated.artistName);
      setSavedMessage("Perfil guardado.");
    } catch (err) {
      if (err instanceof ApiError) {
        const serverErrors = mapServerErrors(err.errors);
        if (Object.keys(serverErrors).length > 0) {
          setFieldErrors(serverErrors);
          setTouched((current) => ({
            ...current,
            ...(Object.fromEntries(
              Object.keys(serverErrors).map((field) => [field, true])
            ) as Partial<Record<ProfileFormField, boolean>>),
          }));
          setSaveError("Revisa los campos marcados.");
        } else if (err.status === 401 || err.status === 403) {
          setSaveError("Tu sesión no es válida. Inicia sesión de nuevo.");
        } else {
          setSaveError(err.message || "No pudimos guardar tu perfil.");
        }
      } else if (err instanceof NetworkError) {
        setSaveError("Sin conexión con el servidor. Revisa tu red.");
      } else {
        setSaveError("Error inesperado al guardar tu perfil.");
      }
    } finally {
      setIsSaving(false);
    }
  };

  const handlePickPhoto = async () => {
    setPhotoError(null);
    setSavedMessage(null);

    try {
      const permission =
        await ImagePicker.requestMediaLibraryPermissionsAsync();

      if (!permission.granted) {
        setPhotoError("Necesitamos permiso para acceder a tus fotos.");
        return;
      }

      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ImagePicker.MediaTypeOptions.Images,
        allowsEditing: true,
        aspect: [1, 1],
        quality: 0.9,
      });

      if (result.canceled || !result.assets[0]) {
        return;
      }

      const asset = result.assets[0];

      if (asset.fileSize && asset.fileSize > MAX_PHOTO_BYTES) {
        setPhotoError("La imagen no puede superar 5 MB.");
        return;
      }

      const upload = await buildProfilePhotoUpload(asset);
      if (!upload) {
        setPhotoError("No pudimos leer la imagen seleccionada.");
        return;
      }

      if (upload.file && upload.file.size > MAX_PHOTO_BYTES) {
        setPhotoError("La imagen no puede superar 5 MB.");
        return;
      }

      setIsPhotoUploading(true);

      const updated = await uploadProfilePhoto(upload);

      setProfile(updated);
      setSavedMessage("Foto actualizada.");
    } catch (err) {
      setPhotoError(mapPhotoError(err));
    } finally {
      setIsPhotoUploading(false);
    }
  };

  const handleRemovePhoto = async () => {
    setPhotoError(null);
    setSavedMessage(null);
    setIsRemovingPhoto(true);

    try {
      const updated = await deleteProfilePhoto();
      setProfile(updated);
      setConfirmRemovePhoto(false);
      setSavedMessage("Foto eliminada.");
    } catch (err) {
      setPhotoError(mapPhotoError(err));
    } finally {
      setIsRemovingPhoto(false);
    }
  };

  const photoUrl = resolveMediaUrl(profile?.photoUrl);
  const isBusy = isSaving || isPhotoUploading || isRemovingPhoto;

  return (
    <ScreenContainer>
      <ScreenHeader showBack />

      <KeyboardAvoidingView
        style={styles.keyboardAvoiding}
        behavior={Platform.OS === "ios" ? "padding" : undefined}
      >
        <ScrollView
          style={styles.scrollView}
          contentContainerStyle={styles.scrollContent}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          {isLoading ? (
            <View style={styles.stateBox}>
              <ActivityIndicator size="large" color={colors.primary} />
              <Text style={styles.stateText}>Cargando tu perfil…</Text>
            </View>
          ) : loadError ? (
            <View style={styles.stateBox}>
              <Ionicons
                name="cloud-offline-outline"
                size={44}
                color={colors.danger}
              />
              <Text style={styles.errorText}>{loadError}</Text>
              <AppButton
                title="Reintentar"
                variant="secondary"
                onPress={load}
                style={styles.retryButton}
              />
            </View>
          ) : (
            <FadeInView style={styles.fade}>
              <View style={styles.formContainer}>
                <View style={styles.hero}>
                  <Text style={styles.title}>Edita tu perfil</Text>
                  <Text style={styles.subtitle}>
                    Así te verán los invitados de tus salas
                  </Text>
                </View>

                <View style={styles.photoSection}>
                  {photoUrl ? (
                    <Image
                      source={{ uri: photoUrl }}
                      style={styles.avatar}
                      accessibilityLabel="Foto actual de tu perfil"
                    />
                  ) : (
                    <View style={styles.avatarPlaceholder}>
                      <Ionicons
                        name="person"
                        size={48}
                        color={colors.textSecondary}
                      />
                    </View>
                  )}

                  <View style={styles.photoActions}>
                    <AppButton
                      title={photoUrl ? "Cambiar foto" : "Agregar foto"}
                      variant="secondary"
                      iconName="image-outline"
                      onPress={handlePickPhoto}
                      loading={isPhotoUploading}
                      disabled={isBusy}
                      style={styles.photoButton}
                    />
                    {photoUrl ? (
                      <AppButton
                        title="Quitar foto"
                        variant="danger"
                        iconName="trash-outline"
                        onPress={() => setConfirmRemovePhoto(true)}
                        disabled={isBusy}
                        style={styles.photoButton}
                      />
                    ) : null}
                  </View>

                  <Text style={styles.photoHelp}>
                    JPEG o PNG, máximo 5 MB.
                  </Text>

                  {confirmRemovePhoto ? (
                    <View style={styles.confirmBox}>
                      <Text style={styles.confirmText}>
                        ¿Quitar tu foto de perfil?
                      </Text>
                      <View style={styles.confirmActions}>
                        <AppButton
                          title="Cancelar"
                          variant="secondary"
                          onPress={() => setConfirmRemovePhoto(false)}
                          disabled={isRemovingPhoto}
                          style={styles.confirmButton}
                        />
                        <AppButton
                          title="Quitar"
                          variant="danger"
                          onPress={handleRemovePhoto}
                          loading={isRemovingPhoto}
                          disabled={isRemovingPhoto}
                          style={styles.confirmButton}
                        />
                      </View>
                    </View>
                  ) : null}

                  {photoError ? (
                    <Text style={styles.fieldError}>{photoError}</Text>
                  ) : null}
                </View>

                <AppTextField
                  label="Nombre artístico"
                  value={values.artistName}
                  onChangeText={(value) => updateField("artistName", value)}
                  onBlur={() => validateField("artistName")}
                  maxLength={MAX_ARTIST_NAME_LENGTH}
                  autoCapitalize="words"
                  hasError={Boolean(fieldErrors.artistName)}
                  errorText={
                    touched.artistName ? fieldErrors.artistName : null
                  }
                />

                <AppTextField
                  label="DJ desde (año)"
                  value={values.startYear}
                  onChangeText={(value) =>
                    updateField("startYear", value.replace(/[^0-9]/g, ""))
                  }
                  onBlur={() => validateField("startYear")}
                  keyboardType="number-pad"
                  maxLength={4}
                  placeholder="2018"
                  helpText="Déjalo vacío si no quieres mostrarlo."
                  hasError={Boolean(fieldErrors.startYear)}
                  errorText={touched.startYear ? fieldErrors.startYear : null}
                />

                <AppTextField
                  label="Descripción"
                  value={values.description}
                  onChangeText={(value) => updateField("description", value)}
                  onBlur={() => validateField("description")}
                  multiline
                  numberOfLines={5}
                  maxLength={MAX_DESCRIPTION_LENGTH}
                  placeholder="Cuéntale a tu público sobre tu estilo y experiencia."
                  helpText={`${values.description.length}/${MAX_DESCRIPTION_LENGTH}`}
                  hasError={Boolean(fieldErrors.description)}
                  errorText={
                    touched.description ? fieldErrors.description : null
                  }
                />

                <View style={styles.socialSection}>
                  <Text style={styles.sectionTitle}>Redes sociales</Text>
                  <Text style={styles.sectionHelp}>
                    Opcional. Usa enlaces HTTPS de cada plataforma.
                  </Text>

                  {socialPlatforms.map((platform) => (
                    <AppTextField
                      key={platform.key}
                      label={platform.label}
                      value={values.socialLinks[platform.key]}
                      onChangeText={(value) =>
                        updateSocial(platform.key, value)
                      }
                      onBlur={() => validateField(platform.key)}
                      placeholder={platform.placeholder}
                      autoCapitalize="none"
                      autoCorrect={false}
                      keyboardType="url"
                      hasError={Boolean(fieldErrors[platform.key])}
                      errorText={
                        touched[platform.key]
                          ? fieldErrors[platform.key]
                          : null
                      }
                    />
                  ))}
                </View>

                <AppButton
                  title="Guardar cambios"
                  onPress={handleSubmit}
                  loading={isSaving}
                  disabled={isBusy}
                  style={styles.saveButton}
                />

                {savedMessage ? (
                  <Text style={styles.successText}>{savedMessage}</Text>
                ) : null}

                {saveError ? (
                  <View style={styles.errorBox}>
                    <Text style={styles.errorBoxText}>{saveError}</Text>
                  </View>
                ) : null}

                <AppButton
                  title="Volver a mi perfil"
                  variant="outline"
                  onPress={() => router.back()}
                  disabled={isBusy}
                  style={styles.backButton}
                />
              </View>
            </FadeInView>
          )}
        </ScrollView>
      </KeyboardAvoidingView>
    </ScreenContainer>
  );
}

const styles = StyleSheet.create({
  keyboardAvoiding: {
    flex: 1,
  },
  scrollView: {
    flex: 1,
  },
  scrollContent: {
    flexGrow: 1,
    alignItems: "center",
    paddingTop: spacing.sm,
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
  fade: {
    width: "100%",
    alignItems: "center",
  },
  formContainer: {
    width: "100%",
    maxWidth: 440,
  },
  hero: {
    alignItems: "center",
    marginBottom: spacing.xl,
  },
  title: {
    ...typography.title,
    color: colors.textPrimary,
    textAlign: "center",
  },
  subtitle: {
    ...typography.body,
    color: colors.textSecondary,
    textAlign: "center",
    marginTop: spacing.sm,
  },
  photoSection: {
    alignItems: "center",
    marginBottom: spacing.xl,
  },
  avatar: {
    width: 112,
    height: 112,
    borderRadius: 56,
    borderWidth: 2,
    borderColor: colors.primary,
    backgroundColor: colors.surfaceRaised,
  },
  avatarPlaceholder: {
    width: 112,
    height: 112,
    borderRadius: 56,
    borderWidth: 2,
    borderColor: colors.border,
    backgroundColor: colors.surfaceRaised,
    alignItems: "center",
    justifyContent: "center",
  },
  photoActions: {
    width: "100%",
    gap: spacing.sm,
    marginTop: spacing.lg,
  },
  photoButton: {
    width: "100%",
  },
  photoHelp: {
    ...typography.small,
    color: colors.textSecondary,
    marginTop: spacing.sm,
    textAlign: "center",
  },
  confirmBox: {
    width: "100%",
    backgroundColor: colors.surface,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.control,
    padding: spacing.md,
    marginTop: spacing.md,
  },
  confirmText: {
    ...typography.body,
    color: colors.textPrimary,
    textAlign: "center",
    marginBottom: spacing.md,
  },
  confirmActions: {
    flexDirection: "row",
    gap: spacing.sm,
  },
  confirmButton: {
    flex: 1,
  },
  fieldError: {
    ...typography.small,
    color: colors.danger,
    marginTop: spacing.sm,
    textAlign: "center",
  },
  socialSection: {
    marginTop: spacing.sm,
  },
  sectionTitle: {
    ...typography.cardTitle,
    color: colors.textPrimary,
  },
  sectionHelp: {
    ...typography.small,
    color: colors.textSecondary,
    marginTop: spacing.xs,
    marginBottom: spacing.lg,
  },
  saveButton: {
    width: "100%",
    marginTop: spacing.sm,
  },
  successText: {
    ...typography.caption,
    color: colors.success,
    textAlign: "center",
    marginTop: spacing.md,
  },
  errorBox: {
    padding: spacing.md,
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.danger,
    borderWidth: 1,
    borderRadius: radius.control,
    marginTop: spacing.md,
  },
  errorBoxText: {
    color: colors.danger,
    fontSize: 13,
    textAlign: "center",
  },
  backButton: {
    width: "100%",
    marginTop: spacing.lg,
  },
});
