import * as React from "react";
import { Animated, Platform, StyleProp, ViewStyle } from "react-native";
import { motion } from "../theme/theme";
import { useReducedMotion } from "../theme/useReducedMotion";

interface FadeInViewProps {
  children: React.ReactNode;
  style?: StyleProp<ViewStyle>;
}

export function FadeInView({ children, style }: FadeInViewProps) {
  const reduceMotion = useReducedMotion();
  const opacity = React.useRef(new Animated.Value(0)).current;
  const translateY = React.useRef(
    new Animated.Value(motion.contentOffset)
  ).current;

  React.useEffect(() => {
    if (reduceMotion === null) {
      return;
    }

    if (reduceMotion) {
      opacity.setValue(1);
      translateY.setValue(0);
      return;
    }

    const animation = Animated.parallel([
      Animated.timing(opacity, {
        toValue: 1,
        duration: 250,
        useNativeDriver: Platform.OS !== "web",
      }),
      Animated.timing(translateY, {
        toValue: 0,
        duration: 250,
        useNativeDriver: Platform.OS !== "web",
      }),
    ]);

    animation.start();

    return () => {
      animation.stop();
    };
  }, [reduceMotion, opacity, translateY]);

  return (
    <Animated.View
      style={[
        style,
        {
          opacity,
          transform: [{ translateY }],
        },
      ]}
    >
      {children}
    </Animated.View>
  );
}
