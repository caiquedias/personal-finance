import { Component, inject, signal, computed, OnInit, input, ViewChild, ElementRef } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { trigger, style, animate, transition } from '@angular/animations';
import { ApiService } from '../../../core/services/api.service';
import { HeaderComponent } from '../../../shared/components/header/header.component';
import { SonicModalComponent } from '../../../shared/components/modal/sonic-modal.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { CurrencyBrlPipe } from '../../../shared/pipes/currency-brl.pipe';
import {
  ExpenseResponse, PeriodResponse, CategoryResponse,
  MONTH_NAMES, PAYMENT_STATUS_LABELS, FORTNIGHT_TYPE_LABELS, SOURCE_TYPE_LABELS,
  PaymentStatus, SourceType, FortnightType
} from '../../../core/models/models';

type SortCol = 'description' | 'category' | 'sourceType' | 'fortnightType' | 'dueDate' | 'amount' | 'paymentStatus';

@Component({
  selector: 'app-expenses',
  standalone: true,
  imports: [HeaderComponent, CurrencyBrlPipe, ReactiveFormsModule, SonicModalComponent, PaginationComponent],
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
  template: `
    <app-header title="Despesas" subtitle="Gerencie seus lançamentos de despesas">
      <button class="btn btn-primary btn-sm" (click)="openCreateModal()">
        + Nova despesa
      </button>
    </app-header>

    <div class="page-content">

      <!-- Filtro por período -->
      <div class="filter-row">
        <label class="field-label">Filtrar por período</label>
        <select #periodFilterSelect class="input" style="width:auto; min-width:180px" (change)="onPeriodFilter($event)">
          <option value="">Selecione um período</option>
          @for (p of periods(); track p.id) {
            <option [value]="p.id">{{ monthName(p.month) }}/{{ p.year }}</option>
          }
        </select>
      </div>

      <!-- Filtros externos (server-side) — visíveis apenas quando há período selecionado -->
      @if (selectedPeriodId) {
        <div class="external-filters card">
          <div class="filters-grid">
            <div class="filter-field">
              <label class="field-label">Descrição</label>
              <input
                class="input input-sm"
                placeholder="Buscar..."
                [value]="filterDescription()"
                (input)="onFilterDescriptionChange($event)"
              />
            </div>

            <div class="filter-field">
              <label class="field-label">Categoria</label>
              <select class="input input-sm" (change)="onFilterCategoryChange($event)">
                <option value="">Todas</option>
                @for (c of categories(); track c.id) {
                  <option [value]="c.id">{{ c.name }}</option>
                }
              </select>
            </div>

            <div class="filter-field">
              <label class="field-label">Fonte</label>
              <select class="input input-sm" (change)="onFilterSourceTypeChange($event)">
                <option value="">Todas</option>
                <option [value]="SourceType.Parental">Parental</option>
                <option [value]="SourceType.Personal">Própria</option>
              </select>
            </div>

            <div class="filter-field">
              <label class="field-label">Status</label>
              <select class="input input-sm" (change)="onFilterStatusChange($event)">
                <option value="">Todos</option>
                <option [value]="PaymentStatus.Pending">Pendente</option>
                <option [value]="PaymentStatus.Paid">Pago</option>
                <option [value]="PaymentStatus.Partial">Parcial</option>
                <option [value]="PaymentStatus.Cancelled">Cancelado</option>
              </select>
            </div>

            <div class="filter-field">
              <label class="field-label">Quinzena</label>
              <select class="input input-sm" (change)="onFilterFortnightChange($event)">
                <option value="">Ambas</option>
                <option [value]="FortnightType.First">1ª Quinzena</option>
                <option [value]="FortnightType.Second">2ª Quinzena</option>
              </select>
            </div>
          </div>

          @if (hasActiveFilters()) {
            <button class="btn-clear-filters" (click)="clearFilters()">Limpar filtros</button>
          }
        </div>
      }

      <!-- Lista -->
      @if (loadingList()) {
        <div class="loading-state"><span class="spinner-lg"></span></div>
      } @else if (!selectedPeriodId) {
        <div class="empty-state">
          <p>Selecione um período para ver as despesas.</p>
        </div>
      } @else if (totalCount() === 0) {
        <div class="empty-state">
          <p>Nenhuma despesa encontrada.</p>
        </div>
      } @else {
        <div class="table-wrap card">
          <table class="table">
            <thead>
              <tr>
                <th class="sortable" (click)="toggleSort('description')">
                  Descrição {{ sortIcon('description') }}
                </th>
                <th class="sortable" (click)="toggleSort('category')">
                  Categoria {{ sortIcon('category') }}
                </th>
                <th class="sortable" (click)="toggleSort('sourceType')">
                  Fonte {{ sortIcon('sourceType') }}
                </th>
                <th class="sortable" (click)="toggleSort('fortnightType')">
                  Quinzena {{ sortIcon('fortnightType') }}
                </th>
                <th class="sortable" (click)="toggleSort('dueDate')">
                  Vencimento {{ sortIcon('dueDate') }}
                </th>
                <th class="sortable" (click)="toggleSort('amount')">
                  Valor {{ sortIcon('amount') }}
                </th>
                <th class="sortable" (click)="toggleSort('paymentStatus')">
                  Status {{ sortIcon('paymentStatus') }}
                </th>
                <th>Ações</th>
              </tr>
            </thead>
            <tbody>
              @for (expense of displayedExpenses(); track expense.id) {
                <tr>
                  <td>
                    <div class="cell-primary">{{ expense.description }}</div>
                    @if (expense.notes) {
                      <div class="cell-secondary">{{ expense.notes }}</div>
                    }
                  </td>
                  <td>
                    <span class="category-dot" [style.background]="categoryColor(expense.categoryId)"></span>
                    {{ categoryName(expense.categoryId) }}
                  </td>
                  <td class="text-muted text-sm">{{ sourceLabel(expense.sourceType) }}</td>
                  <td class="text-muted text-sm">{{ fortnightLabel(expense.fortnightType) }}</td>
                  <td class="text-muted text-sm">{{ formatDate(expense.dueDate) }}</td>
                  <td class="font-semibold">{{ expense.amount | currencyBrl }}</td>
                  <td>
                    <span class="badge" [class]="statusBadgeClass(expense.paymentStatus)">
                      {{ statusLabel(expense.paymentStatus) }}
                    </span>
                  </td>
                  <td>
                    <div class="row-actions">
                      @if (expense.paymentStatus === PaymentStatus.Pending || expense.paymentStatus === PaymentStatus.Partial) {
                        <button class="action-btn action-success" (click)="markAsPaid(expense)" title="Marcar como pago">
                          ✓
                        </button>
                      }
                      <button class="action-btn action-edit" title="Editar" (click)="openEditModal(expense)">
                        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                          <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                          <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                        </svg>
                      </button>
                      <button class="action-btn action-danger" (click)="deleteExpense(expense.id)" title="Excluir">
                        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                          <polyline points="3 6 5 6 21 6"/>
                          <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/>
                          <path d="M10 11v6M14 11v6"/>
                          <path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/>
                        </svg>
                      </button>
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>

          <!-- Paginação -->
          <app-pagination
            [totalCount]="totalCount()"
            [pageSize]="pageSize()"
            [currentPage]="currentPage()"
            (pageChange)="onPageChange($event)"
            (pageSizeChange)="onPageSizeChange($event)"
          />
        </div>
      }
    </div>

    <!-- ── Modal inline com @if — Angular controla :enter/:leave ── -->
    @if (modalOpen()) {
      <!-- Backdrop -->
      <div class="modal-overlay" @backdropAnim (click)="closeModal()"></div>

      <!-- Painel centralizado -->
      <div class="modal-center" @modalAnim>
        <app-sonic-modal
          [title]="modalMode() === 'create' ? 'Nova Despesa' : 'Editar Despesa'"
          (closed)="closeModal()"
        >
          <form [formGroup]="form" (ngSubmit)="onSubmit()">
            <div class="modal-form-grid">

              @if (modalMode() === 'create') {
                <div class="field">
                  <label class="field-label">Período *</label>
                  <select formControlName="periodId" class="input" [class.error]="showError('periodId')">
                    <option value="">Selecione</option>
                    @for (p of periods(); track p.id) {
                      <option [value]="p.id">{{ monthName(p.month) }}/{{ p.year }}</option>
                    }
                  </select>
                  @if (showError('periodId')) { <span class="field-error">Obrigatório</span> }
                </div>
              }

              <div class="field">
                <label class="field-label">Categoria *</label>
                <select formControlName="categoryId" class="input" [class.error]="showError('categoryId')">
                  <option value="">Selecione</option>
                  @for (c of categories(); track c.id) {
                    <option [value]="c.id">{{ c.name }}</option>
                  }
                </select>
                @if (showError('categoryId')) { <span class="field-error">Obrigatório</span> }
              </div>

              <div class="field field-full">
                <label class="field-label">Descrição *</label>
                <input
                  formControlName="description"
                  class="input"
                  [class.error]="showError('description')"
                  placeholder="Ex: Aluguel, Internet..."
                />
                @if (showError('description')) { <span class="field-error">Obrigatório</span> }
              </div>

              <div class="field">
                <label class="field-label">Valor (R$) *</label>
                <input
                  type="number"
                  formControlName="amount"
                  class="input"
                  [class.error]="showError('amount')"
                  placeholder="0,00"
                  step="0.01"
                  min="0.01"
                />
                @if (showError('amount')) { <span class="field-error">Valor inválido</span> }
              </div>

              <div class="field">
                <label class="field-label">Vencimento *</label>
                <input type="date" formControlName="dueDate" class="input" [class.error]="showError('dueDate')" />
                @if (showError('dueDate')) { <span class="field-error">Obrigatório</span> }
              </div>

              <div class="field">
                <label class="field-label">Origem *</label>
                <select formControlName="sourceType" class="input">
                  <option [value]="1">Parental</option>
                  <option [value]="2">Própria</option>
                </select>
              </div>

              <div class="field">
                <label class="field-label">Quinzena *</label>
                <select formControlName="fortnightType" class="input">
                  <option [value]="1">1ª Quinzena</option>
                  <option [value]="2">2ª Quinzena</option>
                </select>
              </div>

              @if (modalMode() === 'edit') {
                <div class="field">
                  <label class="field-label">Status</label>
                  <select formControlName="status" class="input">
                    <option [value]="PaymentStatus.Pending">Pendente</option>
                    <option [value]="PaymentStatus.Paid">Pago</option>
                    <option [value]="PaymentStatus.Partial">Parcial</option>
                    <option [value]="PaymentStatus.Cancelled">Cancelado</option>
                  </select>
                </div>
              }

              <div class="field field-full">
                <label class="field-label">Observações</label>
                <input formControlName="notes" class="input" placeholder="Opcional..." />
              </div>

            </div>

            @if (apiError()) {
              <div class="form-error">{{ apiError() }}</div>
            }

            <div class="modal-footer">
              <button type="button" class="btn btn-secondary" (click)="closeModal()">Cancelar</button>
              <button type="submit" class="btn btn-primary" [disabled]="saving()">
                {{ saving() ? 'Salvando...' : (modalMode() === 'create' ? 'Criar despesa' : 'Salvar') }}
              </button>
            </div>
          </form>
        </app-sonic-modal>
      </div>
    }
  `,
  styles: [`
    .page-content { padding: 28px; display: flex; flex-direction: column; gap: 20px; }

    .filter-row {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    /* ── Filtros externos ── */
    .external-filters {
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }

    .filters-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 12px;
    }

    .filter-field {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .input-sm {
      height: 32px;
      padding: 0 10px;
      font-size: 0.8125rem;
    }

    .btn-clear-filters {
      align-self: flex-start;
      background: none;
      border: none;
      color: var(--color-info);
      font-size: 0.8125rem;
      cursor: pointer;
      padding: 0;
      text-decoration: underline;
    }

    .btn-clear-filters:hover { color: var(--rust); }

    /* ── Tabela ── */
    .table-wrap { overflow-x: auto; }
    .table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
    .table th {
      text-align: left; padding: 12px 16px;
      font-size: 0.75rem; font-weight: 600;
      text-transform: uppercase; letter-spacing: 0.04em;
      color: var(--ink3); border-bottom: 1px solid var(--border);
    }
    .table td {
      padding: 12px 16px;
      border-bottom: 1px solid var(--bg3);
      color: var(--ink2); vertical-align: middle;
    }
    .table tr:last-child td { border-bottom: none; }
    .table tbody tr:hover { background: var(--surface-overlay); }

    .sortable {
      cursor: pointer;
      user-select: none;
      white-space: nowrap;
    }
    .sortable:hover { color: var(--ink); }

    .cell-primary   { color: var(--ink); font-weight: 500; }
    .cell-secondary { font-size: 0.75rem; color: var(--ink3); margin-top: 2px; }
    .category-dot   { display: inline-block; width: 8px; height: 8px; border-radius: 50%; margin-right: 6px; }
    .text-sm        { font-size: 0.8125rem; }
    .text-muted     { color: var(--ink3); }
    .font-semibold  { font-weight: 600; color: var(--ink); }

    .row-actions { display: flex; gap: 4px; }
    .action-btn {
      width: 28px; height: 28px;
      border: 1px solid var(--border);
      background: none; border-radius: var(--radius-sm);
      cursor: pointer; font-size: 12px;
      display: flex; align-items: center; justify-content: center;
      transition: all var(--transition); color: var(--ink3);
    }
    .action-success:hover { background: var(--color-success-bg); border-color: var(--sage2); color: var(--sage2); }
    .action-edit:hover    { background: var(--color-info-bg);    border-color: var(--color-info);  color: var(--color-info);  }
    .action-danger:hover  { background: var(--color-danger-bg);  border-color: var(--rust);        color: var(--rust);        }

    .loading-state, .empty-state {
      display: flex; flex-direction: column;
      align-items: center; gap: 16px; padding: 64px; color: var(--ink3);
    }

    .spinner-lg {
      width: 32px; height: 32px;
      border: 3px solid var(--border);
      border-top-color: var(--sage2);
      border-radius: 50%;
      animation: spin .8s linear infinite; display: block;
    }

    @keyframes spin { to { transform: rotate(360deg); } }

    /* ── Modal overlay e painel ── */
    .modal-overlay {
      position: fixed;
      inset: 0;
      background: rgba(26, 20, 14, 0.55);
      backdrop-filter: blur(3px);
      z-index: 900;
    }

    .modal-center {
      position: fixed;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 901;
      pointer-events: none;
    }

    .modal-center > * {
      pointer-events: all;
    }

    /* ── Modal form ── */
    .modal-form-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }

    .field       { display: flex; flex-direction: column; gap: 6px; }
    .field-full  { grid-column: span 2; }
    .field-label { font-size: 0.875rem; font-weight: 500; color: var(--ink2); }
    .field-error { font-size: 0.8125rem; color: var(--color-danger); }

    .form-error {
      margin-top: 12px; padding: 10px 14px;
      background: var(--color-danger-bg); color: var(--color-danger);
      border-radius: var(--radius); font-size: 0.875rem;
    }

    .modal-footer {
      display: flex; justify-content: flex-end; gap: 10px;
      margin-top: 20px; padding-top: 16px;
      border-top: 1px solid var(--border);
    }
  `]
})
export class ExpensesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb  = inject(FormBuilder);

  readonly PaymentStatus = PaymentStatus;
  readonly FortnightType = FortnightType;
  readonly SourceType    = SourceType;

  readonly periods    = signal<PeriodResponse[]>([]);
  readonly categories = signal<CategoryResponse[]>([]);

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

  markAsPaid(expense: ExpenseResponse): void {
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

  deleteExpense(id: string): void {
    if (!confirm('Confirma a exclusão desta despesa?')) return;
    this.api.deleteExpense(id).subscribe({
      next: () => {
        this.expenses.update(list => list.filter(e => e.id !== id));
        this.totalCount.update(n => n - 1);
      }
    });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  categoryName(categoryId: string): string {
    return this.categories().find(c => c.id === categoryId)?.name ?? '—';
  }

  categoryColor(categoryId: string): string {
    return this.categories().find(c => c.id === categoryId)?.color ?? '#c8bfaf';
  }

  statusLabel(status: PaymentStatus): string     { return PAYMENT_STATUS_LABELS[status]; }
  sourceLabel(type: SourceType): string           { return SOURCE_TYPE_LABELS[type];     }
  fortnightLabel(type: FortnightType): string     { return FORTNIGHT_TYPE_LABELS[type];  }
  monthName(month: number): string               { return MONTH_NAMES[month - 1].substring(0, 3); }
  formatDate(d: string): string                  { return new Date(d).toLocaleDateString('pt-BR'); }

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
