export interface DateTimeFieldProps {
  label: string;
  value: string;
  onChange: (next: string) => void;
  hasError?: boolean;
}
