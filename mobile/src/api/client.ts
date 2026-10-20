import type { z } from "zod";
import { MeResponse, SolicitudPageResponse, SolicitudResponse } from "./dto";
import * as SecureStore from "expo-secure-store";
import * as Storage from "../auth/storage";

export type Method = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

export interface RequestOptions {
  body?: unknown;
  headers?: Record<string, string>;
  retryCount?: number;
}

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public type?: string,
    public retryAfter?: string
  ) {
    super(message);
    this.name = "ApiError";
  }
}

export class NetworkError extends Error {
  constructor(message = "Error de red") {
    super(message);
    this.name = "NetworkError";
  }
}

const rawBaseUrl =
  typeof process !== "undefined" ? process.env.EXPO_PUBLIC_API_URL : undefined;

if (!rawBaseUrl) {
  throw new Error(
    "EXPO_PUBLIC_API_URL no está definida. Configúrala en el entorno antes de usar el cliente API."
  );
}

const BASE_URL = rawBaseUrl.replace(/\/+$/, "");

const PUBLIC_PATHS = ["/api/auth/login", "/api/auth/register", "/api/auth/refresh"];

function isPublicPath(path: string): boolean {
  return PUBLIC_PATHS.some((p) => path.startsWith(p));
}

type SessionRejectedCallback = () => void;
let onSessionRejected: SessionRejectedCallback | null = null;

export function setOnSessionRejected(cb: SessionRejectedCallback | null): void {
  onSessionRejected = cb;
}

function notifySessionRejected(): void {
  if (onSessionRejected) {
    try {
      onSessionRejected();
    } catch {
      // Ignorar errores del callback para no romper el flujo de la petición
    }
  }
}

export async function request<T>(
  method: Method,
  path: string,
  options: RequestOptions = {}
): Promise<T> {
  const retryCount = options.retryCount ?? 0;
  const publicPath = isPublicPath(path);

  let authHeaders: Record<string, string> = {};
  if (!publicPath) {
    authHeaders = await getAuthHeaders();
  }

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...authHeaders,
    ...options.headers,
  };

  // Reconstruir Authorization con el token actual para evitar que un header
  // antiguo proporcionado por la pantalla sobrescriba el token renovado.
  if (!publicPath && authHeaders.Authorization) {
    headers.Authorization = authHeaders.Authorization;
  }

  let body: string | undefined;
  if (options.body !== undefined) {
    body = JSON.stringify(options.body);
  }

  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), 15000);

  try {
    const fetchOptions: RequestInit = {
      method,
      headers,
      signal: controller.signal,
    };

    if (body) {
      fetchOptions.body = body;
    }

    const response = await fetch(`${BASE_URL}${path}`, fetchOptions);
    clearTimeout(timeoutId);

    if (response.status === 401 && !publicPath && retryCount === 0) {
      // Intentar renovar sesión y reintentar UNA sola vez con el token nuevo
      await refreshTokens();
      return request<T>(method, path, {
        ...options,
        retryCount: 1,
        headers: undefined,
      });
    }

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      const retryAfter = response.headers.get("Retry-After") ?? undefined;

      throw new ApiError(
        response.status,
        errorData.detail || errorData.message || `Error ${response.status}`,
        errorData.type,
        retryAfter
      );
    }

    const data = (await response.json()) as T;
    return data;
  } catch (err) {
    clearTimeout(timeoutId);

    if (err instanceof ApiError || err instanceof NetworkError) {
      throw err;
    }

    if (err instanceof DOMException && err.name === "AbortError") {
      throw new NetworkError("Tiempo de espera agotado");
    }

    if (err instanceof TypeError && err.message.includes("fetch")) {
      throw new NetworkError("Error de conexión de red");
    }

    throw err instanceof Error ? err : new NetworkError("Error desconocido");
  }
}

let refreshPromise: Promise<string> | null = null;

async function refreshTokens(): Promise<string> {
  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = (async () => {
    const refreshToken = await Storage.getRefreshToken();

    if (!refreshToken) {
      const err = new ApiError(401, "No hay token de actualización disponible.");
      notifySessionRejected();
      throw err;
    }

    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 15000);

    try {
      const response = await fetch(`${BASE_URL}/api/auth/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken }),
        signal: controller.signal,
      });
      clearTimeout(timeoutId);

      if (response.status === 401 || response.status === 403) {
        // Rechazo definitivo: el refresh token ya no sirve.
        await Storage.removeTokens();
        notifySessionRejected();
        throw new ApiError(401, "La sesión ya no es válida. Inicia sesión de nuevo.");
      }

      if (!response.ok) {
        // Otro fallo del servidor (429, 5xx…): conservar tokens, no rechazar sesión.
        const errorData = await response.json().catch(() => ({}));
        throw new ApiError(
          response.status,
          errorData.detail || errorData.message || `Error ${response.status}`,
          errorData.type,
          response.headers.get("Retry-After") ?? undefined
        );
      }

      const tokens = (await response.json()) as {
        accessToken: string;
        refreshToken: string;
      };

      await Storage.setTokens(tokens.accessToken, tokens.refreshToken);
      return tokens.accessToken;
    } catch (err) {
      clearTimeout(timeoutId);
      if (err instanceof ApiError || err instanceof NetworkError) {
        throw err;
      }
      if (err instanceof DOMException && err.name === "AbortError") {
        throw new NetworkError("Tiempo de espera agotado al renovar la sesión");
      }
      if (err instanceof TypeError && err.message.includes("fetch")) {
        // Fallo de red: conservar tokens para poder reintentar más tarde.
        throw new NetworkError("Sin conexión al renovar la sesión");
      }
      throw err instanceof Error ? err : new NetworkError("Error al renovar la sesión");
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

export { refreshTokens as refreshAuth };

export async function loginApi(email: string, password: string): Promise<{ accessToken: string; refreshToken: string }> {
  const result = await request<{ accessToken: string; refreshToken: string }>(
    "POST",
    "/api/auth/login",
    { body: { email, password } }
  );

  await Storage.setTokens(result.accessToken, result.refreshToken);

  return result;
}

export async function registerApi(
  email: string,
  password: string,
  artistName: string
): Promise<{ didRegister: boolean; accessToken?: string; refreshToken?: string }> {
  await request<unknown>("POST", "/api/auth/register", {
    body: { email, password, artistName },
  });

  try {
    const loginResult = await loginApi(email, password);
    return { didRegister: true, accessToken: loginResult.accessToken, refreshToken: loginResult.refreshToken };
  } catch {
    return { didRegister: true, accessToken: undefined, refreshToken: undefined };
  }
}

export async function getMe(): Promise<MeResponse> {
  return await request<MeResponse>("GET", "/api/auth/me");
}

export async function getAuthHeaders(): Promise<Record<string, string>> {
  const token = await Storage.getAccessToken();

  if (!token) {
    return {};
  }

  return { Authorization: `Bearer ${token}` };
}

// --- Solicitudes (Song Requests) ---

export async function listSolicitudes(
  roomId: string,
  params?: { page?: number; pageSize?: number; estado?: string; generoId?: number }
): Promise<SolicitudPageResponse> {
  const queryParams = new URLSearchParams();
  if (params?.page) queryParams.set("page", String(params.page));
  if (params?.pageSize) queryParams.set("pageSize", String(params.pageSize));
  if (params?.estado) queryParams.set("estado", params.estado);
  if (params?.generoId) queryParams.set("generoId", String(params.generoId));

  const qs = queryParams.toString();
  const path = `/api/rooms/${roomId}/requests${qs ? `?${qs}` : ""}`;
  return await request<SolicitudPageResponse>("GET", path);
}

export async function updateSolicitudState(
  roomId: string,
  solicitudId: string,
  estado: string,
  version: number
): Promise<SolicitudResponse> {
  return await request<SolicitudResponse>(
    "PATCH",
    `/api/rooms/${roomId}/requests/${solicitudId}`,
    { body: { estado, version } }
  );
}

export { BASE_URL };
