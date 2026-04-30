import { Component, inject, signal, computed, OnInit, input, ViewChild, ElementRef } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { trigger, style, animate, transition } from '@angular/animations';
import { ApiService } from '../../../../core/services/api.service';
import { HeaderComponent } from '../../../../shared/components/header/header.component';
import { SonicModalComponent } from '../../../../shared/components/modal/sonic-modal/sonic-modal.component';
import { PaginationComponent } from '../../../../shared/components/pagination/pagination.component';
import { CurrencyBrlPipe } from '../../../../shared/pipes/currency-brl.pipe';
import { SonicRingBurstComponent } from '../../../../shared/components/sonic-ring-burst/sonic-ring-burst.component';
import {
  ExpenseResponse, PeriodResponse, CategoryResponse,
  MONTH_NAMES, PAYMENT_STATUS_LABELS, FORTNIGHT_TYPE_LABELS, SOURCE_TYPE_LABELS,
  PaymentStatus, SourceType, FortnightType, ExpenseOrderItem
} from '../../../../core/models/models';

type SortCol = 'description' | 'category' | 'sourceType' | 'fortnightType' | 'dueDate' | 'amount' | 'paymentStatus';

@Component({
  selector: 'app-expenses',
  standalone: true,
  imports: [HeaderComponent, CurrencyBrlPipe, ReactiveFormsModule, SonicModalComponent, PaginationComponent, SonicRingBurstComponent],
  templateUrl: './expenses.component.html',
  styleUrls: ['./expenses.component.css'],
  animations: [
    trigger('backdropAnim', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('200ms ease', style({ opacity: 1 }))
      ]),
      transition(':leave', [
        animate('180ms ease', style({ opacity: 0 }))
      ])
    ]),
    trigger('modalAnim', [
      transition(':enter', [
        style({ opacity: 0, transform: 'scale(0.88) translateY(-16px)' }),
        animate('260ms cubic-bezier(0.34, 1.56, 0.64, 1)',
          style({ opacity: 1, transform: 'scale(1) translateY(0)' }))
      ]),
      transition(':leave', [
        animate('180ms ease-in',
          style({ opacity: 0, transform: 'scale(0.93) translateY(10px)' }))
      ])
    ])
  ],
})
export class ExpensesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb  = inject(FormBuilder);

  readonly PaymentStatus = PaymentStatus;
  readonly FortnightType = FortnightType;
  readonly SourceType    = SourceType;

  readonly periods    = signal<PeriodResponse[]>([]);
  readonly categories = signal<CategoryResponse[]>([]);
  readonly touchedCatId = signal<string | null>(null);

  // Dados vindos da API (página atual)
  readonly expenses    = signal<ExpenseResponse[]>([]);
  readonly totalCount  = signal(0);
  readonly currentPage = signal(1);
  readonly pageSize    = signal(20);

  // Filtros externos (server-side)
  readonly filterDescription   = signal('');
  readonly filterCategoryId    = signal('');
  readonly filterStatus        = signal<PaymentStatus | null>(null);
  readonly filterFortnightType = signal<FortnightType | null>(null);
  readonly filterSourceType    = signal<SourceType | null>(null);

  // Ordenação client-side (página atual)
  readonly sortCol = signal<SortCol | null>(null);
  readonly sortDir = signal<'asc' | 'desc'>('asc');

  readonly loadingList = signal(false);
  readonly saving      = signal(false);
  readonly apiError    = signal<string | null>(null);
  readonly modalOpen   = signal(false);
  readonly modalMode   = signal<'create' | 'edit'>('create');

  private editingId: string | null = null;
  selectedPeriodId: string | null = null;

  // Debounce para busca por descrição
  private descriptionDebounce: ReturnType<typeof setTimeout> | null = null;

  // Seleção em lote
  readonly selectedExpenseIds = signal<string[]>([]);
  readonly allSelected = computed(() => {
    const displayed = this.displayedExpenses();
    return displayed.length > 0 && displayed.every(e => this.selectedExpenseIds().includes(e.id));
  });

  // Ring animation — campo de valor
  private _ringId = 0;
  readonly rings    = signal<{ id: number; x: number }[]>([]);
  readonly sparkles = signal<{ id: number; x: number; y: string }[]>([]);

  // Sonic ring burst — animação de pagamento
  private _burstId = 0;
  readonly bursts = signal<{ id: number; origin: { x: number; y: number } }[]>([]);

  incrementAmount(): void {
    const cur = this.form.get('amount')?.value ?? 0;
    this.form.get('amount')?.setValue(Math.round((cur + 10) * 100) / 100);
    this._spawnRing();
  }

  decrementAmount(): void {
    const cur = this.form.get('amount')?.value ?? 0;
    const next = Math.max(0, Math.round((cur - 10) * 100) / 100);
    this.form.get('amount')?.setValue(next);
    this._spawnRing();
  }

  private _spawnRing(): void {
    const id = ++this._ringId;
    const x  = 20 + Math.random() * 140;
    this.rings.update(r => [...r, { id, x }]);
    this.sparkles.update(s => [
      ...s,
      { id: id * 100 + 1, x: x - 10 + Math.random() * 20, y: '30%' },
      { id: id * 100 + 2, x: x - 20 + Math.random() * 40, y: '60%' },
    ]);
    setTimeout(() => this.rings.update(r => r.filter(ring => ring.id !== id)), 900);
    setTimeout(() => this.sparkles.update(s => s.filter(sp => sp.id !== id * 100 + 1 && sp.id !== id * 100 + 2)), 500);
  }

  // Drag and drop
  private draggedIndex: number | null = null;
  readonly pendingOrders = signal<ExpenseOrderItem[]>([]);
  readonly savingOrder   = signal(false);
  readonly hasPendingOrder = computed(() => this.pendingOrders().length > 0);

  /** Pré-seleção via query param: /expenses?periodId=xxx */
  readonly periodId = input<string>();
  @ViewChild('periodFilterSelect') periodFilterSelect!: ElementRef<HTMLSelectElement>;

  /** Verifica se há algum filtro ativo */
  readonly hasActiveFilters = computed(() =>
    !!this.filterDescription() || !!this.filterCategoryId() ||
    this.filterStatus() != null || this.filterFortnightType() != null ||
    this.filterSourceType() != null);

  /** Aplica ordenação client-side sobre os itens da página atual */
  readonly displayedExpenses = computed<ExpenseResponse[]>(() => {
    const col = this.sortCol();
    const dir = this.sortDir();
    const list = [...this.expenses()];

    if (!col) return list;

    return list.sort((a, b) => {
      let va: string | number;
      let vb: string | number;

      switch (col) {
        case 'description':   va = a.description.toLowerCase();   vb = b.description.toLowerCase();   break;
        case 'category':      va = this.categoryName(a.categoryId); vb = this.categoryName(b.categoryId); break;
        case 'sourceType':    va = a.sourceType;                  vb = b.sourceType;                  break;
        case 'fortnightType': va = a.fortnightType;               vb = b.fortnightType;               break;
        case 'dueDate':       va = a.dueDate;                     vb = b.dueDate;                     break;
        case 'amount':        va = a.amount;                      vb = b.amount;                      break;
        case 'paymentStatus': va = a.paymentStatus;               vb = b.paymentStatus;               break;
        default:              return 0;
      }

      if (va < vb) return dir === 'asc' ? -1 : 1;
      if (va > vb) return dir === 'asc' ?  1 : -1;
      return 0;
    });
  });

  readonly form = this.fb.group({
    periodId:      ['', Validators.required],
    categoryId:    ['', Validators.required],
    description:   ['', Validators.required],
    amount:        [null as number | null, [Validators.required, Validators.min(0.01)]],
    dueDate:       ['', Validators.required],
    sourceType:    [SourceType.Personal],
    fortnightType: [FortnightType.First],
    notes:         [''],
    status:        [PaymentStatus.Pending],
  });

  ngOnInit(): void {
    const preId = this.periodId();
    this.api.getPeriods().subscribe(p => {
      this.periods.set(p);
      if (preId) {
        this.selectedPeriodId = preId;
        this.loadPage();
        setTimeout(() => {
          if (this.periodFilterSelect?.nativeElement)
            this.periodFilterSelect.nativeElement.value = preId;
        }, 0);
      }
    });
    this.api.getCategories().subscribe(c => this.categories.set(c));
  }

  private loadPage(): void {
    if (!this.selectedPeriodId) return;
    this.loadingList.set(true);
    this.api.getExpensesByPeriod(this.selectedPeriodId, {
      pageNumber:    this.currentPage(),
      pageSize:      this.pageSize(),
      description:   this.filterDescription() || undefined,
      categoryId:    this.filterCategoryId()  || undefined,
      paymentStatus: this.filterStatus()        ?? undefined,
      fortnightType: this.filterFortnightType() ?? undefined,
      sourceType:    this.filterSourceType()    ?? undefined,
    }).subscribe({
      next: result => {
        this.expenses.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loadingList.set(false);
      },
      error: () => this.loadingList.set(false)
    });
  }

  openCreateModal(): void {
    this.editingId = null;
    this.modalMode.set('create');
    this.apiError.set(null);
    this.form.reset({
      periodId:      this.selectedPeriodId ?? '',
      sourceType:    SourceType.Personal,
      fortnightType: FortnightType.First,
    });
    this.modalOpen.set(true);
  }

  openEditModal(expense: ExpenseResponse): void {
    this.editingId = expense.id;
    this.modalMode.set('edit');
    this.apiError.set(null);
    this.form.patchValue({
      periodId:      expense.periodId,
      categoryId:    expense.categoryId,
      description:   expense.description,
      amount:        expense.amount,
      dueDate:       expense.dueDate.split('T')[0],
      sourceType:    expense.sourceType,
      fortnightType: expense.fortnightType,
      notes:         expense.notes ?? '',
      status:        expense.paymentStatus,
    });
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.modalOpen.set(false);
    this.editingId = null;
    this.apiError.set(null);
  }

  showError(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  onPeriodFilter(event: Event): void {
    const periodId = (event.target as HTMLSelectElement).value;
    this.selectedPeriodId = periodId || null;
    this.currentPage.set(1);
    this.expenses.set([]);
    this.totalCount.set(0);
    if (periodId) this.loadPage();
  }

  // ── Filtros externos ──────────────────────────────────────────────────────

  onFilterDescriptionChange(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.filterDescription.set(val);
    if (this.descriptionDebounce) clearTimeout(this.descriptionDebounce);
    this.descriptionDebounce = setTimeout(() => {
      this.currentPage.set(1);
      this.loadPage();
    }, 350);
  }

  onFilterCategoryChange(event: Event): void {
    this.filterCategoryId.set((event.target as HTMLSelectElement).value);
    this.currentPage.set(1);
    this.loadPage();
  }

  onFilterStatusChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.filterStatus.set(val ? Number(val) as PaymentStatus : null);
    this.currentPage.set(1);
    this.loadPage();
  }

  onFilterFortnightChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.filterFortnightType.set(val ? Number(val) as FortnightType : null);
    this.currentPage.set(1);
    this.loadPage();
  }

  onFilterSourceTypeChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.filterSourceType.set(val ? Number(val) as SourceType : null);
    this.currentPage.set(1);
    this.loadPage();
  }

  clearFilters(): void {
    this.filterDescription.set('');
    this.filterCategoryId.set('');
    this.filterStatus.set(null);
    this.filterFortnightType.set(null);
    this.filterSourceType.set(null);
    this.currentPage.set(1);
    this.loadPage();
  }

  // ── Paginação ─────────────────────────────────────────────────────────────

  onPageChange(page: number): void {
    this.currentPage.set(page);
    this.loadPage();
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.currentPage.set(1);
    this.loadPage();
  }

  // ── Ordenação client-side ─────────────────────────────────────────────────

  toggleSort(col: SortCol): void {
    if (this.sortCol() === col) {
      this.sortDir.update(d => d === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortCol.set(col);
      this.sortDir.set('asc');
    }
  }

  sortIcon(col: SortCol): string {
    if (this.sortCol() !== col) return '↕';
    return this.sortDir() === 'asc' ? '↑' : '↓';
  }

  // ── CRUD ──────────────────────────────────────────────────────────────────

  onSubmit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    this.saving.set(true);
    this.apiError.set(null);

    const v = this.form.getRawValue();

    if (this.modalMode() === 'create') {
      this.api.createExpense({
        periodId:      v.periodId!,
        categoryId:    v.categoryId!,
        description:   v.description!,
        amount:        v.amount!,
        dueDate:       v.dueDate!,
        sourceType:    Number(v.sourceType) as SourceType,
        fortnightType: Number(v.fortnightType) as FortnightType,
        notes:         v.notes || undefined,
      }).subscribe({
        next: () => {
          this.closeModal();
          this.saving.set(false);
          // Recarrega a página atual para refletir o novo registro
          this.loadPage();
        },
        error: err => {
          this.apiError.set(err.error?.message ?? 'Erro ao salvar despesa.');
          this.saving.set(false);
        }
      });
    } else {
      const id = this.editingId!;
      const sourceType    = Number(v.sourceType)    as SourceType;
      const fortnightType = Number(v.fortnightType) as FortnightType;
      const status        = Number(v.status)        as PaymentStatus;

      this.api.updateExpense(id, {
        categoryId:    v.categoryId!,
        description:   v.description!,
        amount:        v.amount!,
        dueDate:       v.dueDate!,
        sourceType,
        fortnightType,
        notes:         v.notes || undefined,
        status,
      }).subscribe({
        next: () => {
          this.expenses.update(list =>
            list.map(e => e.id === id
              ? { ...e,
                  categoryId:    v.categoryId!,
                  description:   v.description!,
                  amount:        v.amount!,
                  dueDate:       v.dueDate!,
                  sourceType,
                  fortnightType,
                  paymentStatus: status,
                  notes:         v.notes || null }
              : e
            )
          );
          this.closeModal();
          this.saving.set(false);
        },
        error: err => {
          this.apiError.set(err.error?.message ?? 'Erro ao editar despesa.');
          this.saving.set(false);
        }
      });
    }
  }

  markAsPaid(event: MouseEvent, expense: ExpenseResponse): void {
    const btn  = event.currentTarget as HTMLElement;
    const rect = btn.getBoundingClientRect();
    const id   = ++this._burstId;
    this.bursts.update(b => [...b, {
      id,
      origin: { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 },
    }]);

    const today = new Date().toISOString().split('T')[0];
    this.api.markExpenseAsPaid(expense.id, { paymentDate: today }).subscribe({
      next: () => {
        this.expenses.update(list =>
          list.map(e => e.id === expense.id
            ? { ...e, paymentStatus: PaymentStatus.Paid, paymentDate: today }
            : e
          )
        );
      }
    });
  }

  removeBurst(id: number): void {
    this.bursts.update(b => b.filter(x => x.id !== id));
  }

  deleteExpense(id: string): void {
    if (!confirm('Confirma a exclusão desta despesa?')) return;
    this.api.deleteExpense(id).subscribe({
      next: () => {
        this.expenses.update(list => list.filter(e => e.id !== id));
        this.totalCount.update(n => n - 1);
      }
    });
  }

  // ── Seleção em lote ───────────────────────────────────────────────────────

  isSelected(id: string): boolean {
    return this.selectedExpenseIds().includes(id);
  }

  toggleSelect(id: string): void {
    this.selectedExpenseIds.update(ids =>
      ids.includes(id) ? ids.filter(x => x !== id) : [...ids, id]
    );
  }

  toggleSelectAll(): void {
    if (this.allSelected()) {
      this.selectedExpenseIds.set([]);
    } else {
      this.selectedExpenseIds.set(this.displayedExpenses().map(e => e.id));
    }
  }

  batchPay(): void {
    const ids = this.selectedExpenseIds();
    if (!ids.length) return;
    const today = new Date().toISOString().split('T')[0];
    this.api.batchPayExpenses(ids).subscribe({
      next: () => {
        this.expenses.update(list =>
          list.map(e => ids.includes(e.id)
            ? { ...e, paymentStatus: PaymentStatus.Paid, paymentDate: today }
            : e
          )
        );
        this.selectedExpenseIds.set([]);
      }
    });
  }

  batchCancel(): void {
    const ids = this.selectedExpenseIds();
    if (!ids.length) return;
    this.api.batchCancelExpenses(ids).subscribe({
      next: () => {
        this.expenses.update(list =>
          list.map(e => ids.includes(e.id)
            ? { ...e, paymentStatus: PaymentStatus.Cancelled }
            : e
          )
        );
        this.selectedExpenseIds.set([]);
      }
    });
  }

  batchDelete(): void {
    const ids = this.selectedExpenseIds();
    if (!ids.length) return;
    if (!confirm(`Confirma a exclusão de ${ids.length} despesa(s)?`)) return;
    this.api.batchDeleteExpenses(ids).subscribe({
      next: () => {
        this.expenses.update(list => list.filter(e => !ids.includes(e.id)));
        this.totalCount.update(n => n - ids.length);
        this.selectedExpenseIds.set([]);
      }
    });
  }

  // ── Drag and drop ─────────────────────────────────────────────────────────

  onDragStart(index: number): void {
    this.draggedIndex = index;
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
  }

  onDrop(targetIndex: number): void {
    if (this.draggedIndex === null || this.draggedIndex === targetIndex) return;

    this.expenses.update(list => {
      const reordered = [...list];
      const [moved] = reordered.splice(this.draggedIndex!, 1);
      reordered.splice(targetIndex, 0, moved);
      return reordered;
    });

    // Registra todos os items da lista atual com a nova ordem
    const newOrders: ExpenseOrderItem[] = this.expenses().map((e, i) => ({ expenseId: e.id, order: i }));
    this.pendingOrders.set(newOrders);
    this.draggedIndex = null;
  }

  saveOrder(): void {
    const items = this.pendingOrders();
    if (!items.length) return;
    this.savingOrder.set(true);
    this.api.saveExpenseOrder(items).subscribe({
      next: () => {
        this.pendingOrders.set([]);
        this.savingOrder.set(false);
      },
      error: () => this.savingOrder.set(false)
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  categoryName(categoryId: string): string {
    return this.categories().find(c => c.id === categoryId)?.name ?? '—';
  }

  categoryColor(categoryId: string): string {
    return this.categories().find(c => c.id === categoryId)?.color ?? '#c8bfaf';
  }

  categoryIcon(categoryId: string): string | null {
    const icon = this.categories().find(c => c.id === categoryId)?.icon ?? null;
    return icon ? `data:image/png;base64,${icon}` : null;
  }

  statusLabel(status: PaymentStatus): string     { return PAYMENT_STATUS_LABELS[status]; }
  sourceLabel(type: SourceType): string           { return SOURCE_TYPE_LABELS[type];     }
  fortnightLabel(type: FortnightType): string     { return FORTNIGHT_TYPE_LABELS[type];  }
  monthName(month: number): string               { return MONTH_NAMES[month - 1].substring(0, 3); }
  formatDate(d: string): string                  { const [y, m, day] = d.substring(0, 10).split('-').map(Number); return new Date(y, m - 1, day).toLocaleDateString('pt-BR'); }

  statusBadgeClass(status: PaymentStatus): string {
    const map: Record<PaymentStatus, string> = {
      [PaymentStatus.Pending]:   'badge-warning',
      [PaymentStatus.Paid]:      'badge-success',
      [PaymentStatus.Cancelled]: 'badge-neutral',
      [PaymentStatus.Partial]:   'badge-info',
    };
    return map[status] ?? 'badge-neutral';
  }
}
