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
import { createRoomSchema } from "../../src/api/dto";
import { RoomResponse } from "@/api/dto";
import { request } from "@/api/client";

function parseLocalDateTime(input: string): { year: number; month: number; day: number; hour: number; minute: number } | null {
  const match = input.match(/^(\d{4})-(\d{2})-(\d{2}) (\d{2}):(\d{2})$/);
  if (!match) return null;

  const [, yearS, monthS, dayS, hourS, minuteS] = match;
  const year = parseInt(yearS, 10);
  const month = parseInt(monthS, 10);
  const day = parseInt(dayS, 10);
  const hour = parseInt(hourS, 10);
  const minute = parseInt(minuteS, 10);

  if (year < 1900 || year > 2100 || month < 1 || month > 12 || day < 1 || day > 31) return null;
  if (hour < 0 || hour > 23 || minute < 0 || minute > 59) return null;

  const daysInMonth = [31, 28 + ((year % 4 === 0 && year % 100 !== 0) || year % 400 === 0 ? 1 : 0), 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  if (day > daysInMonth[month - 1]) return null;

  return { year, month, day, hour, minute };
}

export default function CreateScreen() {
  const router = useRouter();
  const { user } = useAuth();

  const [name, setName] = React.useState("");
  const [startsAtInput, setStartsAtInput] = React.useState("");
  const [endsAtInput, setEndsAtInput] = React.useState("");
  const [timezone, setTimezone] = React.useState("America/Mexico_City");
  const [isSubmitting, setIsSubmitting] = React.useState(false);
  const [nameError, setNameError] = React.useState<string | null>(null);
  const [startsAtError, setStartsAtError] = React.useState<string | null>(null);
  const [endsAtError, setEndsAtError] = React.useState<string | null>(null);
  const [generalError, setGeneralError] = React.useState<string | null>(null);
  const [nameTouched, setNameTouched] = React.useState(false);
  const [startsAtTouched, setStartsAtTouched] = React.useState(false);
  const [endsAtTouched, setEndsAtTouched] = React.useState(false);

  const onNameBlur = () => {
    setNameTouched(true);
    if (!name.trim()) {
      setNameError("Nombre requerido");
    }
  };

  const onStartsAtBlur = () => {
    setStartsAtTouched(true);
    if (!startsAtInput.trim()) {
      setStartsAtError("Fecha requerida");
    } else {
      const parsed = parseLocalDateTime(startsAtInput);
      if (!parsed) {
        setStartsAtError("Formato de fecha inválido. Use AAAA-MM-DD HH:mm");
      } else {
        setStartsAtError(null);
      }
    }
  };

  const onEndsAtBlur = () => {
    setEndsAtTouched(true);
    if (!endsAtInput.trim()) {
      setEndsAtError("Fecha requerida");
    } else {
      const parsed = parseLocalDateTime(endsAtInput);
      if (!parsed) {
        setEndsAtError("Formato de fecha inválido. Use AAAA-MM-DD HH:mm");
      } else {
        setEndsAtError(null);
      }
    }
  };

const onSubmit = async () => {
    setGeneralError(null);
    let valid = true;
    let parsedStart: { year: number; month: number; day: number; hour: number; minute: number } | undefined;
    let parsedEnd: { year: number; month: number; day: number; hour: number; minute: number } | undefined;

    // 1. Validar nombre
    if (!name.trim()) {
      setNameError("Nombre requerido");
      setNameTouched(true);
      valid = false;
    } else {
      setNameError(null);
    }

    // 2. Validar y parsea startsAt
    if (!startsAtInput.trim()) {
      setStartsAtError("Fecha requerida");
      setStartsAtTouched(true);
      valid = false;
    } else {
      const pStart = parseLocalDateTime(startsAtInput);
      if (!pStart) {
        setStartsAtError("Formato de fecha inválido. Use AAAA-MM-DD HH:mm");
        setStartsAtTouched(true);
        valid = false;
      } else {
        parsedStart = pStart;
        setStartsAtError(null);
      }
    }

    // 3. Validar y parsea endsAt
    if (!endsAtInput.trim()) {
      setEndsAtError("Fecha requerida");
      setEndsAtTouched(true);
      valid = false;
    } else {
      const pEnd = parseLocalDateTime(endsAtInput);
      if (!pEnd) {
        setEndsAtError("Formato de fecha inválido. Use AAAA-MM-DD HH:mm");
        setEndsAtTouched(true);
        valid = false;
      } else {
        parsedEnd = pEnd;
        setEndsAtError(null);
      }
    }

    if (!valid) return;

    // 4. Validar que fin > inicio usando Date objects
    const dateStart = new Date(parsedStart!.year, parsedStart!.month - 1, parsedStart!.day, parsedStart!.hour, parsedStart!.minute, 0, 0);
    const dateEnd = new Date(parsedEnd!.year, parsedEnd!.month - 1, parsedEnd!.day, parsedEnd!.hour, parsedEnd!.minute, 0, 0);

    // Verificación anti-normalización: comparar componentes
    if (
      dateStart.getFullYear() !== parsedStart!.year ||
      dateStart.getMonth() !== parsedStart!.month - 1 ||
      dateStart.getDate() !== parsedStart!.day ||
      dateStart.getHours() !== parsedStart!.hour ||
      dateStart.getMinutes() !== parsedStart!.minute
    ) {
      setStartsAtError("Fecha/hora inválida (normalización)");
      setStartsAtTouched(true);
      return;
    }
    if (
      dateEnd.getFullYear() !== parsedEnd!.year ||
      dateEnd.getMonth() !== parsedEnd!.month - 1 ||
      dateEnd.getDate() !== parsedEnd!.day ||
      dateEnd.getHours() !== parsedEnd!.hour ||
      dateEnd.getMinutes() !== parsedEnd!.minute
    ) {
      setEndsAtError("Fecha/hora inválida (normalización)");
      setEndsAtTouched(true);
      return;
    }

    if (dateEnd <= dateStart) {
      setEndsAtError("La fecha de fin debe ser posterior a la de inicio");
      setEndsAtTouched(true);
      return;
    }

    // 5. Validar fin > ahora (fecha actual en local)
    const now = new Date();
    if (dateEnd <= now) {
      setEndsAtError("La fecha de fin debe ser posterior al momento actual");
      setEndsAtTouched(true);
      return;
    }

    // 6. Construir payload usando toISOString() (interpreta hora local del dispositivo)
    const startsAtISO = dateStart.toISOString();
    const endsAtISO = dateEnd.toISOString();

    // 7. Aplicar createRoomSchema.safeParse
    const validated = createRoomSchema.safeParse({
      name: name.trim(),
      startsAt: startsAtISO,
      endsAt: endsAtISO,
      timezone,
    });

    if (!validated.success) {
      // Mapear errores de Zod a campos
      for (const err of validated.error.errors) {
        const path = err.path[0] as keyof { name: string; startsAt: string; endsAt: string };
        if (path === "name") setNameError(err.message);
        else if (path === "startsAt") setStartsAtError(err.message);
        else if (path === "endsAt") setEndsAtError(err.message);
        else setGeneralError(err.message);
      }
      // Marcar touched para que aparezcan los mensajes
      setNameTouched(true);
      setStartsAtTouched(true);
      setEndsAtTouched(true);
      return;
    }

    // 8. Enviar con request() del cliente API
    setIsSubmitting(true);
    try {
      const room: RoomResponse = await request<RoomResponse>("POST", "/api/rooms", {
        body: {
          name: validated.data.name,
          startsAt: validated.data.startsAt,
          endsAt: validated.data.endsAt,
          timezone: validated.data.timezone || "America/Mexico_City",
        },
      });

      // request lanza ApiError en caso de error; si llegamos aquí, fue 2xx
      const roomId = room.id;
      if (roomId) {
        router.push(`/(app)/room/${roomId}`);
      } else {
        setGeneralError("La respuesta no contiene el ID de la sala");
      }
    } catch (err: any) {
      if (err instanceof Error && err.message.includes("Network error")) {
        setGeneralError("No se puede conectar con el servidor. Verifica tu conexión e intenta de nuevo.");
      } else if (err instanceof Error) {
        // ApiError del cliente: mensaje ya incluye status y detail
        setGeneralError(err.message || "Error inesperado al crear la sala");
      } else {
        setGeneralError("Error inesperado al crear la sala");
      }
    } finally {
      setIsSubmitting(false);
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
        Crear sala
      </Text>

      <View style={{ width: "100%", maxWidth: 360 }}>
        {/* Nombre */}
        <TextInput
          placeholder="Nombre de la sala"
          value={name}
          onChangeText={(text) => {
            setName(text);
            if (nameError) setNameError(null);
          }}
          onBlur={onNameBlur}
          style={{
            width: "100%",
            height: 50,
            borderColor: nameTouched && name === "" ? "#ff0000" : "#c0c0c0",
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

        {/* Fecha de inicio */}
        <TextInput
          placeholder="AAAA-MM-DD HH:mm"
          value={startsAtInput}
          onChangeText={(text) => {
            setStartsAtInput(text);
            if (startsAtError) setStartsAtError(null);
          }}
          onBlur={onStartsAtBlur}
          style={{
            width: "100%",
            height: 50,
            borderColor: startsAtTouched && startsAtInput === "" ? "#ff0000" : "#c0c0c0",
            borderWidth: 1,
            borderRadius: 8,
            paddingHorizontal: 12,
            marginBottom: 12,
            fontSize: 16,
            backgroundColor: "#fff",
          }}
        />
        {startsAtTouched && startsAtError && (
          <Text style={{ color: "red", fontSize: 12, marginBottom: 8 }}>{startsAtError}</Text>
        )}

        {/* Fecha de fin */}
        <TextInput
          placeholder="AAAA-MM-DD HH:mm"
          value={endsAtInput}
          onChangeText={(text) => {
            setEndsAtInput(text);
            if (endsAtError) setEndsAtError(null);
          }}
          onBlur={onEndsAtBlur}
          style={{
            width: "100%",
            height: 50,
            borderColor: endsAtTouched && endsAtInput === "" ? "#ff0000" : "#c0c0c0",
            borderWidth: 1,
            borderRadius: 8,
            paddingHorizontal: 12,
            marginBottom: 20,
            fontSize: 16,
            backgroundColor: "#fff",
          }}
        />
        {endsAtTouched && endsAtError && (
          <Text style={{ color: "red", fontSize: 12, marginBottom: 8 }}>{endsAtError}</Text>
        )}

        <Text style={{ marginTop: 20, color: "#64748b", fontSize: 14 }}>
          Las horas se interpretan en la zona de tu dispositivo
        </Text>

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
          onPress={onSubmit}
        >
          <Text style={{ color: "white", fontSize: 16 }}>{isSubmitting ? "Creando..." : "Crear sala"}</Text>
        </TouchableOpacity>

        {generalError && (
          <View style={{ padding: 12, backgroundColor: "#ffeef0", borderRadius: 8, marginTop: 12, alignItems: "center" }}>
            <Text style={{ color: "red", fontSize: 12 }}>{generalError}</Text>
          </View>
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({});