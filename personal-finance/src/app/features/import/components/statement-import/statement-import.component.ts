import { Component, inject, signal, OnInit } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../../core/services/api.service';
import { CategoryResponse, ConfirmStatementImportResult } from '../../../../core/models/models';

export interface StatementReviewItem {
  date:                     string; // yyyy-MM-dd
  description:              string;
  amount:                   number;
  kind:                     'Income' | 'Expense';
  categoryId:               string | null;
  sourceType:               string;
  isLikelyInternalTransfer: boolean;
  isLikelyDuplicate:        boolean;
}

const MAX_FILE_BYTES = 10 * 1024 * 1024;
const MAX_DESCRIPTION = 200;

@Component({
  selector: 'app-statement-import',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  templateUrl: './statement-import.component.html',
  styleUrls: ['./statement-import.component.css'],
})
export class StatementImportComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly selectedFile   = signal<File | null>(null);
  readonly password       = signal('');
  readonly fromDate       = signal('');
  readonly categories     = signal<CategoryResponse[]>([]);
  readonly items          = signal<StatementReviewItem[]>([]);
  readonly discardedCount = signal(0);
  readonly errorMessage   = signal<string | null>(null);
  readonly result         = signal<ConfirmStatementImportResult | null>(null);
  readonly loading        = signal(false);
  readonly saving         = signal(false);

  ngOnInit(): void {
    this.api.getCategories().subscribe({
      next: cats => this.categories.set(cats),
      error: () => this.errorMessage.set('Não foi possível carregar as categorias.'),
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (!file.name.toLowerCase().endsWith('.pdf')) {
      this.reject('Apenas arquivos .pdf são aceitos.');
      return;
    }
    if (file.size > MAX_FILE_BYTES) {
      this.reject('O arquivo excede o limite de 10 MB.');
      return;
    }
    this.errorMessage.set(null);
    this.selectedFile.set(file);
  }

  private reject(message: string): void {
    this.selectedFile.set(null);
    this.errorMessage.set(message);
  }

  preview(): void {
    const file = this.selectedFile();
    if (!file) return;

    this.loading.set(true);
    this.errorMessage.set(null);
    this.result.set(null);
    this.api.previewStatementImport(file, this.password() || undefined, this.fromDate() || undefined).subscribe({
      next: res => {
        this.items.set(res.items.map(i => ({
          date:                     i.date,
          description:              i.description,
          amount:                   i.amount,
          kind:                     i.kind,
          categoryId:               i.suggestedCategoryId,
          sourceType:               'Personal',
          isLikelyInternalTransfer: i.isLikelyInternalTransfer,
          isLikelyDuplicate:        i.isLikelyDuplicate,
        })));
        this.discardedCount.set(res.discardedByDateCount);
        this.loading.set(false);
      },
      error: err => {
        this.items.set([]);
        this.errorMessage.set(err?.error?.message ?? 'Erro ao processar o extrato.');
        this.loading.set(false);
      },
    });
  }

  removeItem(index: number): void {
    this.items.update(list => list.filter((_, i) => i !== index));
  }

  updateItem(index: number, patch: Partial<StatementReviewItem>): void {
    this.items.update(list => list.map((it, i) => (i === index ? { ...it, ...patch } : it)));
  }

  isItemValid(item: StatementReviewItem): boolean {
    const desc = item.description.trim();
    return desc.length > 0
      && desc.length <= MAX_DESCRIPTION
      && item.amount > 0
      && /^\d{4}-\d{2}-\d{2}$/.test(item.date)
      && item.date <= this.todayIso()
      && (item.kind === 'Income' || !!item.categoryId);
  }

  canSave(): boolean {
    const list = this.items();
    return list.length > 0 && !this.saving() && list.every(i => this.isItemValid(i));
  }

  save(): void {
    if (!this.canSave()) return;

    this.saving.set(true);
    this.errorMessage.set(null);
    const request = {
      items: this.items().map(i => ({
        date:        i.date,
        description: i.description.trim(),
        amount:      i.amount,
        kind:        i.kind,
        categoryId:  i.kind === 'Expense' ? i.categoryId : null,
        sourceType:  i.sourceType,
      })),
    };
    this.api.confirmStatementImport(request).subscribe({
      next: res => {
        this.result.set(res);
        this.saving.set(false);
      },
      error: err => {
        this.errorMessage.set(err?.error?.message ?? 'Erro ao salvar o extrato.');
        this.saving.set(false);
      },
    });
  }

  // Data local yyyy-MM-dd (sem toISOString, que converte para UTC)
  private todayIso(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
}
