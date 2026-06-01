import { Component, inject, signal, computed, input, output } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { ApiService } from '../../../../core/services/api.service';
import { MarioModalComponent } from '../mario-modal/mario-modal.component';
import {
  CategoryResponse, ExpenseResponse,
  SourceType, FortnightType,
} from '../../../../core/models/models';

export interface BatchFormItem {
  categoryId:    string;
  sourceType:    SourceType;
  fortnightType: FortnightType;
  description:   string;
  amount:        number;
  dueDate:       string;
  notes?:        string;
  isRecurring:   boolean;
}

@Component({
  selector: 'app-batch-expenses-modal',
  standalone: true,
  imports: [ReactiveFormsModule, DecimalPipe, MarioModalComponent],
  templateUrl: './batch-expenses-modal.component.html',
  styleUrls: ['./batch-expenses-modal.component.css'],
})
export class BatchExpensesModalComponent {
  private readonly api = inject(ApiService);
  private readonly fb  = inject(FormBuilder);

  readonly periodId  = input.required<string>();
  readonly categories = input<CategoryResponse[]>([]);

  readonly closed = output<void>();
  readonly saved  = output<ExpenseResponse[]>();

  readonly SourceType    = SourceType;
  readonly FortnightType = FortnightType;

  readonly items        = signal<BatchFormItem[]>([]);
  readonly editingIndex = signal<number | null>(null);
  readonly saving       = signal(false);

  readonly marioOpen    = signal(false);
  readonly marioContent = signal('');

  readonly newForm = this.fb.group({
    categoryId:    ['', Validators.required],
    sourceType:    [SourceType.Personal, Validators.required],
    fortnightType: [FortnightType.First, Validators.required],
    description:   ['', Validators.required],
    amount:        [null as number | null, [Validators.required, Validators.min(0.01)]],
    dueDate:       ['', Validators.required],
    notes:         [''],
    isRecurring:   [false],
  });

  readonly editForm = this.fb.group({
    categoryId:    ['', Validators.required],
    sourceType:    [SourceType.Personal, Validators.required],
    fortnightType: [FortnightType.First, Validators.required],
    description:   ['', Validators.required],
    amount:        [null as number | null, [Validators.required, Validators.min(0.01)]],
    dueDate:       ['', Validators.required],
    notes:         [''],
    isRecurring:   [false],
  });

  canSave(): boolean {
    return this.items().length > 0;
  }

  addItem(): void {
    if (this.newForm.invalid) {
      this.newForm.markAllAsTouched();
      return;
    }
    const v = this.newForm.getRawValue();
    this.items.update(list => [...list, {
      categoryId:    v.categoryId!,
      sourceType:    v.sourceType!,
      fortnightType: v.fortnightType!,
      description:   v.description!,
      amount:        v.amount!,
      dueDate:       v.dueDate!,
      notes:         v.notes || undefined,
      isRecurring:   v.isRecurring ?? false,
    }]);
    this.newForm.reset({
      categoryId: '', sourceType: SourceType.Personal,
      fortnightType: FortnightType.First, description: '',
      amount: null, dueDate: '', notes: '', isRecurring: false,
    });
  }

  startEditItem(index: number): void {
    const item = this.items()[index];
    this.editForm.patchValue({
      categoryId:    item.categoryId,
      sourceType:    item.sourceType,
      fortnightType: item.fortnightType,
      description:   item.description,
      amount:        item.amount,
      dueDate:       item.dueDate,
      notes:         item.notes ?? '',
      isRecurring:   item.isRecurring,
    });
    this.editingIndex.set(index);
  }

  confirmEditItem(): void {
    const idx = this.editingIndex();
    if (idx === null || this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }
    const v = this.editForm.getRawValue();
    this.items.update(list => {
      const updated = [...list];
      updated[idx] = {
        categoryId:    v.categoryId!,
        sourceType:    v.sourceType!,
        fortnightType: v.fortnightType!,
        description:   v.description!,
        amount:        v.amount!,
        dueDate:       v.dueDate!,
        notes:         v.notes || undefined,
        isRecurring:   v.isRecurring ?? false,
      };
      return updated;
    });
    this.editingIndex.set(null);
  }

  cancelEdit(): void {
    this.editingIndex.set(null);
  }

  removeItem(index: number): void {
    const editIdx = this.editingIndex();
    this.items.update(list => list.filter((_, i) => i !== index));
    if (editIdx !== null) {
      if (editIdx === index) {
        this.editingIndex.set(null);
      } else if (editIdx > index) {
        this.editingIndex.set(editIdx - 1);
      }
    }
  }

  save(): void {
    if (!this.canSave() || this.saving()) return;
    this.saving.set(true);
    this.api.createExpensesBatch({
      periodId: this.periodId(),
      items: this.items(),
    }).subscribe({
      next: (result) => {
        this.saving.set(false);
        this.saved.emit(result);
      },
      error: (err) => {
        this.saving.set(false);
        this.marioContent.set(err?.error?.message ?? 'Erro ao salvar as despesas em lote.');
        this.marioOpen.set(true);
      },
    });
  }

  close(): void {
    this.closed.emit();
  }
}
