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
      await register(email, password, artistName);
      Alert.alert("Éxito", "Cuenta creada. Ahora puedes iniciar sesión.");
      router.replace("/(app)");
    } catch (err: any) {
      if (err instanceof Error && err.message.includes("Network error")) {
        Alert.alert("Error de red", "No se puede conectar con el servidor. Verifica tu conexión e intenta de nuevo.");
      } else {
        Alert.alert("Error", "No se pudo crear la cuenta. Verifica los datos.");
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
        Registro
      </Text>

      <View style={{ width: "100%", maxWidth: 350 }}>
        {/* Artist Name */}
        <TextInput
          placeholder="Nombre artístico"
          value={artistName}
          onChangeText={(text) => {
            setArtistName(text);
            if (nameError) setNameError(null);
          }}
          onBlur={onNameBlur}
          style={{
            width: "100%",
            height: 50,
            borderColor: nameTouched && artistName === "" ? "#ff0000" : "#c0c0c0",
            borderWidth: 1,
            borderRadius: 8,
            paddingHorizontal: 12,
            marginBottom: 12,
            fontSize: 16,
            backgroundColor: "#fff",
          }}
        />
        {nameTouched && nameError && (
          <Text style={{ color: "red", fontSize: 12, marginBottom: 8 }}>{nameError}</Text>
        )}

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
          <Text style={{ color: "red", fontSize: 12, marginBottom: 8 }}>Correo obligatorio</Text>
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
              width: "100%",
              height: 48,
              backgroundColor: "#8a2be2",
              borderRadius: 8,
              justifyContent: "center",
              alignItems: "center",
            }}
            onPress={onSubmit}
          >
            <Text style={{ color: "white", fontSize: 16 }}>Crear cuenta</Text>
          </TouchableOpacity>
        </View>

        <Text
          style={{
            marginTop: 20,
            color: "#64748b",
            fontSize: 14,
          }}>
            ¿Ya tienes cuenta? <Text onPress={() => router.push("/(auth)/login")}>Inicia sesión</Text>
          </Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({});