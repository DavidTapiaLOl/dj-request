import * as React from "react";
import {
  View,
  Text,
  StyleSheet,
} from "react-native";
import { useRouter } from "expo-router";
import { useAuth } from "../../src/auth/provider";
import { createRoomSchema } from "../../src/api/dto";
import { RoomResponse } from "@/api/dto";
import { request, ApiError, NetworkError } from "@/api/client";
import DateTimeField from "../../src/components/DateTimeField";
import { ScreenContainer } from "@/components/ScreenContainer";
import { ScreenHeader } from "@/components/ScreenHeader";
import { AppButton } from "@/components/AppButton";
import { AppTextField } from "@/components/AppTextField";
import { colors, spacing, typography } from "@/theme/theme";

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
  const createdRoomIdRef = React.useRef<string | null>(null);

  const onNameBlur = () => {
    setNameTouched(true);
    if (!name.trim()) {
      setNameError("Nombre requerido");
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
      // Si la sala ya fue creada, no repetir el POST; solo reintentar la navegación.
      let room: RoomResponse;
      if (createdRoomIdRef.current) {
        room = { id: createdRoomIdRef.current } as RoomResponse;
      } else {
        room = await request<RoomResponse>("POST", "/api/rooms", {
          body: {
            name: validated.data.name,
            startsAt: validated.data.startsAt,
            endsAt: validated.data.endsAt,
            timezone: validated.data.timezone || "America/Mexico_City",
          },
        });
        createdRoomIdRef.current = room.id;
      }

      if (room.id) {
        router.push(`/(app)/room/${room.id}`);
      } else {
        setGeneralError("La respuesta no contiene el ID de la sala");
      }
    } catch (err) {
      if (err instanceof NetworkError) {
        if (createdRoomIdRef.current) {
          setGeneralError(
            "La sala fue creada, pero no pudimos abrir el detalle. Revisa tu conexión y reintenta."
          );
        } else {
          setGeneralError(
            "No se puede conectar con el servidor. Verifica tu conexión e intenta de nuevo."
          );
        }
      } else if (err instanceof ApiError) {
        setGeneralError(err.message || "Error inesperado al crear la sala");
      } else {
        setGeneralError("Error inesperado al crear la sala");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <ScreenContainer>
      <ScreenHeader showBack={true} />

      <View style={styles.formContainer}>
        <AppTextField
          label="Nombre de la sala"
          value={name}
          onChange={(text) => {
            setName(text);
            if (nameError) setNameError(null);
          }}
          helpText="El nombre de la sala es obligatorio"
          hasError={nameTouched && !!nameError}
        />
        {nameTouched && nameError && (
          <Text style={styles.errorText}>{nameError}</Text>
        )}

        <DateTimeField
          label="Inicio del evento"
          value={startsAtInput}
          onChange={(v) => {
            setStartsAtInput(v);
            setStartsAtTouched(true);
            if (startsAtError) setStartsAtError(null);
          }}
          hasError={startsAtTouched && !!startsAtError}
        />
        {startsAtTouched && startsAtError && (
          <Text style={styles.errorText}>{startsAtError}</Text>
        )}

        <DateTimeField
          label="Fin del evento"
          value={endsAtInput}
          onChange={(v) => {
            setEndsAtInput(v);
            setEndsAtTouched(true);
            if (endsAtError) setEndsAtError(null);
          }}
          hasError={endsAtTouched && !!endsAtError}
        />
        {endsAtTouched && endsAtError && (
          <Text style={styles.errorText}>{endsAtError}</Text>
        )}

        <Text style={styles.zoneText}>
          Las horas se interpretan en la zona de tu dispositivo
        </Text>

        <AppButton
          title="Crear sala"
          iconName="add"
          onPress={onSubmit}
          style={styles.createButton}
          disabled={isSubmitting}
        />
      </View>

      {generalError && (
        <View style={styles.errorBox}>
          <Text style={styles.errorText}>{generalError}</Text>
        </View>
      )}
    </ScreenContainer>
  );
}

const styles = StyleSheet.create({
  formContainer: {
    width: "100%",
    maxWidth: 360,
    // backgroundColor transparente (hereda del ScreenContainer)
  },
  errorText: {
    fontSize: 12,
    color: colors.danger,
    marginBottom: spacing.xs,
  },
  zoneText: {
    ...typography.small,
    color: colors.textSecondary,
    marginTop: spacing.md,
  },
  createButton: {
    width: "100%",
    marginTop: spacing.md,
  },
  errorBox: {
    padding: 12,
    backgroundColor: "#ffeef0",
    borderRadius: 8,
    marginTop: 12,
    alignItems: "center",
  },
});