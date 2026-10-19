import * as SecureStore from "expo-secure-store";

const STORAGE_KEYS = {
  accessToken: "dj_access_token",
  refreshToken: "dj_refresh_token",
};

// Helper: obtener clave de almacenamiento según el entorno
function getStorageKey(key: "accessToken" | "refreshToken"): string {
  return `${STORAGE_KEYS[key]}_${typeof window !== "undefined" ? "web" : "native"}`;
}

// Almacenamiento nativo (Android/iOS)
async function nativeGetItem(key: string): Promise<string | null> {
  try {
    return await SecureStore.getItemAsync(key);
  } catch (error) {
    console.error(`Error reading storage key ${key}:`, error);
    return null;
  }
}

async function nativeSetItem(key: string, value: string): Promise<void> {
  try {
    await SecureStore.setItemAsync(key, value);
  } catch (error) {
    console.error(`Error setting storage key ${key}:`, error);
    throw error;
  }
}

async function nativeRemoveItem(key: string): Promise<void> {
  try {
    await SecureStore.deleteItemAsync(key);
  } catch (error) {
    console.error(`Error removing storage key ${key}:`, error);
    throw error;
  }
}

// Almacenamiento web (sessionStorage)
function getWebStorage(): Storage {
  if (typeof window === "undefined") {
    throw new Error("Web storage accessed outside of browser environment");
  }
  return window.sessionStorage;
}

async function webGetItem(key: string): Promise<string | null> {
  try {
    const storage = getWebStorage();
    return storage.getItem(key) || null;
  } catch (error) {
    console.error(`Error reading web storage key ${key}:`, error);
    return null;
  }
}

async function webSetItem(key: string, value: string): Promise<void> {
  try {
    const storage = getWebStorage();
    storage.setItem(key, value);
  } catch (error) {
    console.error(`Error setting web storage key ${key}:`, error);
    throw error;
  }
}

async function webRemoveItem(key: string): Promise<void> {
  try {
    const storage = getWebStorage();
    storage.removeItem(key);
  } catch (error) {
    console.error(`Error removing web storage key ${key}:`, error);
    throw error;
  }
}

// --- getAccessToken / getRefreshToken ---

export async function getAccessToken(): Promise<string | null> {
  try {
    // Intentar primero SecureStore (nativo), si falla usar sessionStorage (web)
    if (typeof window === "undefined") {
      // Entorno nativo
      return nativeGetItem(STORAGE_KEYS.accessToken);
    } else {
      // Entorno web: intentar SecureStore primero (fallará), luego sessionStorage
      const nativeToken = await nativeGetItem(STORAGE_KEYS.accessToken).catch(() => null);
      if (nativeToken) return nativeToken;
      return webGetItem(getStorageKey("accessToken"));
    }
  } catch (error) {
    console.error("Error getting access token:", error);
    return null;
  }
}

export async function getRefreshToken(): Promise<string | null> {
  try {
    if (typeof window === "undefined") {
      return nativeGetItem(STORAGE_KEYS.refreshToken);
    } else {
      const nativeToken = await nativeGetItem(STORAGE_KEYS.refreshToken).catch(() => null);
      if (nativeToken) return nativeToken;
      return webGetItem(getStorageKey("refreshToken"));
    }
  } catch (error) {
    console.error("Error getting refresh token:", error);
    return null;
  }
}

export async function setTokens(accessToken: string, refreshToken: string): Promise<void> {
  try {
    if (typeof window === "undefined") {
      // Entorno nativo: usar SecureStore
      await nativeSetItem(STORAGE_KEYS.accessToken, accessToken);
      await nativeSetItem(STORAGE_KEYS.refreshToken, refreshToken);
    } else {
      // Entorno web: usar sessionStorage (temporal, por duración de pestaña)
      await webSetItem(getStorageKey("accessToken"), accessToken);
      await webSetItem(getStorageKey("refreshToken"), refreshToken);
    }
    // NO logar tokens en consola
  } catch (error) {
    console.error("Error setting tokens:", error);
    throw error;
  }
}

export async function removeTokens(): Promise<void> {
  try {
    if (typeof window === "undefined") {
      await nativeRemoveItem(STORAGE_KEYS.accessToken);
      await nativeRemoveItem(STORAGE_KEYS.refreshToken);
    } else {
      await webRemoveItem(getStorageKey("accessToken"));
      await webRemoveItem(getStorageKey("refreshToken"));
    }
  } catch (error) {
    console.error("Error removing tokens:", error);
    throw error;
  }
}