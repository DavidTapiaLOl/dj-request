import * as React from "react";
import {
  View,
  Text,
  TextInput,
  Button,
  Alert,
  ActivityIndicator,
  StyleSheet,
  TouchableOpacity,
} from "react-native";
import { useRouter } from "expo-router";
import { useAuth } from "../../src/auth/provider";

export default function LoginScreen() {
  const router = useRouter();
  const { login } = useAuth();

  const [email, setEmail] = React.useState("");
  const [password, setPassword] = React.useState("");
  const [emailError, setEmailError] = React.useState<string | null>(null);
  const [passwordError, setPasswordError] = React.useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [emailTouched, setEmailTouched] = React.useState(false);
  const [passwordTouched, setPasswordTouched] = React.useState(false);

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
        Alert.alert("Error de red", "No se puede conectar con el servidor. Verifica tu conexión e intenta de nuevo.");
      } else {
        Alert.alert("Error", "Credenciales inválidas. Inténtalo de nuevo.");
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
    <View
      style={{
        flex: 1,
        justifyContent: "center",
        alignItems: "center",
        padding: 20,
        backgroundColor: "#f0e6ff",
      }}
    >
      <Text style={{ fontSize: 24, fontWeight: "bold", marginBottom: 20 }}>
        Inicio de sesión
      </Text>

      <View style={{ width: "100%", maxWidth: 350 }}>
        {/* Email */}
        <TextInput
          placeholder="Correo electrónico"
          value={email}
          onChangeText={(text) => {
            setEmail(text);
            if (emailError) setEmailError(null);
          }}
          onBlur={onEmailBlur}
          style={{
            width: "100%",
            height: 50,
            borderColor: emailTouched && email === "" ? "#ff0000" : "#c0c0c0",
            borderWidth: 1,
            borderRadius: 8,
            paddingHorizontal: 12,
            marginBottom: 12,
            fontSize: 16,
            backgroundColor: "#fff",
          }}
        />
        {emailTouched && emailError && (
          <Text style={{ color: "red", fontSize: 12, marginBottom: 8 }}>{emailError}</Text>
        )}

        {/* Password */}
        <TextInput
          placeholder="Contraseña"
          secureTextEntry
          value={password}
          onChangeText={(text) => {
            setPassword(text);
            if (passwordError) setPasswordError(null);
          }}
          onBlur={onPasswordBlur}
          style={{
            width: "100%",
            height: 50,
            borderColor: passwordTouched && password === "" ? "#ff0000" : "#c0c0c0",
            borderWidth: 1,
            borderRadius: 8,
            paddingHorizontal: 12,
            marginBottom: 20,
            fontSize: 16,
            backgroundColor: "#fff",
          }}
        />
        {passwordTouched && passwordError && (
          <Text style={{ color: "red", fontSize: 12, marginBottom: 8 }}>Contraseña obligatoria</Text>
        )}

        {/* Buttons */}
        <View style={{ width: "100%", flexDirection: "row", justifyContent: "space-between" }}>
          <TouchableOpacity
            disabled={isSubmitting}
            style={{
              width: "48%",
              height: 48,
              backgroundColor: "#8a2be2",
              borderRadius: 8,
              justifyContent: "center",
              alignItems: "center",
            }}
            onPress={onSubmit}
          >
            <Text style={{ color: "white", fontSize: 16 }}>Entrar</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={{
              width: "48%",
              height: 48,
              backgroundColor: "#6c757d",
              borderRadius: 8,
              justifyContent: "center",
              alignItems: "center",
            }}
            onPress={() => router.push("/(auth)/register")}
          >
            <Text style={{ color: "white", fontSize: 16 }}>Registrarse</Text>
          </TouchableOpacity>
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({});