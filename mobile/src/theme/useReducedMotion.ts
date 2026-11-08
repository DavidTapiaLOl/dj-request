import * as React from "react";
import { Platform, AccessibilityInfo } from "react-native";

let cachedReducedMotion: boolean | null = null;

export function useReducedMotion(): boolean | null {
  const [reduced, setReduced] = React.useState<boolean | null>(() => {
    if (Platform.OS === "web") {
      const mm = (globalThis as any).matchMedia?.(
        "(prefers-reduced-motion: reduce)"
      );
      if (mm) {
        cachedReducedMotion = mm.matches;
        return mm.matches;
      }
      cachedReducedMotion = false;
      return false;
    }
    return cachedReducedMotion;
  });

  React.useEffect(() => {
    if (Platform.OS === "web") {
      const mm = (globalThis as any).matchMedia?.(
        "(prefers-reduced-motion: reduce)"
      );
      if (mm) {
        const handler = (e: any) => {
          cachedReducedMotion = e.matches;
          setReduced(e.matches);
        };
        mm.addEventListener?.("change", handler);
        return () => {
          mm.removeEventListener?.("change", handler);
        };
      }
    } else {
      AccessibilityInfo.isReduceMotionEnabled()
        .then((v) => {
          cachedReducedMotion = v;
          setReduced(v);
        })
        .catch(() => {
          cachedReducedMotion = false;
          setReduced(false);
        });

      const sub = AccessibilityInfo.addEventListener?.(
        "reduceMotionChanged",
        (v) => {
          cachedReducedMotion = v;
          setReduced(v);
        }
      );

      return () => {
        sub?.remove?.();
      };
    }
  }, []);

  return reduced;
}
