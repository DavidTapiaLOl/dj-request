import type {
  ProfileResponse,
  SocialLinks,
  UpdateProfileRequest,
} from "@/api/dto";

export type SocialPlatformKey = keyof SocialLinks;

export interface SocialPlatform {
  key: SocialPlatformKey;
  label: string;
  placeholder: string;
  hosts: string[];
}

export const MAX_ARTIST_NAME_LENGTH = 100;
export const MAX_DESCRIPTION_LENGTH = 1500;
export const MIN_START_YEAR = 1900;

export const socialPlatforms: SocialPlatform[] = [
  {
    key: "instagram",
    label: "Instagram",
    placeholder: "https://instagram.com/tu-usuario",
    hosts: ["instagram.com", "www.instagram.com"],
  },
  {
    key: "facebook",
    label: "Facebook",
    placeholder: "https://facebook.com/tu-pagina",
    hosts: [
      "facebook.com",
      "www.facebook.com",
      "m.facebook.com",
      "web.facebook.com",
    ],
  },
  {
    key: "tiktok",
    label: "TikTok",
    placeholder: "https://tiktok.com/@tu-usuario",
    hosts: ["tiktok.com", "www.tiktok.com", "m.tiktok.com", "vm.tiktok.com"],
  },
  {
    key: "youtube",
    label: "YouTube",
    placeholder: "https://youtube.com/@tu-canal",
    hosts: [
      "youtube.com",
      "www.youtube.com",
      "m.youtube.com",
      "music.youtube.com",
      "youtu.be",
      "www.youtu.be",
    ],
  },
  {
    key: "soundcloud",
    label: "SoundCloud",
    placeholder: "https://soundcloud.com/tu-usuario",
    hosts: [
      "soundcloud.com",
      "www.soundcloud.com",
      "m.soundcloud.com",
      "on.soundcloud.com",
    ],
  },
];

export interface ProfileFormValues {
  artistName: string;
  description: string;
  startYear: string;
  socialLinks: Record<SocialPlatformKey, string>;
}

export type ProfileFormField =
  | "artistName"
  | "description"
  | "startYear"
  | SocialPlatformKey;

export type ProfileFormErrors = Partial<Record<ProfileFormField, string>>;

export function profileToForm(profile: ProfileResponse): ProfileFormValues {
  return {
    artistName: profile.artistName ?? "",
    description: profile.description ?? "",
    startYear: profile.startYear ? String(profile.startYear) : "",
    socialLinks: {
      instagram: profile.socialLinks?.instagram ?? "",
      facebook: profile.socialLinks?.facebook ?? "",
      tiktok: profile.socialLinks?.tiktok ?? "",
      youtube: profile.socialLinks?.youtube ?? "",
      soundcloud: profile.socialLinks?.soundcloud ?? "",
    },
  };
}

export function validateSocialLink(
  platform: SocialPlatform,
  value: string
): string | null {
  const trimmed = value.trim();
  if (!trimmed) {
    return null;
  }

  let url: URL;
  try {
    url = new URL(trimmed);
  } catch {
    return "Ingresa una URL completa que empiece por https://.";
  }

  if (url.protocol !== "https:") {
    return "El enlace debe usar HTTPS.";
  }

  if (url.username || url.password) {
    return "El enlace no puede incluir credenciales.";
  }

  if (url.port) {
    return "El enlace no puede incluir un puerto.";
  }

  if (!platform.hosts.includes(url.hostname.toLowerCase())) {
    return `El enlace debe ser de ${platform.hosts[0]}.`;
  }

  return null;
}

export function validateProfileForm(
  values: ProfileFormValues,
  currentYear: number
): ProfileFormErrors {
  const errors: ProfileFormErrors = {};

  const artistName = values.artistName.trim();
  if (!artistName) {
    errors.artistName = "El nombre artístico es obligatorio.";
  } else if (artistName.length > MAX_ARTIST_NAME_LENGTH) {
    errors.artistName = `El nombre artístico no puede superar los ${MAX_ARTIST_NAME_LENGTH} caracteres.`;
  }

  if (values.description.trim().length > MAX_DESCRIPTION_LENGTH) {
    errors.description = `La descripción no puede superar los ${MAX_DESCRIPTION_LENGTH} caracteres.`;
  }

  const yearText = values.startYear.trim();
  if (yearText) {
    if (!/^\d{4}$/.test(yearText)) {
      errors.startYear = "Ingresa un año de 4 dígitos.";
    } else {
      const year = Number(yearText);
      if (year < MIN_START_YEAR || year > currentYear) {
        errors.startYear = `El año debe estar entre ${MIN_START_YEAR} y ${currentYear}.`;
      }
    }
  }

  for (const platform of socialPlatforms) {
    const error = validateSocialLink(platform, values.socialLinks[platform.key]);
    if (error) {
      errors[platform.key] = error;
    }
  }

  return errors;
}

export function toUpdateProfileRequest(
  values: ProfileFormValues
): UpdateProfileRequest {
  const yearText = values.startYear.trim();

  return {
    artistName: values.artistName.trim(),
    description: values.description.trim() || null,
    startYear: yearText ? Number(yearText) : null,
    socialLinks: {
      instagram: values.socialLinks.instagram.trim() || null,
      facebook: values.socialLinks.facebook.trim() || null,
      tiktok: values.socialLinks.tiktok.trim() || null,
      youtube: values.socialLinks.youtube.trim() || null,
      soundcloud: values.socialLinks.soundcloud.trim() || null,
    },
  };
}

const serverFieldMap: Record<string, ProfileFormField> = {
  ArtistName: "artistName",
  Description: "description",
  StartYear: "startYear",
  Instagram: "instagram",
  Facebook: "facebook",
  Tiktok: "tiktok",
  Youtube: "youtube",
  Soundcloud: "soundcloud",
};

export function mapServerErrors(
  errors: Record<string, string[]> | undefined
): ProfileFormErrors {
  const result: ProfileFormErrors = {};
  if (!errors) {
    return result;
  }

  for (const [key, messages] of Object.entries(errors)) {
    const field = serverFieldMap[key];
    if (field && messages.length > 0) {
      result[field] = messages[0];
    }
  }

  return result;
}
