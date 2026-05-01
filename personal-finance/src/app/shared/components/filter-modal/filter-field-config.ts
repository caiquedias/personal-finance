export interface FilterFieldOption {
  value: unknown;
  label: string;
}

export interface FilterFieldConfig {
  key: string;
  label: string;
  type: 'text' | 'select' | 'multiSelect';
  options?: FilterFieldOption[];
  value?: unknown;
}
