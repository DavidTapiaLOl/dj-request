import * as React from "react";
import {
  View,
  Text,
  TextInput,
  Button,
  ActivityIndicator,
  Alert,
  Keyboard,
  StyleSheet,
} from "react-native";
import { useNavigation } from "@react-navigation/native";
import { useForm } from "react-hook-form";
import { useAuth } from "../../src/auth/provider";
import { loginSchema } from "../../src/api/dto";
import { z } from "zod";

export default function LoginScreen() {
  const navigation = useNavigation();

  const {
    register: formRegister,
    handleSubmit,
    reset,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<z.infer<typeof loginSchema>>({
    mode: "onBlur",
    defaultValues: {
      email: "",
      password: "",
    },
  });

  const { login } = useAuth();

  const onSubmit = async (data: z.infer<typeof loginSchema>) => {
    try {
      await login(data.email, data.password);
      navigation.navigate("(app)/rooms");
    } catch (err) {
      Alert.alert("Error", "Credenciales inválidas. Inténtalo de nuevo.");
      setTimeout(() => {}, 100);
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

      <form onSubmit={handleSubmit(onSubmit)} style={{ width: "100%", maxWidth: 350 }}>
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
            onPress={handleSubmit(onSubmit)}
          >
            <Text style={{ color: "white", fontSize: 16 }}>Entrar</Text>
          </TouchableOpacity>

          <TouchableOpacity
            disabled={isSubmitting}
            style={{
              width: "48%",
              height: 48,
              backgroundColor: "#6c757d",
              borderRadius: 8,
              justifyContent: "center",
              alignItems: "center",
            }}
            onPress={() => navigation.navigate("Register")}
          >
            <Text style={{ color: "white", fontSize: 16 }}>Registrarse</Text>
          </TouchableOpacity>
        </View>

        {/* Forgot password link */}
        <Text
          style={{
            marginTop: 20,
            color: "#64748b",
            fontSize: 14,
          }}
        >
          ¿Olvidaste tu contraseña?
        </Text>
      </form>
    </View>
  );
}