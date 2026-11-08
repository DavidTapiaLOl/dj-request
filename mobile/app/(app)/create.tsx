import * as React from "react";
import {
  View,
  Text,
  ScrollView,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
} from "react-native";
import { useRouter } from "expo-router";
import { createRoomSchema } from "../../src/api/dto";
import { RoomResponse } from "@/api/dto";
import { request, ApiError, NetworkError } from "@/api/client";
import DateTimeField from "../../src/components/DateTimeField";
import { ScreenContainer } from "@/components/ScreenContainer";
import { ScreenHeader } from "@/components/ScreenHeader";
import { AppButton } from "@/components/AppButton";
import { AppTextField } from "@/components/AppTextField";
import { BrandLogo } from "@/components/BrandLogo";
import { FadeInView } from "@/components/FadeInView";
import { colors, spacing, typography } from "@/theme/theme";

function parseLocalDateTime(input: string): {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
} | null {
  const match = input.match(/^(\d{4})-(\d{2})-(\d{2}) (\d{2}):(\d{2})$/);
  if (!match) return null;

  const [, yearS, monthS, dayS, hourS, minuteS] = match;
  const year = parseInt(yearS, 10);
  const month = parseInt(monthS, 10);
  const day = parseInt(dayS, 10);
  const hour = parseInt(hourS, 10);
  const minute = parseInt(minuteS, 10);

  if (year < 1900 || year > 2100 || month < 1 || month > 12 || day < 1 || day > 31)
    return null;
  if (hour < 0 || hour > 23 || minute < 0 || minute > 59) return null;

  const daysInMonth = [
    31,
    28 +
      ((year % 4 === 0 && year % 100 !== 0) || year % 400 === 0 ? 1 : 0),
    31,
    30,
    31,
    30,
    31,
    31,
    30,
    31,
    30,
    31,
  ];
  if (day > daysInMonth[month - 1]) return null;

  return { year, month, day, hour, minute };
}

export default function CreateScreen() {
  const router = useRouter();

  const [name, setName] = React.useState("");
  const [startsAtInput, setStartsAtInput] = React.useState("");
  const [endsAtInput, setEndsAtInput] = React.useState("");
  const [timezone] = React.useState("America/Mexico_City");
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
    let parsedStart:
      | { year: number; month: number; day: number; hour: number; minute: number }
      | undefined;
    let parsedEnd:
      | { year: number; month: number; day: number; hour: number; minute: number }
      | undefined;

    if (!name.trim()) {
      setNameError("Nombre requerido");
      setNameTouched(true);
      valid = false;
    } else {
      setNameError(null);
    }

    if (!startsAtInput.trim()) {
      setStartsAtError("Fecha requerida");
      setStartsAtTouched(true);
      valid = false;
    } else {
      const pStart = parseLocalDateTime(startsAtInput);
      if (!pStart) {
        setStartsAtError("Formato de fecha inválido");
        setStartsAtTouched(true);
        valid = false;
      } else {
        parsedStart = pStart;
        setStartsAtError(null);
      }
    }

    if (!endsAtInput.trim()) {
      setEndsAtError("Fecha requerida");
      setEndsAtTouched(true);
      valid = false;
    } else {
      const pEnd = parseLocalDateTime(endsAtInput);
      if (!pEnd) {
        setEndsAtError("Formato de fecha inválido");
        setEndsAtTouched(true);
        valid = false;
      } else {
        parsedEnd = pEnd;
        setEndsAtError(null);
      }
    }

    if (!valid) return;

    const dateStart = new Date(
      parsedStart!.year,
      parsedStart!.month - 1,
      parsedStart!.day,
      parsedStart!.hour,
      parsedStart!.minute,
      0,
      0
    );
    const dateEnd = new Date(
      parsedEnd!.year,
      parsedEnd!.month - 1,
      parsedEnd!.day,
      parsedEnd!.hour,
      parsedEnd!.minute,
      0,
      0
    );

    if (
      dateStart.getFullYear() !== parsedStart!.year ||
      dateStart.getMonth() !== parsedStart!.month - 1 ||
      dateStart.getDate() !== parsedStart!.day ||
      dateStart.getHours() !== parsedStart!.hour ||
      dateStart.getMinutes() !== parsedStart!.minute
    ) {
      setStartsAtError("Fecha/hora inválida");
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
      setEndsAtError("Fecha/hora inválida");
      setEndsAtTouched(true);
      return;
    }

    if (dateEnd <= dateStart) {
      setEndsAtError("La fecha de fin debe ser posterior a la de inicio");
      setEndsAtTouched(true);
      return;
    }

    const now = new Date();
    if (dateEnd <= now) {
      setEndsAtError("La fecha de fin debe ser posterior al momento actual");
      setEndsAtTouched(true);
      return;
    }

    const startsAtISO = dateStart.toISOString();
    const endsAtISO = dateEnd.toISOString();

    const validated = createRoomSchema.safeParse({
      name: name.trim(),
      startsAt: startsAtISO,
      endsAt: endsAtISO,
      timezone,
    });

    if (!validated.success) {
      for (const err of validated.error.errors) {
        const path = err.path[0] as keyof {
          name: string;
          startsAt: string;
          endsAt: string;
        };
        if (path === "name") setNameError(err.message);
        else if (path === "startsAt") setStartsAtError(err.message);
        else if (path === "endsAt") setEndsAtError(err.message);
        else setGeneralError(err.message);
      }
      setNameTouched(true);
      setStartsAtTouched(true);
      setEndsAtTouched(true);
      return;
    }

    setIsSubmitting(true);
    try {
      let room: RoomResponse;
      if (createdRoomIdRef.current) {
        room = { id: createdRoomIdRef.current } as RoomResponse;
      } else {
        room = await request<RoomResponse>("POST", "/api/rooms", {
          body: {
            name: validated.data.name,
            startsAt: validated.data.startsAt,
            endsAt: validated.data.endsAt,
            timezone: validated.data.timezone || timezone,
          },
        });
        createdRoomIdRef.current = room.id;
      }

      if (room.id) {
        router.replace(`/(app)/room/${room.id}`);
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
      <ScreenHeader showBack />

      <KeyboardAvoidingView
        style={styles.keyboardAvoiding}
        behavior={Platform.OS === "ios" ? "padding" : undefined}
      >
        <ScrollView
          style={styles.scrollView}
          contentContainerStyle={styles.scrollContent}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          <FadeInView style={styles.fade}>
            <View style={styles.formContainer}>
              <View style={styles.hero}>
                <BrandLogo size={64} showWordmark wordmarkSize={22} />
                <Text style={styles.title}>Crea una sala</Text>
                <Text style={styles.subtitle}>
                  Prepara el espacio para tu próximo evento
                </Text>
              </View>

              <AppTextField
                label="Nombre de la sala"
                labelPosition="hidden"
                value={name}
                onChangeText={(text) => {
                  setName(text);
                  if (nameError) setNameError(null);
                }}
                onBlur={onNameBlur}
                placeholder="Nombre de la sala"
                placeholderTextColor={colors.textSecondary}
                hasError={nameTouched && !!nameError}
                errorText={nameTouched ? nameError : null}
              />

              <DateTimeField
                label="Inicio del evento"
                labelPosition="inside"
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
                labelPosition="inside"
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

              <AppButton
                title="Crear sala"
                onPress={onSubmit}
                style={styles.createButton}
                loading={isSubmitting}
                disabled={isSubmitting}
              />

              {generalError && (
                <View style={styles.errorBox}>
                  <Text style={styles.errorBoxText}>{generalError}</Text>
                </View>
              )}
            </View>
          </FadeInView>
        </ScrollView>
      </KeyboardAvoidingView>
    </ScreenContainer>
  );
}

const styles = StyleSheet.create({
  keyboardAvoiding: {
    flex: 1,
  },
  scrollView: {
    flex: 1,
  },
  scrollContent: {
    flexGrow: 1,
    justifyContent: "flex-start",
    alignItems: "center",
    paddingTop: spacing.sm,
    paddingBottom: spacing.xxl,
  },
  fade: {
    width: "100%",
    alignItems: "center",
  },
  formContainer: {
    width: "100%",
    maxWidth: 400,
  },
  hero: {
    alignItems: "center",
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
  errorText: {
    fontSize: 13,
    color: colors.danger,
    marginTop: -spacing.sm,
    marginBottom: spacing.md,
  },
  createButton: {
    width: "100%",
    marginTop: spacing.sm,
  },
  errorBox: {
    padding: spacing.md,
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.danger,
    borderWidth: 1,
    borderRadius: 12,
    marginTop: spacing.md,
  },
  errorBoxText: {
    color: colors.danger,
    fontSize: 13,
    textAlign: "center",
  },
});
