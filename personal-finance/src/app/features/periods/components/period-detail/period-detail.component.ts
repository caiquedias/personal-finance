import { Component, inject, signal, computed, OnInit, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { animate, style, transition, trigger } from '@angular/animations';
import { ApiService } from '../../../../core/services/api.service';
import { HeaderComponent } from '../../../../shared/components/header/header.component';
import { CurrencyBrlPipe } from '../../../../shared/pipes/currency-brl.pipe';
import { MarioModalComponent } from '../../../../shared/components/modal/mario-modal/mario-modal.component';
import { PaginationComponent } from '../../../../shared/components/pagination/pagination.component';
import { FilterModalComponent } from '../../../../shared/components/filter-modal/filter-modal.component';
import { FilterFieldConfig } from '../../../../shared/components/filter-modal/filter-field-config';
import {
  PeriodSummary, ExpenseResponse, IncomeResponse, CategoryResponse,
  MONTH_NAMES, PAYMENT_STATUS_LABELS, SOURCE_TYPE_LABELS,
  FORTNIGHT_TYPE_LABELS, PaymentStatus, FortnightType, SourceType
} from '../../../../core/models/models';

type MarioTarget = 'receitas' | 'despesas' | 'pago' | 'apagar' | 'saldo' | 'saldoAposPagamento';
type ExpSortCol = 'description' | 'category' | 'fortnightType' | 'dueDate' | 'amount' | 'paymentStatus' | 'sourceType';
type IncSortCol = 'description' | 'fortnightType' | 'receivedAt' | 'amount';

@Component({
  selector: 'app-period-detail',
  standalone: true,
  imports: [HeaderComponent, CurrencyBrlPipe, RouterLink, MarioModalComponent, PaginationComponent, FilterModalComponent],
  templateUrl: './period-detail.component.html',
  styleUrls: ['./period-detail.component.css'],
  animations: [
    trigger('backdropAnim', [
      transition(':enter', [style({ opacity: 0 }), animate('200ms ease', style({ opacity: 1 }))]),
      transition(':leave', [animate('180ms ease', style({ opacity: 0 }))])
    ]),
    trigger('modalAnim', [
      transition(':enter', [
        style({ opacity: 0, transform: 'scale(0.88) translateY(-16px)' }),
        animate('260ms cubic-bezier(0.34,1.56,0.64,1)',
          style({ opacity: 1, transform: 'scale(1) translateY(0)' }))
      ]),
      transition(':leave', [
        animate('180ms ease-in',
          style({ opacity: 0, transform: 'scale(0.93) translateY(10px)' }))
      ])
    ])
  ],
})
export class PeriodDetailComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly id = input.required<string>();

  readonly loading    = signal(true);
  readonly activeTab  = signal<'expenses' | 'incomes'>('expenses');
  readonly summary    = signal<PeriodSummary | null>(null);
  readonly categories = signal<CategoryResponse[]>([]);

  readonly touchedCatId = signal<string | null>(null);

  // Mario modal
  readonly marioOpen    = signal(false);
  readonly marioTitle   = signal('');
  readonly marioContent = signal('');

  // ── Despesas: paginação, filtros e ordenação ──────────────────────────────
  readonly expenses        = signal<ExpenseResponse[]>([]);
  readonly expPage         = signal(1);
  readonly expPageSize     = signal(20);
  readonly expTotal        = signal(0);
  readonly expLoadingList  = signal(false);
  // Filtros compartilhados entre despesas e receitas
  readonly filterDesc      = signal('');
  readonly filterFortnight = signal<FortnightType | null>(null);

  readonly expCategoryId   = signal('');
  readonly expStatus       = signal<PaymentStatus | null>(null);
  readonly expSourceType   = signal<SourceType | null>(null);
  readonly expSortCol      = signal<ExpSortCol | null>(null);
  readonly expSortDir      = signal<'asc' | 'desc'>('asc');
  readonly expFilterOpen   = signal(false);

  readonly allFilteredExpenses = signal<ExpenseResponse[]>([]);
  readonly allFilteredIncomes  = signal<IncomeResponse[]>([]);

  readonly expHasFilters = computed(() =>
    !!this.filterDesc() || !!this.expCategoryId() ||
    this.expStatus() != null || this.filterFortnight() != null ||
    this.expSourceType() != null);

  readonly expFilterFields = computed<FilterFieldConfig[]>(() => [
    { key: 'description',   label: 'Descrição', type: 'text',   value: this.filterDesc() },
    { key: 'categoryId',    label: 'Categoria',  type: 'select', value: this.expCategoryId(),
      options: [{ value: '', label: 'Todas' }, ...this.categories().map(c => ({ value: c.id, label: c.name }))] },
    { key: 'paymentStatus', label: 'Status',     type: 'select', value: this.expStatus() ?? '',
      options: [{ value: '', label: 'Todos' }, { value: PaymentStatus.Pending, label: 'Pendente' },
        { value: PaymentStatus.Paid, label: 'Pago' }, { value: PaymentStatus.Partial, label: 'Parcial' },
        { value: PaymentStatus.Cancelled, label: 'Cancelado' }] },
    { key: 'fortnightType', label: 'Quinzena',   type: 'select', value: this.filterFortnight() ?? '',
      options: [{ value: '', label: 'Ambas' }, { value: FortnightType.First, label: '1ª Quinzena' },
        { value: FortnightType.Second, label: '2ª Quinzena' }] },
    { key: 'sourceType',    label: 'Fonte',      type: 'select', value: this.expSourceType() ?? '',
      options: [{ value: '', label: 'Todas' }, { value: SourceType.Personal, label: 'Própria' },
        { value: SourceType.Parental, label: 'Parental' }] },
  ]);

  readonly displayedExpenses = computed<ExpenseResponse[]>(() => {
    const col = this.expSortCol();
    const dir = this.expSortDir();
    const list = [...this.expenses()];
    if (!col) return list;
    return list.sort((a, b) => {
      let va: string | number;
      let vb: string | number;
      switch (col) {
        case 'description':   va = a.description.toLowerCase();     vb = b.description.toLowerCase();     break;
        case 'category':      va = this.categoryName(a.categoryId); vb = this.categoryName(b.categoryId); break;
        case 'fortnightType': va = a.fortnightType;                 vb = b.fortnightType;                 break;
        case 'dueDate':       va = a.dueDate;                       vb = b.dueDate;                       break;
        case 'amount':        va = a.amount;                        vb = b.amount;                        break;
        case 'paymentStatus': va = a.paymentStatus;                 vb = b.paymentStatus;                 break;
        case 'sourceType':    va = a.sourceType;                    vb = b.sourceType;                    break;
        default:              return 0;
      }
      if (va < vb) return dir === 'asc' ? -1 : 1;
      if (va > vb) return dir === 'asc' ?  1 : -1;
      return 0;
    });
  });

  // ── Receitas: paginação, filtros e ordenação ──────────────────────────────
  readonly incomes        = signal<IncomeResponse[]>([]);
  readonly incPage        = signal(1);
  readonly incPageSize    = signal(20);
  readonly incTotal       = signal(0);
  readonly incLoadingList = signal(false);
  readonly incSortCol     = signal<IncSortCol | null>(null);
  readonly incSortDir     = signal<'asc' | 'desc'>('asc');
  readonly incFilterOpen  = signal(false);

  readonly incHasFilters = computed(() =>
    !!this.filterDesc() || this.filterFortnight() != null);

  readonly incFilterFields = computed<FilterFieldConfig[]>(() => [
    { key: 'description',   label: 'Descrição', type: 'text',   value: this.filterDesc() },
    { key: 'fortnightType', label: 'Quinzena',   type: 'select', value: this.filterFortnight() ?? '',
      options: [{ value: '', label: 'Ambas' }, { value: FortnightType.First, label: '1ª Quinzena' },
        { value: FortnightType.Second, label: '2ª Quinzena' }] },
  ]);

  readonly displayedIncomes = computed<IncomeResponse[]>(() => {
    const col = this.incSortCol();
    const dir = this.incSortDir();
    const list = [...this.incomes()];
    if (!col) return list;
    return list.sort((a, b) => {
      let va: string | number;
      let vb: string | number;
      switch (col) {
        case 'description':   va = a.description.toLowerCase(); vb = b.description.toLowerCase(); break;
        case 'fortnightType': va = a.fortnightType;             vb = b.fortnightType;             break;
        case 'receivedAt':    va = a.receivedAt;                vb = b.receivedAt;                break;
        case 'amount':        va = a.amount;                    vb = b.amount;                    break;
        default:              return 0;
      }
      if (va < vb) return dir === 'asc' ? -1 : 1;
      if (va > vb) return dir === 'asc' ?  1 : -1;
      return 0;
    });
  });

  readonly headerTitle = computed(() => {
    const s = this.summary();
    if (!s) return 'Período';
    return `${MONTH_NAMES[s.month - 1]} ${s.year}`;
  });

  readonly monthAbbr = computed(() => {
    const s = this.summary();
    if (!s) return '';
    return MONTH_NAMES[s.month - 1].slice(0, 3);
  });

  readonly year = computed(() => this.summary()?.year ?? null);

  readonly kpiIncomeTotal = computed(() =>
    this.incHasFilters()
      ? this.allFilteredIncomes().reduce((sum, i) => sum + i.amount, 0)
      : (this.summary()?.totalIncome ?? 0));

  readonly kpiExpenseTotal = computed(() =>
    this.expHasFilters()
      ? this.allFilteredExpenses().reduce((sum, e) => sum + e.amount, 0)
      : (this.summary()?.totalExpense ?? 0));

  readonly kpiPaid = computed(() =>
    this.expHasFilters()
      ? this.allFilteredExpenses()
          .filter(e => e.paymentStatus === PaymentStatus.Paid)
          .reduce((sum, e) => sum + e.amount, 0)
      : (this.summary()?.totalPaid ?? 0));

  readonly kpiOwed = computed(() =>
    this.expHasFilters()
      ? this.allFilteredExpenses()
          .filter(e => e.paymentStatus === PaymentStatus.Pending || e.paymentStatus === PaymentStatus.Partial)
          .reduce((sum, e) => sum + e.amount, 0)
      : (this.summary()?.totalOwed ?? 0));

  readonly kpiBalance = computed(() => {
    if (this.expHasFilters() || this.incHasFilters())
      return this.kpiIncomeTotal() - this.kpiExpenseTotal();
    return this.summary()?.balance ?? 0;
  });

  readonly kpiBalanceAfterPayment = computed(() =>
    this.kpiIncomeTotal() - this.kpiExpenseTotal());

  // Exposição de enums para o template
  readonly PaymentStatus  = PaymentStatus;
  readonly FortnightType  = FortnightType;
  readonly SourceType     = SourceType;

  ngOnInit(): void {
    const periodId = this.id();
    Promise.all([
      this.api.getPeriodSummary(periodId).toPromise(),
      this.api.getCategories().toPromise(),
    ]).then(([summary, categories]) => {
      this.summary.set(summary ?? null);
      this.categories.set(categories ?? []);
      this.loading.set(false);
    }).catch(() => this.loading.set(false));

    this.loadExpenses();
    this.loadIncomes();
  }

  // ── Despesas ─────────────────────────────────────────────────────────────

  private loadAllFilteredExpenses(): void {
    if (!this.expHasFilters()) { this.allFilteredExpenses.set([]); return; }
    this.api.getExpensesByPeriod(this.id(), {
      pageNumber:    1,
      pageSize:      9999,
      description:   this.filterDesc()      || undefined,
      categoryId:    this.expCategoryId()   || undefined,
      paymentStatus: this.expStatus()        ?? undefined,
      fortnightType: this.filterFortnight()  ?? undefined,
      sourceType:    this.expSourceType()    ?? undefined,
    }).subscribe({ next: r => this.allFilteredExpenses.set(r.items) });
  }

  private loadAllFilteredIncomes(): void {
    if (!this.incHasFilters()) { this.allFilteredIncomes.set([]); return; }
    this.api.getIncomesByPeriod(this.id(), {
      pageNumber:    1,
      pageSize:      9999,
      description:   this.filterDesc()      || undefined,
      fortnightType: this.filterFortnight()  ?? undefined,
    }).subscribe({ next: r => this.allFilteredIncomes.set(r.items) });
  }

  private loadExpenses(): void {
    this.expLoadingList.set(true);
    this.api.getExpensesByPeriod(this.id(), {
      pageNumber:    this.expPage(),
      pageSize:      this.expPageSize(),
      description:   this.filterDesc()      || undefined,
      categoryId:    this.expCategoryId()   || undefined,
      paymentStatus: this.expStatus()        ?? undefined,
      fortnightType: this.filterFortnight()  ?? undefined,
      sourceType:    this.expSourceType()    ?? undefined,
    }).subscribe({
      next: result => {
        this.expenses.set(result.items);
        this.expTotal.set(result.totalCount);
        this.expLoadingList.set(false);
      },
      error: () => this.expLoadingList.set(false),
    });
  }

  onExpFilterApply(values: Record<string, unknown>): void {
    this.filterDesc.set((values['description'] as string) ?? '');
    this.expCategoryId.set((values['categoryId'] as string) ?? '');
    const status = values['paymentStatus'] as string;
    this.expStatus.set(status ? Number(status) as PaymentStatus : null);
    const fortnight = values['fortnightType'] as string;
    this.filterFortnight.set(fortnight ? Number(fortnight) as FortnightType : null);
    const source = values['sourceType'] as string;
    this.expSourceType.set(source ? Number(source) as SourceType : null);
    this.expFilterOpen.set(false);
    this.expPage.set(1);
    this.incPage.set(1);
    this.loadExpenses();
    this.loadAllFilteredExpenses();
    this.loadIncomes();
    this.loadAllFilteredIncomes();
  }

  onExpFilterClear(): void {
    this.expFilterOpen.set(false);
    this.clearExpFilters();
  }

  clearExpFilters(): void {
    this.filterDesc.set('');
    this.filterFortnight.set(null);
    this.expCategoryId.set('');
    this.expStatus.set(null);
    this.expSourceType.set(null);
    this.expPage.set(1);
    this.incPage.set(1);
    this.allFilteredExpenses.set([]);
    this.allFilteredIncomes.set([]);
    this.loadExpenses();
    this.loadIncomes();
  }

  onExpPageChange(page: number): void { this.expPage.set(page); this.loadExpenses(); }
  onExpPageSizeChange(size: number): void { this.expPageSize.set(size); this.expPage.set(1); this.loadExpenses(); }

  toggleExpSort(col: ExpSortCol): void {
    if (this.expSortCol() === col) { this.expSortDir.update(d => d === 'asc' ? 'desc' : 'asc'); }
    else { this.expSortCol.set(col); this.expSortDir.set('asc'); }
  }

  expSortIcon(col: ExpSortCol): string {
    if (this.expSortCol() !== col) return '↕';
    return this.expSortDir() === 'asc' ? '↑' : '↓';
  }

  // ── Receitas ──────────────────────────────────────────────────────────────

  private loadIncomes(): void {
    this.incLoadingList.set(true);
    this.api.getIncomesByPeriod(this.id(), {
      pageNumber:    this.incPage(),
      pageSize:      this.incPageSize(),
      description:   this.filterDesc()      || undefined,
      fortnightType: this.filterFortnight()  ?? undefined,
    }).subscribe({
      next: result => {
        this.incomes.set(result.items);
        this.incTotal.set(result.totalCount);
        this.incLoadingList.set(false);
      },
      error: () => this.incLoadingList.set(false),
    });
  }

  onIncFilterApply(values: Record<string, unknown>): void {
    this.filterDesc.set((values['description'] as string) ?? '');
    const fortnight = values['fortnightType'] as string;
    this.filterFortnight.set(fortnight ? Number(fortnight) as FortnightType : null);
    this.incFilterOpen.set(false);
    this.incPage.set(1);
    this.expPage.set(1);
    this.loadIncomes();
    this.loadAllFilteredIncomes();
    this.loadExpenses();
    this.loadAllFilteredExpenses();
  }

  onIncFilterClear(): void {
    this.incFilterOpen.set(false);
    this.clearIncFilters();
  }

  clearIncFilters(): void {
    this.filterDesc.set('');
    this.filterFortnight.set(null);
    this.incPage.set(1);
    this.expPage.set(1);
    this.allFilteredIncomes.set([]);
    this.allFilteredExpenses.set([]);
    this.loadIncomes();
    this.loadExpenses();
  }

  onIncPageChange(page: number): void { this.incPage.set(page); this.loadIncomes(); }
  onIncPageSizeChange(size: number): void { this.incPageSize.set(size); this.incPage.set(1); this.loadIncomes(); }

  toggleIncSort(col: IncSortCol): void {
    if (this.incSortCol() === col) { this.incSortDir.update(d => d === 'asc' ? 'desc' : 'asc'); }
    else { this.incSortCol.set(col); this.incSortDir.set('asc'); }
  }

  incSortIcon(col: IncSortCol): string {
    if (this.incSortCol() !== col) return '↕';
    return this.incSortDir() === 'asc' ? '↑' : '↓';
  }

  // ── Mario modal ───────────────────────────────────────────────────────────

  openMario(target: MarioTarget): void {
    const s = this.summary()!;
    const exps = this.expenses();
    let title = '';
    let lines: string[] = [];

    switch (target) {
      case 'receitas': {
        this.api.getIncomesByPeriod(this.id(), { pageNumber: 1, pageSize: 1000 })
          .subscribe(result => {
            const receitas = result.items;
            if (receitas.length === 0) {
              lines = ['Nenhuma receita\nregistrada neste periodo.'];
            } else {
              lines = receitas.map(i => `• ${i.description}\n  ${this.fmt(i.amount)}`);
              lines.push('', `TOTAL: ${this.fmt(s.totalIncome)}`);
            }
            this.marioTitle.set('RECEITAS DO PERIODO');
            this.marioContent.set(lines.join('\n'));
            this.marioOpen.set(true);
          });
        return;
      }
      case 'despesas': {
        this.api.getExpensesByPeriod(this.id(), { pageNumber: 1, pageSize: 1000 })
          .subscribe(result => {
            const despesas = result.items;
            if (despesas.length === 0) {
              lines = ['Nenhuma despesa\nregistrada neste periodo.'];
            } else {
              lines = despesas.map(e => `• ${e.description}\n  ${this.fmt(e.amount)}`);
              lines.push('', `TOTAL: ${this.fmt(s.totalExpense)}`);
            }
            this.marioTitle.set('DESPESAS DO PERIODO');
            this.marioContent.set(lines.join('\n'));
            this.marioOpen.set(true);
          });
        return;
      }
      case 'pago': {
        this.api.getExpensesByPeriod(this.id(), { pageNumber: 1, pageSize: 1000, paymentStatus: PaymentStatus.Paid })
          .subscribe(result => {
            const pagas = result.items;
            if (pagas.length === 0) {
              lines = ['Nenhuma despesa\npaga neste periodo.'];
            } else {
              lines = pagas.map(e => `• ${e.description}\n  ${this.fmt(e.amount)}`);
              lines.push('', `TOTAL PAGO: ${this.fmt(s.totalPaid)}`);
            }
            this.marioTitle.set('DESPESAS PAGAS');
            this.marioContent.set(lines.join('\n'));
            this.marioOpen.set(true);
          });
        return;
      }
      case 'apagar': {
        this.api.getExpensesByPeriod(this.id(), { pageNumber: 1, pageSize: 1000, paymentStatus: PaymentStatus.Pending })
          .subscribe(result => {
            const pendentes = result.items;
            if (pendentes.length === 0) {
              lines = ['Nenhuma despesa\npendente neste periodo.'];
            } else {
              lines = pendentes.map(e => {
                const suffix = e.paymentStatus === PaymentStatus.Partial ? ' (parcial)' : '';
                return `• ${e.description}${suffix}\n  ${this.fmt(e.amount)}`;
              });
              lines.push('', `TOTAL A PAGAR: ${this.fmt(s.totalOwed)}`);
            }
            this.marioTitle.set('DESPESAS A PAGAR');
            this.marioContent.set(lines.join('\n'));
            this.marioOpen.set(true);
          });
        return;
      }
      case 'saldo': {
        title = 'CALCULO DO SALDO';
        lines = [
          `Receitas:`,
          `  ${this.fmt(s.totalIncome)}`,
          '',
          `(-) Despesas:`,
          `  ${this.fmt(s.totalExpense)}`,
          '',
          `(=) Saldo:`,
          `  ${this.fmt(s.balance)}`,
        ];
        break;
      }
      case 'saldoAposPagamento': {
        const val = s.totalIncome - s.totalExpense;
        title = 'SALDO APOS PAGAMENTO';
        lines = [
          `Receitas:`,
          `  ${this.fmt(s.totalIncome)}`,
          '',
          `(-) Total de despesas:`,
          `  ${this.fmt(s.totalExpense)}`,
          '',
          `(=) Saldo apos pagamento:`,
          `  ${this.fmt(val)}`,
        ];
        break;
      }
    }

    this.marioTitle.set(title);
    this.marioContent.set(lines.join('\n'));
    this.marioOpen.set(true);
  }

  private fmt(value: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(value);
  }

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

  statusLabel(status: PaymentStatus): string {
    return PAYMENT_STATUS_LABELS[status] ?? String(status);
  }

  statusBadgeClass(status: PaymentStatus): string {
    const map: Record<PaymentStatus, string> = {
      [PaymentStatus.Pending]:   'badge-warning',
      [PaymentStatus.Paid]:      'badge-success',
      [PaymentStatus.Cancelled]: 'badge-neutral',
      [PaymentStatus.Partial]:   'badge-info',
    };
    return map[status] ?? 'badge-neutral';
  }

  sourceLabel(type: number): string {
    return SOURCE_TYPE_LABELS[type as keyof typeof SOURCE_TYPE_LABELS] ?? String(type);
  }

  fortnightLabel(type: FortnightType): string {
    return FORTNIGHT_TYPE_LABELS[type];
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString('pt-BR');
  }
}
