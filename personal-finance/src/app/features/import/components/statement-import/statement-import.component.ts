import { Component, DestroyRef, computed, inject, signal, OnInit } from '@angular/core';
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
const PROGRESS_CEILING = 90;
const TICK_MS = 200;

export type StatementImportState = 'empty' | 'proc' | 'preview' | 'done' | 'err';

@Component({
  selector: 'app-statement-import',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  templateUrl: './statement-import.component.html',
  styleUrls: ['./statement-import.component.css'],
})
export class StatementImportComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly destroyRef = inject(DestroyRef);
  private tickerId: ReturnType<typeof setInterval> | null = null;

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
  readonly previewed      = signal(false);
  readonly isDragging     = signal(false);

  // Progresso cosmético: o back-end é request/response único, sem progresso real
  readonly simulatedProgress = signal(0);

  // Precedência: proc > done > err > preview > empty
  readonly state = computed<StatementImportState>(() => {
    if (this.loading() || this.saving()) return 'proc';
    if (this.result()) return 'done';
    if (this.errorMessage() && this.items().length === 0) return 'err';
    if (this.items().length > 0) return 'preview';
    return 'empty';
  });

  // Cards de resumo: derivam de items() e fromDate()
  readonly expenseItems = computed(() => this.items().filter(i => i.kind === 'Expense'));
  readonly incomeItems  = computed(() => this.items().filter(i => i.kind === 'Income'));
  readonly expenseSum   = computed(() => this.expenseItems().reduce((s, i) => s + i.amount, 0));
  readonly incomeSum    = computed(() => this.incomeItems().reduce((s, i) => s + i.amount, 0));

  private static isPending(i: StatementReviewItem): boolean {
    return i.kind === 'Expense' && !i.categoryId;
  }
  private static hasWarning(i: StatementReviewItem): boolean {
    return i.isLikelyInternalTransfer || i.isLikelyDuplicate;
  }

  readonly pendingCount = computed(() =>
    this.items().filter(StatementImportComponent.isPending).length);
  readonly warnCount = computed(() =>
    this.items().filter(StatementImportComponent.hasWarning).length);
  // União de pendentes e com aviso (item nas duas condições conta uma vez)
  readonly attentionCount = computed(() =>
    this.items().filter(i =>
      StatementImportComponent.isPending(i) || StatementImportComponent.hasWarning(i)).length);

  // yyyy-MM-dd -> dd/MM/yyyy sem passar por Date (evita deslocamento de fuso)
  readonly discardedSinceLabel = computed(() => {
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(this.fromDate());
    return m ? `${m[3]}/${m[2]}/${m[1]}` : '';
  });

  constructor() {
    this.destroyRef.onDestroy(() => this.clearTicker());
  }

  ngOnInit(): void {
    this.api.getCategories().subscribe({
      next: cats => this.categories.set(cats),
      error: () => this.errorMessage.set('Não foi possível carregar as categorias.'),
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.acceptFile(input.files?.[0]);
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(false);
    this.acceptFile(event.dataTransfer?.files?.[0]);
  }

  // Validação comum entre seleção por input e drag-and-drop
  private acceptFile(file: File | undefined): void {
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
    this.startTicker();
    this.errorMessage.set(null);
    this.result.set(null);
    this.previewed.set(false);
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
        this.previewed.set(true);
        this.finishTicker();
        this.loading.set(false);
      },
      error: err => {
        this.items.set([]);
        this.errorMessage.set(err?.error?.message ?? 'Erro ao processar o extrato.');
        this.finishTicker();
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
    this.startTicker();
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
        this.items.set([]);
        this.selectedFile.set(null);
        this.finishTicker();
        this.saving.set(false);
      },
      error: err => {
        this.errorMessage.set(err?.error?.message ?? 'Erro ao salvar o extrato.');
        this.finishTicker();
        this.saving.set(false);
      },
    });
  }

  // Zera e inicia o ticker; cancela o anterior para nunca haver dois ativos
  private startTicker(): void {
    this.clearTicker();
    this.simulatedProgress.set(0);
    this.tickerId = setInterval(() => {
      this.simulatedProgress.update(p => Math.min(PROGRESS_CEILING, p + (PROGRESS_CEILING - p) * 0.08 + 0.5));
    }, TICK_MS);
  }

  // Fim da request (sucesso ou erro): para o ticker e salta para 100%
  private finishTicker(): void {
    this.clearTicker();
    this.simulatedProgress.set(100);
  }

  private clearTicker(): void {
    if (this.tickerId !== null) {
      clearInterval(this.tickerId);
      this.tickerId = null;
    }
  }

  // Data local yyyy-MM-dd (sem toISOString, que converte para UTC)
  private todayIso(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }
}
