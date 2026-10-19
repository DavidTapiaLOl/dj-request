import * as React from "react";
import { useEffect } from "react";
import * as storage from "./storage";
import { loginApi, registerApi, getMe, getAuthHeaders, refreshAuth } from "../api/client";
import { MeResponse } from "../api/dto";

export interface AuthContextType {
  user: MeResponse | null;
  isLoading: boolean;
  loginStatus: "checking" | "authenticated" | "unauthenticated";
  restoreError?: boolean;
  setRestoreError: (value: boolean | undefined) => void;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, artistName?: string) => Promise<void>;
  logout: () => Promise<void>;
  getAuthHeaders: () => Promise<Record<string, string>>;
}

const AuthContext = React.createContext<AuthContextType & { restoreError?: boolean } | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const [user, setUser] = React.useState<MeResponse | null>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loginStatus, setLoginStatus] = React.useState<
    "checking" | "authenticated" | "unauthenticated"
  >("checking");
  const [restoreError, setRestoreError] = React.useState<boolean | undefined>(undefined);

  useEffect(() => {
    async function restoreSession() {
      try {
        const token = await storage.getAccessToken();
        if (!token) {
          setLoginStatus("unauthenticated");
          setUser(null);
          setIsLoading(false);
          return;
        }

        const me = await getMe();
        setUser({
          id: me.id,
          email: me.email,
          artistName: me.artistName,
        });
        setLoginStatus("authenticated");
      } catch (err: any) {
        if (err instanceof Error && (err.message.includes("401") || err.message.includes("403"))) {
          await storage.removeTokens();
          setLoginStatus("unauthenticated");
        } else if (err instanceof Error && err.message.includes("Network error")) {
          // Error de red: no borrar tokens ni marcar unauthenticated
          setRestoreError(true);
          setLoginStatus("checking");
        } else {
          setRestoreError(false);
          setLoginStatus("unauthenticated");
        }
        setUser(null);
      } finally {
        setIsLoading(false);
      }
    }

    restoreSession();
  }, []);

  const localGetAuthHeaders = async () => {
    return await getAuthHeaders();
  };

  const login = async (email: string, password: string) => {
    setIsLoading(true);
    setLoginStatus("checking");
    try {
      await loginApi(email, password);
      const me = await getMe();
setUser({
          id: me.id,
          email: me.email,
          artistName: me.artistName,
        });
      setLoginStatus("authenticated");
    } catch (err: any) {
      setLoginStatus("unauthenticated");
      console.error("Login error:", err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const register = async (email: string, password: string, artistName?: string) => {
    setIsLoading(true);
    setLoginStatus("checking");
    try {
      const result = await registerApi(email, password, artistName ?? "");
      if (result.accessToken) {
        const me = await getMe();
        setUser({
          id: me.id,
          email: me.email,
          artistName: me.artistName,
        });
        setLoginStatus("authenticated");
      } else {
        setLoginStatus("unauthenticated");
        throw new Error("Tu cuenta fue creada, pero no pudimos iniciar sesión. Intenta ingresar");
      }
    } catch (err: any) {
      setLoginStatus("unauthenticated");
      console.error("Register error:", err);
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const logout = async () => {
    try {
      await storage.removeTokens();
      setUser(null);
      setLoginStatus("unauthenticated");
    } catch (error) {
      console.error("Logout error:", error);
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