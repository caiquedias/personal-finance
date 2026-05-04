import { Component, input, output, signal, effect, untracked } from '@angular/core';
import { trigger, style, animate, transition } from '@angular/animations';
import { FilterFieldConfig } from './filter-field-config';

@Component({
  selector: 'app-filter-modal',
  standalone: true,
  templateUrl: './filter-modal.component.html',
  styleUrls: ['./filter-modal.component.css'],
  animations: [
    trigger('backdropAnim', [
      transition(':enter', [style({ opacity: 0 }), animate('180ms ease', style({ opacity: 1 }))]),
      transition(':leave', [animate('150ms ease', style({ opacity: 0 }))])
    ]),
    trigger('panelAnim', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(-8px) scale(0.97)' }),
        animate('200ms ease', style({ opacity: 1, transform: 'translateY(0) scale(1)' }))
      ]),
      transition(':leave', [
        animate('150ms ease-in', style({ opacity: 0, transform: 'translateY(-8px) scale(0.97)' }))
      ])
    ])
  ]
})
export class FilterModalComponent {
  readonly fields = input<FilterFieldConfig[]>([]);
  readonly open   = input(false);

  readonly apply  = output<Record<string, unknown>>();
  readonly clear  = output<void>();
  readonly closed = output<void>();

  readonly draftValues = signal<Record<string, unknown>>({});

  constructor() {
    // Ao abrir o painel, sincroniza o rascunho com o estado atual dos filtros
    effect(() => {
      if (this.open()) {
        const fs = this.fields();
        untracked(() => {
          const initial: Record<string, unknown> = {};
          for (const f of fs) {
            initial[f.key] = f.value ?? (f.type === 'multiSelect' ? [] : '');
          }
          this.draftValues.set(initial);
        });
      }
    });
  }

  setValue(key: string, value: unknown): void {
    this.draftValues.update(v => ({ ...v, [key]: value }));
  }

  toggleMultiValue(key: string, value: unknown): void {
    const current = this.getArrayValue(key);
    const updated = current.includes(value)
      ? current.filter(v => v !== value)
      : [...current, value];
    this.setValue(key, updated);
  }

  onApply(): void {
    this.apply.emit(this.draftValues());
    this.closed.emit();
  }

  onClear(): void {
    const cleared: Record<string, unknown> = {};
    for (const f of this.fields()) {
      cleared[f.key] = f.type === 'multiSelect' ? [] : '';
    }
    this.draftValues.set(cleared);
    this.clear.emit();
    this.closed.emit();
  }

  onClose(): void {
    this.closed.emit();
  }

  getStringValue(key: string): string {
    return (this.draftValues()[key] as string) ?? '';
  }

  getArrayValue(key: string): unknown[] {
    return (this.draftValues()[key] as unknown[]) ?? [];
  }

  isChecked(key: string, value: unknown): boolean {
    return this.getArrayValue(key).includes(value);
  }
}
