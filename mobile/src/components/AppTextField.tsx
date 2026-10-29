import * as React from "react";
import {
  View,
  TextInput,
  StyleSheet,
  Text,
  KeyboardTypeOptions,
  Platform,
  StyleProp,
  TextStyle,
} from "react-native";
import { colors, radius, spacing, typography } from "../theme/theme";

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
  keyboardType?: KeyboardTypeOptions;
  autoComplete?: TextInput["props"]["autoComplete"];
  autoCorrect?: boolean;
  textContentType?: TextInput["props"]["textContentType"];
  placeholder?: string;
  placeholderTextColor?: string;
  helpText?: string;
  hasError?: boolean;
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
  keyboardType,
  autoComplete,
  autoCorrect,
  textContentType,
  placeholder,
  placeholderTextColor,
  helpText,
  hasError = false,
  style,
}: AppTextFieldProps) {
  const [focused, setFocused] = React.useState(false);

  const handleFocus = React.useCallback(() => {
    setFocused(true);
    if (onFocus) onFocus();
  }, [onFocus]);

  const handleBlur = React.useCallback(() => {
    setFocused(false);
    if (onBlur) onBlur();
  }, [onBlur]);

  return (
    <View style={styles.field}>
      <Text style={[styles.label, focused && styles.labelFocus]}>{label}</Text>
      <TextInput
        value={value}
        onChangeText={onChangeText || onChange}
        onFocus={handleFocus}
        onBlur={handleBlur}
        secureTextEntry={secureTextEntry}
        placeholder={placeholder || label}
        placeholderTextColor={placeholderTextColor ?? colors.textSecondary}
        style={[
          styles.input,
          hasError && styles.inputError,
          focused && styles.inputFocused,
          style,
          styles.inputTextColor,
          webAutofillStyle,
        ]}
        autoComplete={autoComplete}
        autoCorrect={autoCorrect}
        textContentType={textContentType}
        keyboardType={keyboardType}
      />
      {helpText && (
        <Text style={[styles.helpText, hasError && styles.helpTextError]}>{helpText}</Text>
      )}
      {hasError && (
        <Text style={styles.errorText}>{/* Error message set by parent, not helpText */}</Text>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  field: { marginBottom: spacing.md },
  label: {
    ...typography.caption,
    color: colors.textSecondary,
    marginBottom: spacing.xs,
  },
  labelFocus: {
    ...typography.caption,
    color: colors.primary,
  },
  input: {
    width: "100%",
    height: 50,
    borderColor: colors.border,
    borderWidth: 1,
    borderRadius: radius.control,
    paddingHorizontal: spacing.lg,
    fontSize: 16,
    backgroundColor: colors.surfaceRaised,
    color: colors.textPrimary,
    fontFamily: "inherit",
  },
  inputTextColor: {
    color: colors.textPrimary,
  },
  inputError: {
    borderColor: colors.danger,
  },
  inputFocused: {
    borderColor: colors.primary,
  },
  helpText: {
    ...typography.small,
    color: colors.textSecondary,
    marginTop: spacing.xs,
  },
  helpTextError: {
    color: colors.danger,
  },
  errorText: {
    ...typography.caption,
    color: colors.danger,
    marginTop: spacing.xs,
  },
});