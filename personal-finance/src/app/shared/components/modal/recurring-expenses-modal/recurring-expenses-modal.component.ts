import { Component, input, output, signal, computed } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { trigger, style, animate, transition } from '@angular/animations';
import { SonicModalComponent } from '../sonic-modal/sonic-modal.component';
import { RecurringExpenseResponse } from '../../../../core/models/models';

@Component({
  selector: 'app-recurring-expenses-modal',
  standalone: true,
  imports: [SonicModalComponent, DecimalPipe],
  templateUrl: './recurring-expenses-modal.component.html',
  styleUrls: ['./recurring-expenses-modal.component.scss'],
  animations: [
    trigger('backdropAnim', [
      transition(':enter', [style({ opacity: 0 }), animate('200ms ease', style({ opacity: 1 }))]),
      transition(':leave', [animate('180ms ease', style({ opacity: 0 }))])
    ]),
    trigger('modalAnim', [
      transition(':enter', [
        style({ opacity: 0, transform: 'scale(0.88) translateY(-16px)' }),
        animate('260ms cubic-bezier(0.34,1.56,0.64,1)', style({ opacity: 1, transform: 'scale(1) translateY(0)' }))
      ]),
      transition(':leave', [
        animate('180ms ease-in', style({ opacity: 0, transform: 'scale(0.93) translateY(10px)' }))
      ])
    ])
  ]
})
export class RecurringExpensesModalComponent {
  readonly expenses  = input.required<RecurringExpenseResponse[]>();
  readonly loading   = input(false);
  readonly replicate = output<string[]>();
  readonly closed    = output<void>();

  readonly selectedIds = signal<Set<string>>(new Set());

  readonly allSelected = computed(() =>
    this.expenses().length > 0 && this.selectedIds().size === this.expenses().length
  );

  toggle(id: string): void {
    this.selectedIds.update(set => {
      const next = new Set(set);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  toggleAll(): void {
    if (this.allSelected()) {
      this.selectedIds.set(new Set());
    } else {
      this.selectedIds.set(new Set(this.expenses().map(e => e.id)));
    }
  }

  isSelected(id: string): boolean {
    return this.selectedIds().has(id);
  }

  onReplicate(): void {
    this.replicate.emit([...this.selectedIds()]);
  }
}
