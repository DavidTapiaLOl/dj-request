import { Platform, TextStyle } from "react-native";

export const colors = {
  background: "#0B0D12",
  surface: "#161A23",
  surfaceRaised: "#202634",
  border: "#303848",
  primary: "#8B5CF6",
  primaryPressed: "#7C3AED",
  textPrimary: "#F5F7FA",
  onPrimary: "#0B0D12",
  textSecondary: "#A7B0C0",
  success: "#4ADE80",
  warning: "#FBBF24",
  neutral: "#6B7280",
  danger: "#F87171",
} as const;

export const spacing = {
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 24,
  xxl: 32,
} as const;

export const radius = {
  card: 16,
  control: 12,
} as const;

export const motion = {
  durationFast: 120,
  durationBase: 200,
  contentOffset: 8,
  pressScale: 0.98,
} as const;

export const typography = {
  title: { fontSize: 28, fontWeight: "700" } as TextStyle,
  cardTitle: { fontSize: 18, fontWeight: "600" } as TextStyle,
  body: { fontSize: 16 } as TextStyle,
  caption: { fontSize: 14 } as TextStyle,
  small: { fontSize: 13 } as TextStyle,
} as const;

export const inputTypography = {
  fontFamily: Platform.OS === "web" ? "Arial, sans-serif" : undefined,
  fontSize: 16,
  fontWeight: "400" as const,
} as TextStyle;

export const layout = {
  maxContentWidth: 720,
  minTouchTarget: 48,
} as const;

export const theme = {
  colors,
  spacing,
  radius,
  typography,
  layout,
} as const;

export type Theme = typeof theme;
