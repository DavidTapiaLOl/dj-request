import type { z } from "zod";
import { MeResponse } from "./dto";
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
    public type?: string
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

const BASE_URL = typeof process !== "undefined"
  ? (process as any).env.EXPO_PUBLIC_API_URL
  : "http://localhost:5000";

const PUBLIC_PATHS = ["/api/auth/login", "/api/auth/register", "/api/auth/refresh"];

export async function request<T>(
  method: Method,
  path: string,
  options: RequestOptions = {}
): Promise<T> {
  // Obtener headers de auth automáticamente (incluye Authorization: Bearer <token>)
  // Excluir endpoints públicos para evitar conflictos con registro/login
  const isPublicPath = PUBLIC_PATHS.some((p) => path.startsWith(p));
  const authHeaders = isPublicPath
    ? {}
    : await getAuthHeaders();

  const url = `${BASE_URL}${path}`;
  const headers = {
    "Content-Type": "application/json",
    ...authHeaders,
    ...options.headers,
  };

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

    const response = await fetch(url, fetchOptions);
    clearTimeout(timeoutId);

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));

      if (options.retryCount === 0 && response.status === 401) {
        await refreshAuth();
        return request<T>(method, path, { ...options, retryCount: 1 } as any);
      }

      throw new ApiError(
        response.status,
        errorData.detail || errorData.message || `Error ${response.status}`,
        errorData.type
      );
    }

    const data = (await response.json()) as T;
    return data;
  } catch (err) {
    clearTimeout(timeoutId);

    if (err instanceof ApiError) {
      throw err;
    }

    if (err instanceof DOMException && err.name === "AbortError") {
      throw new NetworkError("Tiempo de espera agotado");
    }

    if (err instanceof TypeError && err.message.includes("fetch")) {
      throw new NetworkError("Error de conexión de red");
    }

    throw new NetworkError(
      err instanceof Error ? err.message : "Error desconocido"
    );
  }
}

let isRefreshing = false;
let refreshPromise: Promise<void> | null = null;

async function refreshAuth(): Promise<void> {
  if (isRefreshing) {
    return refreshPromise!;
  }

  refreshPromise = (async () => {
    try {
      const refreshToken = await Storage.getRefreshToken();

      if (!refreshToken) {
        refreshPromise = null;
        return;
      }

      const result = await request<{ accessToken: string; refreshToken: string }>(
        "POST",
        "/api/auth/refresh",
        { body: { refreshToken }, retryCount: 0 }
      );

      await Storage.setTokens(result.accessToken, result.refreshToken);
      refreshPromise = null;
    } catch (err) {
      refreshPromise = null;
    } finally {
      isRefreshing = false;
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

export { refreshAuth };

export async function loginApi(email: string, password: string): Promise<{ accessToken: string; refreshToken: string }> {
  const result = await request<{ accessToken: string; refreshToken: string }>(
    "POST",
    "/api/auth/login",
    { body: { email, password }, retryCount: 0 }
  );

  await Storage.setTokens(result.accessToken, result.refreshToken);

  return result;
}

export async function registerApi(
  email: string,
  password: string,
  artistName: string
): Promise<{ didRegister: boolean; accessToken?: string; refreshToken?: string }> {
  const registerResult: any = await request<any>(
    "POST",
    "/api/auth/register",
    { body: { email, password, artistName }, retryCount: 0 }
  );

  try {
    const loginResult = await loginApi(email, password);
    return { didRegister: true, accessToken: loginResult.accessToken, refreshToken: loginResult.refreshToken };
  } catch (loginErr) {
    return { didRegister: true, accessToken: undefined, refreshToken: undefined };
  }
}

export async function getMe(): Promise<MeResponse> {
  const result = await request<MeResponse>("GET", "/api/auth/me", {
    retryCount: 0,
  });
  return result;
}

// Tipado corregido para admitir ausencia de token
export async function getAuthHeaders(): Promise<Record<string, string>> {
  const token = await Storage.getAccessToken();

  if (!token) {
    return {};
  }

  return { Authorization: `Bearer ${token}` };
}

export { BASE_URL };