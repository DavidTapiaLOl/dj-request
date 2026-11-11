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

// --- Perfil del DJ ---

export type SocialLinks = {
  instagram: string | null;
  facebook: string | null;
  tiktok: string | null;
  youtube: string | null;
  soundcloud: string | null;
};

export type ProfileResponse = {
  artistName: string;
  description: string | null;
  startYear: number | null;
  photoUrl: string | null;
  socialLinks: SocialLinks;
};

export type UpdateProfileRequest = {
  artistName: string;
  description: string | null;
  startYear: number | null;
  socialLinks: SocialLinks;
};

export type ProfilePhotoUpload = {
  uri: string;
  name: string;
  type: string;
  file?: Blob;
};

// --- Solicitudes (Song Requests) ---

export type SolicitudResponse = {
  id: string;
  nombre: string;
  artista: string | null;
  generoId: number | null;
  generoNombre: string | null;
  enlace: string | null;
  estado: string;
  version: number;
  createdAt: string;
  updatedAt: string;
};

export type SolicitudPageResponse = {
  items: SolicitudResponse[];
  page: number;
  pageSize: number;
  totalCount: number;
  hasMore: boolean;
};

export type CreateSolicitudRequest = {
  nombre: string;
  artista?: string | null;
  generoId?: number | null;
  enlace?: string | null;
  idempotencyKey: string;
};

export type UpdateSolicitudStateRequest = {
  estado: string;
  version: number;
};

// --- Esquemas Zod validación ---

export const loginSchema = z.object({
  email: z.string().email("Email inválido"),
  password: z.string(),
});

export const registerSchema = z.object({
  email: z.string().email("Email inválido"),
  password: z.string().min(12, "La contraseña debe tener al menos 12 caracteres"),
  artistName: z
    .string()
    .min(2, "Nombre de artista requerido")
    .max(100, "Nombre de artista demasiado largo"),
});

// --- Esquema para creación de sala ---

export const createRoomSchema = z.object({
  name: z
    .string()
    .min(1, "Nombre requerido")
    .max(120, "El nombre de la sala es demasiado largo"),
  startsAt: z.string().datetime({ offset: true, message: "Formato de fecha inválido" }),
  endsAt: z.string().datetime({ offset: true, message: "Formato de fecha inválido" }),
  timezone: z.string().default("America/Mexico_City").optional(),
})
.refine(data => new Date(data.endsAt) > new Date(data.startsAt), {
  message: "La fecha de fin debe ser posterior a la de inicio",
  path: ["endsAt"],
})
.refine(data => new Date(data.endsAt) > new Date(), {
  message: "La fecha de fin debe ser posterior al momento actual",
  path: ["endsAt"],
});