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

export default function DateTimeField({ label, value, onChange, hasError }: DateTimeFieldProps) {
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
      <Text style={styles.label}>{label}</Text>

      <TouchableOpacity
        style={[styles.picker, hasError && styles.pickerError]}
        onPress={open}
        activeOpacity={0.7}
      >
        <Text style={[styles.valueText, !value && styles.placeholder]}>
          {value || "AAAA-MM-DD HH:mm"}
        </Text>
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
  field: { marginBottom: 12 },
  label: {
    fontSize: 14,
    fontWeight: "500",
    color: "#374151",
    marginBottom: 6,
  },
  picker: {
    width: "100%",
    height: 48,
    borderWidth: 1,
    borderRadius: radius.control,
    paddingHorizontal: spacing.lg,
    justifyContent: "center",
    backgroundColor: colors.surfaceRaised,
    borderColor: colors.border,
  },
  pickerError: {
    borderColor: colors.danger,
  },
  valueText: {
    fontSize: 16,
    color: colors.textPrimary,
  },
  placeholder: {
    color: colors.textSecondary,
  },
  modalOverlay: {
    flex: 1,
    justifyContent: "flex-end",
    backgroundColor: "rgba(0,0,0,0.4)",
  },
  modalContent: {
    backgroundColor: "#fff",
    borderTopLeftRadius: 12,
    borderTopRightRadius: 12,
    paddingBottom: 20,
  },
  modalHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    padding: 14,
    borderBottomWidth: 1,
    borderBottomColor: "#e5e7eb",
  },
  modalTitle: {
    fontSize: 16,
    fontWeight: "600",
  },
  modalCancel: {
    fontSize: 16,
    color: "#6b7280",
  },
  modalConfirm: {
    fontSize: 16,
    fontWeight: "600",
    color: "#8a2be2",
  },
});
