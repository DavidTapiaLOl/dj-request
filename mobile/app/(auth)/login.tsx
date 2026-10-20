import * as React from "react";
import { View, Text, StyleSheet } from "react-native";
import { useRouter } from "expo-router";
import { useAuth } from "../../src/auth/provider";
import { AppButton } from "@/components/AppButton";
import { AppTextField } from "@/components/AppTextField";
import { AuthScreenContainer } from "@/components/AuthScreenContainer";
import { colors, spacing } from "@/theme/theme";

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
      await login(email, password);
      router.replace("/(app)");
    } catch (err: any) {
      if (err instanceof Error && err.message.includes("Network error")) {
        setErrorRed("No se puede conectar con el servidor. Verifica tu conexión e intenta de nuevo.");
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
      {restoreError === "session_expired" && (
        <View style={styles.expiredBanner}>
          <Text style={styles.expiredBannerText}>Tu sesión caducó. Inicia sesión de nuevo.</Text>
        </View>
      )}

      <AppTextField
        label="Correo electrónico"
        value={email}
        onChangeText={(text) => {
          setEmail(text);
          if (emailError) setEmailError(null);
        }}
        onBlur={onEmailBlur}
        autoComplete="email"
        keyboardType="email-address"
        placeholder="tu@correo.com"
        placeholderTextColor={colors.textSecondary}
        hasError={emailTouched && !!emailError}
      />
      {emailTouched && emailError && (
        <Text style={styles.errorText}>{emailError}</Text>
      )}

      <AppTextField
        label="Contraseña"
        value={password}
        onChangeText={(text) => {
          setPassword(text);
          if (passwordError) setPasswordError(null);
        }}
        onBlur={onPasswordBlur}
        secureTextEntry={true}
        autoComplete="current-password"
        placeholder="Tu contraseña"
        placeholderTextColor={colors.textSecondary}
        hasError={passwordTouched && !!passwordError}
      />
      {passwordTouched && passwordError && (
        <Text style={styles.errorText}>{passwordError}</Text>
      )}

      {errorRed && (
        <View style={styles.errorRedBanner}>
          <Text style={styles.errorRedText}>{errorRed}</Text>
        </View>
      )}

      <AppButton
        title="Entrar"
        onPress={onSubmit}
        variant="primary"
        loading={isSubmitting}
        disabled={isSubmitting}
        style={styles.enterButton}
      />

      <View style={styles.linkContainer}>
        <Text style={styles.linkText}>
          ¿No tienes cuenta?{" "}
          <Text style={styles.link} onPress={() => router.push("/(auth)/register")}>
            Crear cuenta
          </Text>
        </Text>
      </View>
    </AuthScreenContainer>
  );
}

const styles = StyleSheet.create({
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
  errorText: {
    color: colors.danger,
    fontSize: 12,
    marginBottom: spacing.sm,
  },
  errorRedBanner: {
    padding: spacing.md,
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.danger,
    borderWidth: 1,
    borderRadius: 12,
    marginTop: spacing.sm,
    marginBottom: spacing.sm,
  },
  errorRedText: {
    color: colors.danger,
    fontSize: 13,
    textAlign: "center",
  },
  enterButton: {
    width: "100%",
    marginTop: spacing.md,
  },
  linkContainer: {
    marginTop: spacing.xl,
    alignItems: "center",
  },
  linkText: {
    color: colors.textSecondary,
    fontSize: 14,
  },
  link: {
    color: colors.primary,
    fontSize: 14,
    fontWeight: "600",
  },
});
