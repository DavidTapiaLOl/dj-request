import * as React from "react";
import * as storage from "./storage";
import { loginApi, registerApi, getMe, getAuthHeaders, refreshAuth } from "../api/client";

// Export functions at module level for screens to import (no JWT decoding)
export async function login(email: string, password: string): Promise<void> {
  try {
    await loginApi(email, password);
  } catch (error) {
    console.error("Login error:", error);
    throw error;
  }
}

// Export register function at module level for screens to import
export async function register(email: string, password: string, artistName?: string): Promise<void> {
  try {
    await registerApi(email, password, artistName ?? "");
  } catch (error) {
    console.error("Register error:", error);
    throw error;
  }
}

export interface AuthContextType {
  user: any;
  isLoading: boolean;
  loginStatus: "checking" | "authenticated" | "unauthenticated";
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, artistName?: string) => Promise<void>;
  logout: () => Promise<void>;
  getAuthHeaders: () => Promise<Record<string, string>>;
}

const AuthContext = React.createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC = ({ children }) => {
  const [user, setUser] = React.useState<any>(null);
  const [isLoading, setIsLoading] = React.useState(true);
  const [loginStatus, setLoginStatus] = React.useState<
    "checking" | "authenticated" | "unauthenticated"
  >("checking");

  const getAuthHeaders = async () => {
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
        name: me.artistName,
        isDJ: false,
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
      await registerApi(email, password, artistName ?? "");
      const me = await getMe();
      if (me.id) {
        setUser({
          id: me.id,
          email: me.email,
          name: me.artistName,
          isDJ: false,
        });
        setLoginStatus("authenticated");
      } else {
        setLoginStatus("unauthenticated");
        throw new Error("No se obtuvo perfil tras registro");
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
        login,
        register,
        logout,
        getAuthHeaders,
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