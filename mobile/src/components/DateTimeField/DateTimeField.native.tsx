import * as React from "react";
import {
  View,
  Text,
  TouchableOpacity,
  Modal,
  Pressable,
  StyleSheet,
  Platform,
} from "react-native";
import DateTimePicker, {
  DateTimePickerAndroid,
  type DateTimePickerEvent,
} from "@react-native-community/datetimepicker";
import { Ionicons } from "@expo/vector-icons";
import type { DateTimeFieldProps } from "./types";
import { colors, radius, spacing } from "../../theme/theme";

export type { DateTimeFieldProps };

function pad(n: number): string {
  return n < 10 ? `0${n}` : String(n);
}

function formatLocal(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function parseValue(value: string): Date | null {
  if (!value) return null;
  const m = value.match(/^(\d{4})-(\d{2})-(\d{2}) (\d{2}):(\d{2})$/);
  if (!m) return null;
  return new Date(
    parseInt(m[1], 10),
    parseInt(m[2], 10) - 1,
    parseInt(m[3], 10),
    parseInt(m[4], 10),
    parseInt(m[5], 10),
    0,
    0
  );
}

function initialDate(value: string): Date {
  const parsed = parseValue(value);
  if (parsed) return parsed;
  const now = new Date();
  return new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0, 0);
}

function mergeDateAndTime(datePart: Date, timePart: Date): Date {
  return new Date(
    datePart.getFullYear(),
    datePart.getMonth(),
    datePart.getDate(),
    timePart.getHours(),
    timePart.getMinutes(),
    0,
    0
  );
}

export default function DateTimeField({
  label,
  value,
  onChange,
  hasError,
  labelPosition = "above",
}: DateTimeFieldProps) {
  const [iosVisible, setIosVisible] = React.useState(false);
  const [iosDate, setIosDate] = React.useState<Date>(() => initialDate(value));

  const handleAndroidDate = React.useCallback(
    (event: DateTimePickerEvent, selected?: Date) => {
      if (event.type === "dismissed" || !selected) return;
      const currentDate = selected;
      DateTimePickerAndroid.open({
        value: currentDate,
        mode: "time",
        is24Hour: true,
        display: "default",
        onChange: (timeEvent, timeSelected) => {
          if (timeEvent.type === "dismissed" || !timeSelected) return;
          onChange(formatLocal(mergeDateAndTime(currentDate, timeSelected)));
        },
      });
    },
    [onChange]
  );

  const openAndroid = React.useCallback(() => {
    DateTimePickerAndroid.open({
      value: initialDate(value),
      mode: "date",
      display: "calendar",
      onChange: handleAndroidDate,
    });
  }, [value, handleAndroidDate]);

  const openIos = React.useCallback(() => {
    setIosDate(initialDate(value));
    setIosVisible(true);
  }, [value]);

  const handleIosConfirm = React.useCallback(() => {
    onChange(formatLocal(iosDate));
    setIosVisible(false);
  }, [iosDate, onChange]);

  const handleIosCancel = React.useCallback(() => {
    setIosVisible(false);
  }, []);

  const open = Platform.OS === "android" ? openAndroid : openIos;

  return (
    <View style={styles.field}>
      {labelPosition === "above" ? (
        <Text style={styles.label}>{label}</Text>
      ) : null}

      <TouchableOpacity
        style={[
          styles.picker,
          labelPosition === "inside" && styles.pickerInside,
          hasError && styles.pickerError,
        ]}
        onPress={open}
        activeOpacity={0.7}
        accessibilityRole="button"
        accessibilityLabel={`${label}${value ? `: ${value}` : ""}`}
      >
        {labelPosition === "inside" ? (
          <View style={styles.innerText}>
            <Text style={styles.innerLabel}>{label}</Text>
            <Text style={[styles.valueText, !value && styles.placeholder]}>
              {value || "Selecciona fecha y hora"}
            </Text>
          </View>
        ) : (
          <Text style={[styles.valueText, !value && styles.placeholder]}>
            {value || "Selecciona fecha y hora"}
          </Text>
        )}
        <Ionicons
          name="calendar-outline"
          size={20}
          color={colors.textSecondary}
        />
      </TouchableOpacity>

      {Platform.OS === "ios" && (
        <Modal visible={iosVisible} transparent animationType="slide">
          <View style={styles.modalOverlay}>
            <View style={styles.modalContent}>
              <View style={styles.modalHeader}>
                <Pressable onPress={handleIosCancel} hitSlop={8}>
                  <Text style={styles.modalCancel}>Cancelar</Text>
                </Pressable>
                <Text style={styles.modalTitle}>{label}</Text>
                <Pressable onPress={handleIosConfirm} hitSlop={8}>
                  <Text style={styles.modalConfirm}>Listo</Text>
                </Pressable>
              </View>
              <DateTimePicker
                value={iosDate}
                mode="datetime"
                display="spinner"
                minuteInterval={1}
                onChange={(_, d) => {
                  if (d) setIosDate(d);
                }}
              />
            </View>
          </View>
        </Modal>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  field: { marginBottom: spacing.lg },
  label: {
    fontSize: 14,
    fontWeight: "500",
    color: colors.textSecondary,
    marginBottom: spacing.sm,
  },
  picker: {
    width: "100%",
    minHeight: 54,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: spacing.sm,
    borderWidth: 1,
    borderRadius: radius.control,
    paddingHorizontal: spacing.lg,
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.border,
  },
  pickerInside: {
    minHeight: 62,
    alignItems: "center",
    paddingVertical: spacing.sm,
  },
  pickerError: {
    borderColor: colors.danger,
  },
  innerText: {
    flex: 1,
    minWidth: 0,
  },
  innerLabel: {
    fontSize: 12,
    fontWeight: "400",
    color: colors.textSecondary,
    marginBottom: 2,
  },
  valueText: {
    flexShrink: 1,
    fontSize: 16,
    fontWeight: "400",
    color: colors.textPrimary,
  },
  placeholder: {
    color: colors.textSecondary,
  },
  modalOverlay: {
    flex: 1,
    justifyContent: "flex-end",
    backgroundColor: "rgba(0,0,0,0.6)",
  },
  modalContent: {
    backgroundColor: colors.surface,
    borderTopLeftRadius: radius.card,
    borderTopRightRadius: radius.card,
    paddingBottom: spacing.xl,
  },
  modalHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    padding: spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  modalTitle: {
    fontSize: 16,
    fontWeight: "600",
    color: colors.textPrimary,
  },
  modalCancel: {
    fontSize: 16,
    color: colors.textSecondary,
  },
  modalConfirm: {
    fontSize: 16,
    fontWeight: "600",
    color: colors.primary,
  },
});
