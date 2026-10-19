import * as React from "react";
import {
  View,
  Text,
  TextInput,
  Button,
  ActivityIndicator,
  Alert,
  Keyboard,
  ScrollView,
  SafeAreaView,
  StyleSheet,
} from "react-native";
import { useNavigation, useRoute } from "@react-navigation/native";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { createRoomSchema } from "../../src/api/dto";

export default function CreateRoomScreen() {
  const navigation = useNavigation();
  const route = useRoute();
  const { user } = useAuth();

  const {
    register: formRegister,
    handleSubmit,
    reset,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<z.infer<typeof createRoomSchema>>({
    mode: "onBlur",
    defaultValues: {
      name: "",
      startsAt: "",
      endsAt: "",
      timezone: "America/Mexico_City",
    },
  });

  const [timezone, setTimezone] = React.useState<string>("America/Mexico_City");
  const [showTimezonePicker, setShowTimezonePicker] = React.useState(false);
  const [date, setDate] = React.useState<Date | null>(null);
  const [minDate, setMinDate] = React.useState<Date | null>(null);
  const [maxDate, setMaxDate] = React.useState<Date | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  // Calculate minDate = now, maxDate = now + 30 days for demo purposes
  React.useEffect(() => {
    const now = new Date();
    setMinDate(now);
    const max = new Date();
    max.setDate(now.getDate() + 30);
    setMaxDate(max);
  }, []);

  const onSubmit = async (data: z.infer<typeof createRoomSchema>) => {
    setError(null);
    try {
      const result = await request<{ id: string }>("POST", "/api/rooms", {
        body: {
          name: data.name,
          startsAt: data.startsAt,
          endsAt: data.endsAt,
          timezone: data.timezone || "America/Mexico_City",
        },
      });

      Alert.alert("Éxito", "Sala creada correctamente");
      navigation.navigate("(app)/room", { id: result.id });
    } catch (err: any) {
      if (err instanceof Error) {
        setError(err.message);
      } else {
        setError("Error desconocido");
      }
    }
  };

  // Helper: formatea Date a "AAAA-MM-DD HH:mm" para mostrar y enviar con offset
  const formatDateISO = (d: Date | null): string => {
    if (!d) return "";
    return d.toISOString();
  };

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: "#f0e6ff" }}>
      <ScrollView contentContainerStyle={{ padding: 20, maxWidth: 400 }}>
        <Text style={{ fontSize: 24, fontWeight: "bold", marginBottom: 20 }}>
          Crear sala
        </Text>

        {/* Name */}
        <TextInput
          placeholder="Nombre de la sala"
          name="name"
          {...formRegister}
          style={{
            width: "100%",
            height: 50,
            borderColor: errors.name?.message ? "#ef4444" : "#c0c0c0",
            borderWidth: 1,
            borderRadius: 8,
            paddingHorizontal: 12,
            marginBottom: 16,
            fontSize: 16,
            backgroundColor: "#fff",
          }}
          value={watch("name")}
        />
        {errors.name && <Text style={{ color: "red", fontSize: 12, marginBottom: 8 }}>{errors.name.message}</Text>}

        {/* Starts At */}
        <View style={{ marginBottom: 16 }}>
          <Text style={{ fontSize: 14, marginBottom: 4 }}>Fecha y hora de inicio</Text>
          <TextInput
            style={{
              width: "100%", height: 50,
              borderColor: "#c0c0c0", borderWidth: 1, borderRadius: 8,
              paddingHorizontal: 12, backgroundColor: "#fff", fontSize: 16,
            }}
            value={date ? formatDateISO(date) : "Seleccionar fecha"}
          />
          {errors.startsAt && <Text style={{ color: "red", fontSize: 12, marginTop: 4 }}>{errors.startsAt.message}</Text>}
        </View>

        {/* Ends At */}
        <View style={{ marginBottom: 16 }}>
          <Text style={{ fontSize: 14, marginBottom: 4 }}>Fecha y hora de fin</Text>
          <TextInput
            style={{
              width: "100%", height: 50,
              borderColor: "#c0c0c0", borderWidth: 1, borderRadius: 8,
              paddingHorizontal: 12, backgroundColor: "#fff", fontSize: 16,
            }}
            value={date ? formatDateISO(new Date(Date.now() + 24 * 60 * 60 * 1000)) : "Seleccionar fecha"}
          />
          {errors.endsAt && <Text style={{ color: "red", fontSize: 12, marginTop: 4 }}>{errors.endsAt.message}</Text>}
        </View>

        {/* Timezone selector */}
        <View style={{ marginBottom: 20, padding: 12, backgroundColor: "#f3f4f6", borderRadius: 8 }}>
          <Text style={{ fontSize: 14, marginBottom: 4 }}>Zona horaria</Text>
          <Text style={{ fontSize: 14 }}>{timezone || "America/Mexico_City"}>
            <Ionicons name="chevron-down" style={{ marginLeft: 8, fontSize: 12 }} />
          </Text>
          {showTimezonePicker && (
            <View style={{ marginTop: 8, maxHeight: 200, backgroundColor: "#fff", border: "1px solid #ccc", borderRadius: 8, padding: 8 }}>
              <Text style={{ fontSize: 12, marginBottom: 8, color: "#64748b" }}>Zonas comunes:</Text>
              <TouchableOpacity onPress={() => { setTimezone("America/Mexico_City"); setShowTimezonePicker(false); }}>
                <Text>America/Mexico_City (CDT)</Text>
              </TouchableOpacity>
              <TouchableOpacity onPress={() => { setTimezone("America/Chicago"); setShowTimezonePicker(false); }}>
                <Text>America/Chicago (CST)</Text>
              </TouchableOpacity>
              <TouchableOpacity onPress={() => { setTimezone("America/New_York"); setShowTimezonePicker(false); }}>
                <Text>America/New_York (EDT)</Text>
              </TouchableOpacity>
              <TouchableOpacity onPress={() => { setTimezone("Europe/Madrid"); setShowTimezonePicker(false); }}>
                <Text>Europe/Madrid (CEST)</Text>
              </TouchableOpacity>
              <TouchableOpacity onPress={() => { setTimezone("UTC"); setShowTimezonePicker(false); }}>
                <Text>UTC</Text>
              </TouchableOpacity>
              <TouchableOpacity
                onPress={() => setShowTimezonePicker(false)}
                style={{ marginTop: 8, padding: 4, borderWidth: 1, borderColor: "#8a2be2", borderRadius: 4 }}
              >
                <Text style={{ color: "#8a2be2" }}>Otra...</Text>
              </TouchableOpacity>
            </View>
          )}
          {error && <Text style={{ color: "red", fontSize: 12, marginTop: 4 }}>{error}</Text>}
        </View>

        <TouchableOpacity
          disabled={isSubmitting || isSubmitting}
          style={{
            width: "100%",
            height: 50,
            backgroundColor: isSubmitting ? "#6c757d" : "#8a2be2",
            borderRadius: 8,
            justifyContent: "center",
            alignItems: "center",
            marginTop: 20,
          }}
          onPress={handleSubmit(onSubmit)}
        >
          {isSubmitting ? <ActivityIndicator size="small" /> : <Text style={{ color: "white", fontSize: 16 }}>Crear sala</Text>}
        </TouchableOpacity>

        {error && <Text style={{ color: "red", fontSize: 12, marginTop: 8, textAlign: "center" }}>Error: {error}</Text>}
      </ScrollView>
    </SafeAreaView>
  );
}