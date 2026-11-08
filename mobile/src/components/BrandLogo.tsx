import * as React from "react";
import {
  StyleProp,
  StyleSheet,
  Text,
  View,
  ViewStyle,
} from "react-native";
import Svg, { Path, Rect } from "react-native-svg";
import { colors } from "../theme/theme";

export const AUTH_BRAND_SIZE = 88;
export const AUTH_BRAND_WORDMARK_SIZE = 26;

export type BrandLogoProps = {
  size?: number;
  showWordmark?: boolean;
  wordmarkSize?: number;
  orientation?: "inline" | "stacked";
  showArcs?: boolean;
  decorative?: boolean;
  style?: StyleProp<ViewStyle>;
};

export function AuthBrand({ style }: { style?: StyleProp<ViewStyle> }) {
  return (
    <BrandLogo
      size={AUTH_BRAND_SIZE}
      showArcs
      showWordmark
      wordmarkSize={AUTH_BRAND_WORDMARK_SIZE}
      style={style}
    />
  );
}

export function BrandLogo({
  size = 32,
  showWordmark = false,
  wordmarkSize = Math.max(16, size * 0.34),
  orientation = "stacked",
  showArcs = false,
  decorative = false,
  style,
}: BrandLogoProps) {
  const arcSize = Math.max(size * 2.6, 180);
  const arcOffset = (arcSize - size) / 2;
  const isInline = orientation === "inline";

  return (
    <View
      accessible={!decorative}
      accessibilityRole={decorative ? undefined : "image"}
      accessibilityLabel={decorative ? undefined : "DJ Requests"}
      style={[
        styles.root,
        isInline ? styles.inline : styles.stacked,
        style,
      ]}
    >
      <View style={[styles.markBox, { width: size, height: size }]}>
        {showArcs ? (
          <Svg
            width={arcSize}
            height={arcSize}
            viewBox="0 0 240 240"
            pointerEvents="none"
            style={{
              position: "absolute",
              left: -arcOffset,
              top: -arcOffset,
            }}
          >
            <Path
              d="M28 120 A92 92 0 0 1 212 120"
              fill="none"
              stroke={colors.primary}
              strokeWidth={1.5}
              strokeLinecap="round"
              opacity={0.2}
            />
            <Path
              d="M56 120 A64 64 0 0 1 184 120"
              fill="none"
              stroke={colors.primary}
              strokeWidth={1.5}
              strokeLinecap="round"
              opacity={0.14}
            />
            <Path
              d="M84 120 A36 36 0 0 1 156 120"
              fill="none"
              stroke={colors.primary}
              strokeWidth={1.5}
              strokeLinecap="round"
              opacity={0.09}
            />
          </Svg>
        ) : null}

        <Svg width={size} height={size} viewBox="0 0 56 56">
          <Path
            d="M14 8 L14 42 Q14 50 28 50 Q42 50 50 42 L50 8 Q42 8 28 8 L14 8 Z"
            fill="none"
            stroke={colors.primary}
            strokeWidth={4}
            strokeLinecap="round"
            strokeLinejoin="round"
          />
          <Rect x="19" y="27" width="5" height="13" rx="1.5" fill={colors.primary} />
          <Rect x="26" y="18" width="5" height="22" rx="1.5" fill={colors.primary} />
          <Rect x="33" y="9" width="5" height="31" rx="1.5" fill={colors.primary} />
        </Svg>
      </View>

      {showWordmark ? (
        <Text
          accessible={false}
          style={[styles.wordmark, { fontSize: wordmarkSize }]}
        >
          DJ Requests
        </Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  root: {
    alignItems: "center",
    justifyContent: "center",
  },
  inline: {
    flexDirection: "row",
    gap: 10,
  },
  stacked: {
    flexDirection: "column",
    gap: 12,
  },
  markBox: {
    alignItems: "center",
    justifyContent: "center",
  },
  wordmark: {
    color: colors.textPrimary,
    fontWeight: "700",
    letterSpacing: -0.5,
    textAlign: "center",
  },
});
