import * as React from "react";
import { View, Text, StyleSheet, Pressable } from "react-native";
import { useRouter } from "expo-router";
import { useAuth } from "../../src/auth/provider";
import { AppButton } from "@/components/AppButton";
import { AppTextField } from "@/components/AppTextField";
import { AuthScreenContainer } from "@/components/AuthScreenContainer";
import { AuthBrand } from "@/components/BrandLogo";
import { FadeInView } from "@/components/FadeInView";
import { ScreenHeader } from "@/components/ScreenHeader";
import { colors, spacing, typography } from "@/theme/theme";

export default function RegisterScreen() {
  const router = useRouter();
  const { register } = useAuth();

  const [artistName, setArtistName] = React.useState("");
  const [email, setEmail] = React.useState("");
  const [password, setPassword] = React.useState("");
  const [nameError, setNameError] = React.useState<string | null>(null);
  const [emailError, setEmailError] = React.useState<string | null>(null);
  const [passwordError, setPasswordError] = React.useState<string | null>(null);
  const [nameTouched, setNameTouched] = React.useState(false);
  const [emailTouched, setEmailTouched] = React.useState(false);
  const [passwordTouched, setPasswordTouched] = React.useState(false);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [errorRed, setErrorRed] = React.useState<string | null>(null);

  const validatePassword = (value: string): string | null => {
    if (!value.trim()) {
      return "Contraseña obligatoria";
    }
    if (value.length < 12) {
      return "La contraseña debe tener al menos 12 caracteres";
    }
    return null;
  };

  const onSubmit = async () => {
    let valid = true;

    if (!artistName.trim()) {
      setNameError("Nombre obligatorio");
      setNameTouched(true);
      valid = false;
    } else {
      setNameError(null);
    }

    if (!email.trim()) {
      setEmailError("Correo obligatorio");
      setEmailTouched(true);
      valid = false;
    } else {
      setEmailError(null);
    }

    const pwdError = validatePassword(password);
    if (pwdError) {
      setPasswordError(pwdError);
      setPasswordTouched(true);
      valid = false;
    } else {
      setPasswordError(null);
    }

    if (!valid) return;

    try {
      setIsSubmitting(true);
      setErrorRed(null);
      await register(email, password, artistName);
      router.replace("/(app)");
    } catch (err: any) {
      if (err instanceof Error && err.message.includes("Network error")) {
        setErrorRed(
          "No se puede conectar con el servidor. Verifica tu conexión e intenta de nuevo."
        );
      } else if (err.status === 409) {
        setErrorRed("El correo ya está registrado");
      } else if (err.status === 400) {
        setErrorRed(err.message || "Datos inválidos. Revisa los campos.");
      } else {
        setErrorRed(
          err.message || "No se pudo crear la cuenta. Intenta de nuevo."
        );
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const onNameBlur = () => {
    setNameTouched(true);
    if (!artistName.trim()) {
      setNameError("Nombre obligatorio");
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
    setPasswordError(validatePassword(password));
  };

  return (
    <AuthScreenContainer contentContainerStyle={styles.screenContent}>
      <FadeInView>
        <ScreenHeader showBack />

        <View style={styles.hero}>
          <AuthBrand />
          <Text style={styles.title}>Crea tu cuenta</Text>
          <Text style={styles.subtitle}>
            Empieza a recibir peticiones en tus eventos
          </Text>
        </View>

        <AppTextField
          label="Nombre artístico"
          value={artistName}
          onChangeText={(text) => {
            setArtistName(text);
            if (nameError) setNameError(null);
          }}
          onBlur={onNameBlur}
          autoComplete="name"
          placeholder="Tu nombre artístico"
          placeholderTextColor={colors.textSecondary}
          hasError={nameTouched && !!nameError}
          errorText={nameTouched ? nameError : null}
        />

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
          autoComplete="new-password"
          autoCapitalize="none"
          placeholder="Mínimo 12 caracteres"
          placeholderTextColor={colors.textSecondary}
          helpText="Mínimo 12 caracteres"
          hasError={passwordTouched && !!passwordError}
          errorText={passwordTouched ? passwordError : null}
        />

        {errorRed && (
          <View style={styles.errorRedBanner}>
            <Text style={styles.errorRedText}>{errorRed}</Text>
          </View>
        )}

        <AppButton
          title="Crear cuenta"
          onPress={onSubmit}
          variant="primary"
          loading={isSubmitting}
          disabled={isSubmitting}
          style={styles.submitButton}
        />

        <Pressable
          onPress={() => router.push("/(auth)/login")}
          accessibilityRole="link"
          accessibilityLabel="Iniciar sesión"
          style={styles.linkContainer}
        >
          <Text style={styles.linkText}>
            ¿Ya tienes cuenta?{" "}
            <Text style={styles.link}>Iniciar sesión</Text>
          </Text>
        </Pressable>
      </FadeInView>
    </AuthScreenContainer>
  );
}

const styles = StyleSheet.create({
  screenContent: {
    justifyContent: "flex-start",
  },
  hero: {
    alignItems: "center",
    marginTop: spacing.lg,
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
  submitButton: {
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
