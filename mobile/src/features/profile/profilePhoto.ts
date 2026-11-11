import { Platform } from "react-native";
import type { ImagePickerAsset } from "expo-image-picker";
import type { ProfilePhotoUpload } from "@/api/dto";

const DEFAULT_MIME_TYPE = "image/jpeg";

function dataUrlMimeType(dataUrl: string): string | null {
  const match = /^data:([^;,]+)[;,]/.exec(dataUrl);
  return match ? match[1] : null;
}

function extensionFor(mimeType: string): string {
  return mimeType === "image/png" ? "png" : "jpg";
}

function fallbackName(mimeType: string): string {
  return `perfil.${extensionFor(mimeType)}`;
}

function dataUrlToBlob(dataUrl: string, fallbackType: string): Blob | null {
  const match = /^data:([^;,]+)?(;base64)?,([\s\S]*)$/.exec(dataUrl);
  if (!match) {
    return null;
  }

  const mimeType = match[1] || fallbackType;
  const isBase64 = match[2] === ";base64";
  const payload = match[3];

  try {
    if (isBase64) {
      const binary = atob(payload);
      const bytes = new Uint8Array(binary.length);
      for (let index = 0; index < binary.length; index += 1) {
        bytes[index] = binary.charCodeAt(index);
      }
      return new Blob([bytes], { type: mimeType });
    }

    return new Blob([decodeURIComponent(payload)], { type: mimeType });
  } catch {
    return null;
  }
}

export async function buildProfilePhotoUpload(
  asset: ImagePickerAsset
): Promise<ProfilePhotoUpload | null> {
  const declaredType =
    asset.mimeType || dataUrlMimeType(asset.uri) || DEFAULT_MIME_TYPE;
  const declaredName = asset.fileName || fallbackName(declaredType);

  if (Platform.OS !== "web") {
    return {
      uri: asset.uri,
      name: declaredName,
      type: declaredType,
    };
  }

  const webFile = (asset as ImagePickerAsset & { file?: File }).file;
  if (webFile) {
    return {
      uri: asset.uri,
      name: webFile.name || declaredName,
      type: webFile.type || declaredType,
      file: webFile,
    };
  }

  const blob = dataUrlToBlob(asset.uri, declaredType);
  if (!blob) {
    return null;
  }

  const type = blob.type || declaredType;
  const name = declaredName || fallbackName(type);
  const file =
    typeof File !== "undefined" ? new File([blob], name, { type }) : blob;

  return {
    uri: asset.uri,
    name,
    type,
    file,
  };
}
