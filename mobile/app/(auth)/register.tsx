import * as React from "react";
import { View, Text, StyleSheet } from "react-native";
import { useRouter } from "expo-router";
import { useAuth } from "../../src/auth/provider";
import { AppButton } from "@/components/AppButton";
import { AppTextField } from "@/components/AppTextField";
import { AuthScreenContainer } from "@/components/AuthScreenContainer";
import { colors, spacing } from "@/theme/theme";

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
      await register(email, password, artistName);
      router.replace("/(app)");
    } catch (err: any) {
      if (err instanceof Error && err.message.includes("Network error")) {
        setErrorRed("No se puede conectar con el servidor. Verifica tu conexión e intenta de nuevo.");
      } else if (err.status === 409) {
        setErrorRed("El correo ya está registrado");
      } else if (err.status === 400) {
        setErrorRed(err.message || "Datos inválidos. Revisa los campos.");
      } else {
        setErrorRed(err.message || "No se pudo crear la cuenta. Intenta de nuevo.");
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
    <AuthScreenContainer>
      <AppTextField
        label="Nombre artístico"
        value={artistName}
        onChangeText={(text) => {
          setArtistName(text);
          if (nameError) setNameError(null);
        }}
        onBlur={onNameBlur}
        placeholder="Tu nombre artístico"
        placeholderTextColor={colors.textSecondary}
        hasError={nameTouched && !!nameError}
      />
      {nameTouched && nameError && (
        <Text style={styles.errorText}>{nameError}</Text>
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
        autoComplete="new-password"
        placeholder="Mínimo 12 caracteres"
        placeholderTextColor={colors.textSecondary}
        helpText="Mínimo 12 caracteres"
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
        title="Crear cuenta"
        onPress={onSubmit}
        variant="primary"
        loading={isSubmitting}
        disabled={isSubmitting}
        style={styles.submitButton}
      />

      <View style={styles.linkContainer}>
        <Text style={styles.linkText}>
          ¿Ya tienes cuenta?{" "}
          <Text style={styles.link} onPress={() => router.push("/(auth)/login")}>
            Inicia sesión
          </Text>
        </Text>
      </View>
    </AuthScreenContainer>
  );
}

const styles = StyleSheet.create({
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
  submitButton: {
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
