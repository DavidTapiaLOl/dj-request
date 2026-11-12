export interface RoomMinimal {
  id: string;
  name: string;
  state: string;
  startsAt: string;
  endsAt: string;
  timezone: string;
  canReceiveRequests: boolean;
  maxSolicitudesSesion: number;
  maxPlaylistsSesion: number;
  intervaloEnvioSegundos: number;
}

export interface GuestSessionResponse {
  guestToken: string;
  expiresAt: string;
  room: RoomMinimal;
}

export interface GuestRoomResponse {
  room: RoomMinimal;
  expiresAt: string;
}

export interface GuestSession {
  guestToken: string;
  expiresAt: string;
  room: RoomMinimal;
  code: string;
}

export interface Solicitud {
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
}

export interface SolicitudPage {
  items: Solicitud[];
  page: number;
  pageSize: number;
  totalCount: number;
  hasMore: boolean;
}

export interface CreateSolicitudInput {
  nombre: string;
  artista: string | null;
  generoId: number | null;
  enlace: string | null;
}

export interface CreateSolicitudPayload extends CreateSolicitudInput {
  idempotencyKey: string;
}

export interface Playlist {
  id: string;
  nombre: string | null;
  enlace: string;
  createdAt: string;
}

export interface CreatePlaylistInput {
  nombre: string | null;
  enlace: string;
}

export interface CreatePlaylistPayload extends CreatePlaylistInput {
  idempotencyKey: string;
}

export interface Genero {
  id: number;
  nombre: string;
}

export interface SocialLinks {
  instagram: string | null;
  facebook: string | null;
  tiktok: string | null;
  youtube: string | null;
  soundcloud: string | null;
}

export interface DjProfile {
  artistName: string;
  description: string | null;
  startYear: number | null;
  photoUrl: string | null;
  socialLinks: SocialLinks;
}
