import * as React from "react";
import {
  View,
  Text,
  TextInput,
  Button,
  ActivityIndicator,
  Alert,
  TouchableOpacity,
} from "react-native";
import { useNavigation } from "@react-navigation/native";
import { useForm } from "react-hook-form";
import { useAuth } from "../../src/auth/provider";
import { registerSchema } from "../../src/api/dto";
import { z } from "zod";

export default function RegisterScreen() {
  const navigation = useNavigation();

  const {
    register: formRegister,
    handleSubmit,
    reset,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<z.infer<typeof registerSchema>>({
    mode: "onBlur",
    defaultValues: {
      email: "",
      password: "",
      artistName: "",
    },
  });

  const { register } = useAuth();

  const onSubmit = async (data: z.infer<typeof registerSchema>) => {
    try {
      await register(data.email, data.password, data.artistName);
      Alert.alert("Éxito", "Cuenta creada. Ahora puedes iniciar sesión.");
      navigation.navigate("Login");
    } catch (err) {
      Alert.alert("Error", "No se pudo crear la cuenta. Verifica los datos.");
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

      <form onSubmit={handleSubmit(onSubmit)} style={{ width: "100%", maxWidth: 350 }}>
        {/* Artist Name */}
        <TextInput
          placeholder="Nombre artístico"
          autoCompleteName="artistName"
          style={{
            width: "100%",
            height: 50,
            borderColor: "#c0c0c0",
            borderWidth: 1,
            borderRadius: 8,
            paddingHorizontal: 12,
            marginBottom: 12,
            fontSize: 16,
            backgroundColor: "#fff",
          }}
          value={watch("artistName")}
          onChange={formRegister}
        />
        {errors.artistName && (
          <Text style={{ color: "red", fontSize: 12, marginBottom: 8 }}>{errors.artistName.message}</Text>
        )}

        {/* Email */}
        <TextInput
          placeholder="Correo electrónico"
          autoCompleteName="email"
          autoCapitalize="none"
          keyboardType="email-address"
          style={{
            width: "100%",
            height: 50,
            borderColor: "#c0c0c0",
            borderWidth: 1,
            borderRadius: 8,
            paddingHorizontal: 12,
            marginBottom: 12,
            fontSize: 16,
            backgroundColor: "#fff",
          }}
          value={watch("email")}
          onChange={formRegister}
        />
        {errors.email && (
          <Text style={{ color: "red", fontSize: 12, marginBottom: 8 }}>{errors.email.message}</Text>
        )}

        {/* Password */}
        <TextInput
          placeholder="Contraseña"
          secureTextEntry
          autoCompleteCurrentPassword
          style={{
            width: "100%",
            height: 50,
            borderColor: "#c0c0c0",
            borderWidth: 1,
            borderRadius: 8,
            paddingHorizontal: 12,
            marginBottom: 20,
            fontSize: 16,
            backgroundColor: "#fff",
          }}
          value={watch("password")}
          onChange={formRegister}
        />
        {errors.password && (
          <Text style={{ color: "red", fontSize: 12, marginBottom: 8 }}>{errors.password.message}</Text>
        )}

        <TouchableOpacity
          disabled={isSubmitting}
          style={{
            width: "100%",
            height: 48,
            backgroundColor: "#8a2be2",
            borderRadius: 8,
            justifyContent: "center",
            alignItems: "center",
            marginTop: 10,
          }}
          onPress={handleSubmit(onSubmit)}
        >
          <Text style={{ color: "white", fontSize: 16 }}>Crear cuenta</Text>
        </TouchableOpacity>

        <Text
          style={{
            marginTop: 20,
            color: "#64748b",
            fontSize: 14,
          }}>
          ¿Ya tienes cuenta? <Text onPress={() => navigation.navigate("Login")}>Inicia sesión</Text>
        </Text>
      </form>
    </View>
  );
}