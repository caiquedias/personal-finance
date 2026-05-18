export interface FilterFieldOption {
  value: unknown;
  label: string;
}

export interface FilterFieldConfig {
  key: string;
  label: string;
  type: 'text' | 'select' | 'multiSelect' | 'sectionHeader';
  options?: FilterFieldOption[];
  value?: unknown;
}
