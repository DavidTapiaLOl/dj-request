import * as React from "react";
import { useEffect } from "react";
import * as storage from "./storage";
import {
  loginApi,
  registerApi,
  getMe,
  getAuthHeaders,
  refreshAuth,
  setOnSessionRejected,
  ApiError,
  NetworkError,
} from "../api/client";
import { MeResponse } from "../api/dto";

export type RestoreErrorType = "session_expired" | "network" | "generic";

export interface AuthContextType {
  user: MeResponse | null;
  isLoading: boolean;
  loginStatus: "checking" | "authenticated" | "unauthenticated";
  restoreError?: RestoreErrorType;
  setRestoreError: (value: RestoreErrorType | undefined) => void;
  restoreSession: () => Promise<void>;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, artistName?: string) => Promise<void>;
  logout: () => Promise<void>;
  getAuthHeaders: () => Promise<Record<string, string>>;
}

const AuthContext = React.createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const [user, setUser] = React.useState<MeResponse | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loginStatus, setLoginStatus] = React.useState<
    "checking" | "authenticated" | "unauthenticated"
  >("checking");
  const [restoreError, setRestoreError] = React.useState<RestoreErrorType | undefined>(undefined);

  const applyAuthenticatedUser = React.useCallback((me: MeResponse) => {
    setUser({
      id: me.id,
      email: me.email,
      artistName: me.artistName,
    });
    setLoginStatus("authenticated");
    setRestoreError(undefined);
  }, []);

  const restoreSession = React.useCallback(async () => {
    try {
      const accessToken = await storage.getAccessToken();
      const refreshToken = await storage.getRefreshToken();

      if (!accessToken && !refreshToken) {
        setLoginStatus("unauthenticated");
        setUser(null);
        setRestoreError(undefined);
        return;
      }

      if (!accessToken && refreshToken) {
        await refreshAuth();
      }

      const me = await getMe();
      applyAuthenticatedUser(me);
    } catch (err) {
      if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
        await storage.removeTokens();
        setLoginStatus("unauthenticated");
        setUser(null);
        setRestoreError("session_expired");
      } else if (err instanceof NetworkError) {
        setRestoreError("network");
        setLoginStatus("checking");
        setUser(null);
      } else {
        setRestoreError("generic");
        setLoginStatus("unauthenticated");
        setUser(null);
      }
    }
  }, [applyAuthenticatedUser]);

  useEffect(() => {
    let cancelled = false;

    async function initialRestore() {
      try {
        const accessToken = await storage.getAccessToken();
        const refreshToken = await storage.getRefreshToken();

        if (!accessToken && !refreshToken) {
          if (!cancelled) {
            setLoginStatus("unauthenticated");
            setUser(null);
          }
          return;
        }

        if (!accessToken && refreshToken) {
          await refreshAuth();
        }

        const me = await getMe();
        if (!cancelled) {
          applyAuthenticatedUser(me);
        }
      } catch (err) {
        if (!cancelled) {
          if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
            await storage.removeTokens();
            setLoginStatus("unauthenticated");
            setUser(null);
            setRestoreError("session_expired");
          } else if (err instanceof NetworkError) {
            setRestoreError("network");
            setLoginStatus("checking");
            setUser(null);
          } else {
            setRestoreError("generic");
            setLoginStatus("unauthenticated");
            setUser(null);
          }
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    initialRestore();

    return () => {
      cancelled = true;
    };
  }, [applyAuthenticatedUser]);

  useEffect(() => {
    setOnSessionRejected(() => {
      storage.removeTokens().catch(() => {});
      setUser(null);
      setLoginStatus("unauthenticated");
      setRestoreError(undefined);
    });

    return () => {
      setOnSessionRejected(null);
    };
  }, []);

  const localGetAuthHeaders = async () => {
    return await getAuthHeaders();
  };

  const login = async (email: string, password: string) => {
    try {
      await loginApi(email, password);
      const me = await getMe();
      applyAuthenticatedUser(me);
    } catch (err) {
      throw err;
    }
  };

  const register = async (email: string, password: string, artistName?: string) => {
    try {
      const result = await registerApi(email, password, artistName ?? "");
      if (result.accessToken) {
        const me = await getMe();
        applyAuthenticatedUser(me);
      } else {
        throw new Error("Tu cuenta fue creada, pero no pudimos iniciar sesión. Intenta ingresar");
      }
    } catch (err) {
      throw err;
    }
  };

  const logout = async () => {
    try {
      await storage.removeTokens();
      setUser(null);
      setLoginStatus("unauthenticated");
      setRestoreError(undefined);
    } catch (error) {
      // Conservar el estado de sesión local aunque falle el borrado.
    }
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        isLoading,
        loginStatus,
        restoreError,
        setRestoreError,
        restoreSession,
        login,
        register,
        logout,
        getAuthHeaders: localGetAuthHeaders,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export function useAuth(): AuthContextType {
  const context = React.useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
