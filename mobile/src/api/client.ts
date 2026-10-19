import type { z } from "zod";

export type Method = "GET" | "POST" | "PUT" | "PATCH" | "DELETE";

export interface RequestOptions {
  body?: unknown;
  headers?: Record<string, string>;
  retry?: boolean;
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

export async function request<T>(
  method: Method,
  path: string,
  options: RequestOptions = {}
): Promise<T> {
  const url = `${BASE_URL}${path}`;
  const headers = {
    "Content-Type": "application/json",
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
let retryQueue: Array<{
  method: Method;
  path: string;
  options: RequestOptions;
  resolve: (data: any) => void;
  reject: (err: Error) => void;
}> = [];

function enqueueRetry<T>(
  method: Method,
  path: string,
  options: RequestOptions,
  resolve: (data: T) => void,
  reject: (err: Error) => void
) {
  retryQueue.push({ method, path, options, resolve, reject });
}

function processRetryQueue(error: Error) {
  retryQueue.forEach(({ method, path, options, resolve, reject }) => {
    reject(error);
  });
  retryQueue = [];
}

async function refreshAuth(): Promise<void> {
  if (isRefreshing) {
    return refreshPromise;
  }

  isRefreshing = true;
  refreshPromise = (async () => {
    try {
      const { getRefreshToken, setTokens } = await import("../auth/storage");
      const refreshToken = await getRefreshToken();

      if (!refreshToken) {
        processRetryQueue(new NetworkError("No hay token de actualización"));
        return;
      }

      const result = await request<{ accessToken: string; refreshToken: string }>(
        "POST",
        "/api/auth/refresh",
        { body: { refreshToken }, retryCount: 0 }
      );

      await setTokens(result.accessToken, result.refreshToken);
      processRetryQueue(new Error("Refresh completed"));
    } catch (err) {
      processRetryQueue(
        err instanceof Error ? err : new Error("Refresh failed")
      );
    } finally {
      isRefreshing = false;
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

export { refreshAuth };

// --- login / register / me API calls ---

export async function loginApi(email: string, password: string): Promise<{ accessToken: string; refreshToken: string }> {
  const result = await request<{ accessToken: string; refreshToken: string }>(
    "POST",
    "/api/auth/login",
    { body: { email, password }, retryCount: 0 }
  );

  await import("../auth/storage").then((mod) => {
    const { setTokens } = mod;
    setTokens(result.accessToken, result.refreshToken);
  });

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
  const { getAccessToken } = await import("../auth/storage");
  const token = await getAccessToken();

  if (!token) {
    return {};
  }

  return { Authorization: `Bearer ${token}` };
}

export { BASE_URL };