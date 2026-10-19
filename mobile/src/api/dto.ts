import * as z from "zod";

// --- DTOs del backend ---

export type LoginRequest = {
  email: string;
  password: string;
};

export type RegisterRequest = {
  email: string;
  password: string;
  artistName: string;
};

export type TokenResponse = {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  expiresIn: number;
};

export type MeResponse = {
  id: string;
  email: string;
  artistName: string;
};

export type RegisterResponse = {
  id: string;
  email: string;
  artistName: string;
  createdAt: string;
};

export type RefreshRequest = {
  refreshToken: string;
};

export type RoomResponse = {
  id: string;
  djId: string;
  nombre: string;
  codigo: string;
  estado: string;
  startsAt: string;
  endsAt: string;
  timezone: string;
  purgeAt: string;
  version: number;
  createdAt: string;
  updatedAt?: string;
  maxSolicitudesSesion: number;
  maxPlaylistsSesion: number;
  intervaloEnvioSegundos: number;
  hasEnded: boolean;
  canReceiveRequests: boolean;
  inviteUrl: string;
};

export type ListRoomsResponse = RoomResponse[];

// --- Esquemas Zod validación ---

export const loginSchema = z.object({
  email: z.string().email("Email inválido"),
  password: z.string().min(6, "La contraseña debe tener al menos 6 caracteres"),
});

export const registerSchema = z.object({
  email: z.string().email("Email inválido"),
  password: z.string().min(6, "La contraseña debe tener al menos 6 caracteres"),
  artistName: z.string().min(2, "Nombre de artista requerido"),
});

// --- Esquema para creación de sala (ya existía) ---

export const createRoomSchema = z.object({
  name: z.string().min(1, "Nombre requerido"),
  startsAt: z
    .string()
    .regex(
      /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$/,
      "Formato de fecha inválido"
    ),
  endsAt: z
    .string()
    .regex(
      /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[+-]\d{2}:\d{2}$/,
      "Formato de fecha inválido"
    ),
  timezone: z.string().optional(),
});