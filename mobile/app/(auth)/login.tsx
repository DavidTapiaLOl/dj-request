import * as React from "react";
import { View, Text, StyleSheet, Pressable } from "react-native";
import { useRouter } from "expo-router";
import { useAuth } from "../../src/auth/provider";
import { AppButton } from "@/components/AppButton";
import { AppTextField } from "@/components/AppTextField";
import { AuthScreenContainer } from "@/components/AuthScreenContainer";
import { AuthBrand } from "@/components/BrandLogo";
import { FadeInView } from "@/components/FadeInView";
import { colors, spacing, typography } from "@/theme/theme";

export default function LoginScreen() {
  const router = useRouter();
  const { login, restoreError } = useAuth();

  const [email, setEmail] = React.useState("");
  const [password, setPassword] = React.useState("");
  const [emailError, setEmailError] = React.useState<string | null>(null);
  const [passwordError, setPasswordError] = React.useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [emailTouched, setEmailTouched] = React.useState(false);
  const [passwordTouched, setPasswordTouched] = React.useState(false);
  const [errorRed, setErrorRed] = React.useState<string | null>(null);

  const onSubmit = async () => {
    let valid = true;

    if (!email.trim()) {
      setEmailError("Correo obligatorio");
      setEmailTouched(true);
      valid = false;
    } else {
      setEmailError(null);
    }

    if (!password.trim()) {
      setPasswordError("Contraseña obligatoria");
      setPasswordTouched(true);
      valid = false;
    } else {
      setPasswordError(null);
    }

    if (!valid) return;

    try {
      setIsSubmitting(true);
      setErrorRed(null);
      await login(email, password);
      router.replace("/(app)");
    } catch (err: any) {
      if (err instanceof Error && err.message.includes("Network error")) {
        setErrorRed(
          "No se puede conectar con el servidor. Verifica tu conexión e intenta de nuevo."
        );
      } else if (err.status === 429) {
        setErrorRed(
          `Demasiados intentos. Inténtalo de nuevo en ${err.retryAfter || "un momento"}.`
        );
      } else if (err.status === 401) {
        setErrorRed("Correo o contraseña incorrectos");
      } else {
        setErrorRed(err.message || "Error al iniciar sesión");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const onEmailBlur = () => {
    setEmailTouched(true);
    if (!email.trim()) {
      setEmailError("Correo obligatorio");
    }
  };

  const onPasswordBlur = () => {
    setPasswordTouched(true);
    if (!password.trim()) {
      setPasswordError("Contraseña obligatoria");
    }
  };

  return (
    <AuthScreenContainer>
      <FadeInView>
        {restoreError === "session_expired" && (
          <View style={styles.expiredBanner}>
            <Text style={styles.expiredBannerText}>
              Tu sesión caducó. Inicia sesión de nuevo.
            </Text>
          </View>
        )}

        <View style={styles.hero}>
          <AuthBrand />
          <Text style={styles.title}>Bienvenido de nuevo</Text>
          <Text style={styles.subtitle}>Tu próxima noche empieza aquí</Text>
        </View>

        <AppTextField
          label="Correo electrónico"
          value={email}
          onChangeText={(text) => {
            setEmail(text);
            if (emailError) setEmailError(null);
          }}
          onBlur={onEmailBlur}
          autoComplete="email"
          autoCapitalize="none"
          keyboardType="email-address"
          placeholder="tu@correo.com"
          placeholderTextColor={colors.textSecondary}
          hasError={emailTouched && !!emailError}
          errorText={emailTouched ? emailError : null}
        />

        <AppTextField
          label="Contraseña"
          value={password}
          onChangeText={(text) => {
            setPassword(text);
            if (passwordError) setPasswordError(null);
          }}
          onBlur={onPasswordBlur}
          secureTextEntry
          showPasswordToggle
          autoComplete="current-password"
          autoCapitalize="none"
          placeholder="Tu contraseña"
          placeholderTextColor={colors.textSecondary}
          hasError={passwordTouched && !!passwordError}
          errorText={passwordTouched ? passwordError : null}
        />

        {errorRed && (
          <View style={styles.errorRedBanner}>
            <Text style={styles.errorRedText}>{errorRed}</Text>
          </View>
        )}

        <AppButton
          title="Iniciar sesión"
          onPress={onSubmit}
          variant="primary"
          loading={isSubmitting}
          disabled={isSubmitting}
          style={styles.enterButton}
        />

        <Pressable
          onPress={() => router.push("/(auth)/register")}
          accessibilityRole="link"
          accessibilityLabel="Crear cuenta"
          style={styles.linkContainer}
        >
          <Text style={styles.linkText}>
            ¿No tienes cuenta?{" "}
            <Text style={styles.link}>Crear cuenta</Text>
          </Text>
        </Pressable>
      </FadeInView>
    </AuthScreenContainer>
  );
}

const styles = StyleSheet.create({
  hero: {
    alignItems: "center",
    marginBottom: spacing.xxl,
  },
  title: {
    ...typography.title,
    color: colors.textPrimary,
    textAlign: "center",
    marginTop: spacing.xl,
  },
  subtitle: {
    ...typography.body,
    color: colors.textSecondary,
    textAlign: "center",
    marginTop: spacing.sm,
  },
  expiredBanner: {
    padding: spacing.md,
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.warning,
    borderWidth: 1,
    borderRadius: 12,
    marginBottom: spacing.lg,
  },
  expiredBannerText: {
    color: colors.textPrimary,
    fontSize: 14,
    textAlign: "center",
  },
  errorRedBanner: {
    padding: spacing.md,
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.danger,
    borderWidth: 1,
    borderRadius: 12,
    marginBottom: spacing.md,
  },
  errorRedText: {
    color: colors.danger,
    fontSize: 13,
    textAlign: "center",
  },
  enterButton: {
    width: "100%",
    marginTop: spacing.sm,
  },
  linkContainer: {
    marginTop: spacing.xl,
    minHeight: 48,
    justifyContent: "center",
    alignItems: "center",
  },
  linkText: {
    color: colors.textSecondary,
    fontSize: 14,
    textAlign: "center",
  },
  link: {
    color: colors.primary,
    fontSize: 14,
    fontWeight: "600",
  },
});
