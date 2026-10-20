import { format } from "date-fns";
import { es } from "date-fns/locale";
import { formatInTimeZone } from "date-fns-tz";

const DAY_FORMAT = "EEE d 'de' MMM";
const TIME_FORMAT = "HH:mm";

export interface RoomSchedule {
  /** Día y hora de inicio; incluye el rango completo si todo ocurre el mismo día. */
  primary: string;
  /** Fin, cuando cae en un día distinto del inicio. */
  secondary: string | null;
  /** Zona IANA del evento usada para el formato (vacío si no se informó). */
  timeZone: string;
}

/**
 * Formatea el horario de una sala en español usando la zona horaria del
 * evento (room.timezone). Si la zona es inválida o falta, recurre a la zona
 * del dispositivo; si las fechas no son válidas, devuelve null.
 */
export function formatRoomSchedule(
  startsAt: string,
  endsAt: string,
  timeZone: string
): RoomSchedule | null {
  try {
    const start = new Date(startsAt);
    const end = new Date(endsAt);
    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) {
      return null;
    }

    const eventZone = timeZone && timeZone.trim() !== "" ? timeZone.trim() : null;

    const formatDate = (date: Date, formatStr: string): string => {
      if (eventZone) {
        try {
          return formatInTimeZone(date, eventZone, formatStr, { locale: es });
        } catch {
          // Zona IANA no reconocida: se formatea en la zona del dispositivo.
        }
      }
      return format(date, formatStr, { locale: es });
    };

    const startDay = formatDate(start, DAY_FORMAT);
    const startTime = formatDate(start, TIME_FORMAT);
    const endDay = formatDate(end, DAY_FORMAT);
    const endTime = formatDate(end, TIME_FORMAT);

    if (startDay === endDay) {
      return {
        primary: `${startDay} · ${startTime} – ${endTime}`,
        secondary: null,
        timeZone: eventZone ?? "",
      };
    }

    return {
      primary: `${startDay} · ${startTime}`,
      secondary: `${endDay} · ${endTime}`,
      timeZone: eventZone ?? "",
    };
  } catch {
    return null;
  }
}
