import { HttpErrorResponse } from '@angular/common/http';

export type ApiErrorKind =
  | 'network'
  | 'validation'
  | 'session'
  | 'notFound'
  | 'unavailable'
  | 'rateLimited'
  | 'quota'
  | 'server'
  | 'unknown';

export interface ApiError {
  kind: ApiErrorKind;
  message: string;
  retryAfterSeconds: number | null;
  fieldErrors: Readonly<Record<string, string>>;
}

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export function mapHttpError(error: HttpErrorResponse): ApiError {
  const body = isObject(error.error) ? (error.error as ProblemDetails) : null;
  const retryAfterSeconds = readRetryAfter(error);
  const fieldErrors = toFieldErrors(body?.errors);

  if (error.status === 0) {
    return {
      kind: 'network',
      message: 'No pudimos conectar con el servidor. Revisa tu conexión e inténtalo de nuevo.',
      retryAfterSeconds,
      fieldErrors,
    };
  }

  switch (error.status) {
    case 400:
      return {
        kind: 'validation',
        message: body?.detail ?? 'Revisa los datos enviados.',
        retryAfterSeconds,
        fieldErrors,
      };
    case 401:
      return {
        kind: 'session',
        message: 'Tu sesión expiró o no es válida. Vuelve a entrar con el código de la sala.',
        retryAfterSeconds,
        fieldErrors,
      };
    case 404:
      return {
        kind: 'notFound',
        message: body?.detail ?? 'No encontramos la sala.',
        retryAfterSeconds,
        fieldErrors,
      };
    case 409:
      return {
        kind: 'unavailable',
        message: body?.detail ?? 'La sala no está disponible en este momento.',
        retryAfterSeconds,
        fieldErrors,
      };
    case 429: {
      const text = `${body?.title ?? ''} ${body?.detail ?? ''}`;
      const isQuota = /l[íi]mite de solicitudes/i.test(text);
      return {
        kind: isQuota ? 'quota' : 'rateLimited',
        message: isQuota
          ? 'Has alcanzado el límite de solicitudes de esta sesión.'
          : (body?.detail ?? 'Estás enviando solicitudes demasiado rápido.'),
        retryAfterSeconds,
        fieldErrors,
      };
    }
    default:
      return error.status >= 500
        ? {
            kind: 'server',
            message: 'El servidor tuvo un problema. Inténtalo de nuevo.',
            retryAfterSeconds,
            fieldErrors,
          }
        : {
            kind: 'unknown',
            message: body?.detail ?? 'Ocurrió un error inesperado.',
            retryAfterSeconds,
            fieldErrors,
          };
  }
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function toFieldErrors(errors: Record<string, string[]> | undefined): Record<string, string> {
  if (!errors) return {};
  return Object.fromEntries(Object.entries(errors).map(([key, value]) => [key, value[0] ?? '']));
}

function readRetryAfter(error: HttpErrorResponse): number | null {
  const header = error.headers?.get('Retry-After');
  if (!header) return null;

  const seconds = Number(header);
  if (Number.isFinite(seconds) && seconds > 0) {
    return Math.ceil(seconds);
  }

  const date = Date.parse(header);
  if (!Number.isNaN(date)) {
    const difference = Math.ceil((date - Date.now()) / 1000);
    return difference > 0 ? difference : 0;
  }

  return null;
}
