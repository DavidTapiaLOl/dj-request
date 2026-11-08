import * as React from "react";
import {
  View,
  TextInput,
  StyleSheet,
  Text,
  KeyboardTypeOptions,
  Platform,
  Pressable,
  StyleProp,
  TextStyle,
} from "react-native";
import { Ionicons } from "@expo/vector-icons";
import {
  colors,
  inputTypography,
  layout,
  radius,
  spacing,
  typography,
} from "../theme/theme";

interface WebAutofillStyle extends TextStyle {
  WebkitTextFillColor?: string;
  WebkitBoxShadow?: string;
}

const webAutofillStyle: WebAutofillStyle | undefined =
  Platform.OS === "web"
    ? {
        WebkitTextFillColor: colors.textPrimary,
        WebkitBoxShadow: `0 0 0 1000px ${colors.surfaceRaised} inset`,
      }
    : undefined;

interface AppTextFieldProps {
  label: string;
  value: string;
  onChange?: (next: string) => void;
  onChangeText?: (next: string) => void;
  onBlur?: () => void;
  onFocus?: () => void;
  secureTextEntry?: boolean;
  showPasswordToggle?: boolean;
  keyboardType?: KeyboardTypeOptions;
  autoComplete?: TextInput["props"]["autoComplete"];
  autoCapitalize?: TextInput["props"]["autoCapitalize"];
  autoCorrect?: boolean;
  textContentType?: TextInput["props"]["textContentType"];
  placeholder?: string;
  placeholderTextColor?: string;
  helpText?: string;
  errorText?: string | null;
  hasError?: boolean;
  labelPosition?: "above" | "hidden";
  style?: StyleProp<TextStyle>;
}

export function AppTextField({
  label,
  value,
  onChange,
  onChangeText,
  onBlur,
  onFocus,
  secureTextEntry = false,
  showPasswordToggle = false,
  keyboardType,
  autoComplete,
  autoCapitalize,
  autoCorrect,
  textContentType,
  placeholder,
  placeholderTextColor,
  helpText,
  errorText,
  hasError = false,
  labelPosition = "above",
  style,
}: AppTextFieldProps) {
  const [focused, setFocused] = React.useState(false);
  const [passwordVisible, setPasswordVisible] = React.useState(false);
  const showError = hasError || Boolean(errorText);

  const handleFocus = React.useCallback(() => {
    setFocused(true);
    if (onFocus) onFocus();
  }, [onFocus]);

  const handleBlur = React.useCallback(() => {
    setFocused(false);
    if (onBlur) onBlur();
  }, [onBlur]);

  const showLabel = labelPosition === "above";

  return (
    <View style={styles.field}>
      {showLabel ? (
        <Text style={[styles.label, focused && styles.labelFocus]}>
          {label}
        </Text>
      ) : null}
      <View
        style={[
          styles.inputShell,
          showError && styles.inputError,
          focused && styles.inputFocused,
        ]}
      >
        <TextInput
          value={value}
          onChangeText={onChangeText || onChange}
          onFocus={handleFocus}
          onBlur={handleBlur}
          secureTextEntry={secureTextEntry && !passwordVisible}
          placeholder={placeholder || label}
          placeholderTextColor={placeholderTextColor ?? colors.textSecondary}
          style={[styles.input, style, styles.inputTextColor, webAutofillStyle]}
          autoComplete={autoComplete}
          autoCapitalize={autoCapitalize}
          autoCorrect={autoCorrect}
          textContentType={textContentType}
          keyboardType={keyboardType}
          accessibilityLabel={label}
        />
        {showPasswordToggle ? (
          <Pressable
            onPress={() => setPasswordVisible((visible) => !visible)}
            accessibilityRole="button"
            accessibilityLabel={
              passwordVisible ? "Ocultar contraseña" : "Mostrar contraseña"
            }
            hitSlop={4}
            style={({ pressed }) => [
              styles.passwordToggle,
              pressed && styles.passwordTogglePressed,
            ]}
          >
            <Ionicons
              name={passwordVisible ? "eye-off-outline" : "eye-outline"}
              size={20}
              color={colors.textSecondary}
            />
          </Pressable>
        ) : null}
      </View>
      {errorText ? (
        <Text style={styles.errorText}>{errorText}</Text>
      ) : helpText ? (
        <Text style={[styles.helpText, hasError && styles.helpTextError]}>
          {helpText}
        </Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  field: { marginBottom: spacing.lg },
  label: {
    ...typography.caption,
    color: colors.textSecondary,
    marginBottom: spacing.sm,
  },
  labelFocus: {
    ...typography.caption,
    color: colors.primary,
  },
  inputShell: {
    width: "100%",
    minHeight: 54,
    flexDirection: "row",
    alignItems: "center",
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.control,
    backgroundColor: colors.surfaceRaised,
  },
  inputError: {
    borderColor: colors.danger,
  },
  inputFocused: {
    borderColor: colors.primary,
  },
  input: {
    flex: 1,
    minHeight: 52,
    paddingHorizontal: spacing.lg,
    paddingVertical: Platform.OS === "web" ? 12 : 0,
    backgroundColor: "transparent",
    ...inputTypography,
  },
  inputTextColor: {
    color: colors.textPrimary,
  },
  passwordToggle: {
    width: layout.minTouchTarget,
    height: layout.minTouchTarget,
    alignItems: "center",
    justifyContent: "center",
    marginRight: spacing.xs,
    borderRadius: radius.control,
  },
  passwordTogglePressed: {
    backgroundColor: colors.surface,
  },
  helpText: {
    ...typography.small,
    color: colors.textSecondary,
    marginTop: spacing.sm,
  },
  helpTextError: {
    color: colors.danger,
  },
  errorText: {
    ...typography.small,
    color: colors.danger,
    marginTop: spacing.sm,
  },
});
