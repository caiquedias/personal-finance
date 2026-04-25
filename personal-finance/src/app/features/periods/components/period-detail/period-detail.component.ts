import { Component, inject, signal, computed, OnInit, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { animate, style, transition, trigger } from '@angular/animations';
import { ApiService } from '../../../../core/services/api.service';
import { HeaderComponent } from '../../../../shared/components/header/header.component';
import { CurrencyBrlPipe } from '../../../../shared/pipes/currency-brl.pipe';
import { MarioModalComponent } from '../../../../shared/components/modal/mario-modal/mario-modal.component';
import { PaginationComponent } from '../../../../shared/components/pagination/pagination.component';
import {
  PeriodSummary, ExpenseResponse, IncomeResponse, CategoryResponse,
  MONTH_NAMES, PAYMENT_STATUS_LABELS, SOURCE_TYPE_LABELS,
  FORTNIGHT_TYPE_LABELS, PaymentStatus, FortnightType, SourceType
} from '../../../../core/models/models';

type MarioTarget = 'receitas' | 'despesas' | 'pago' | 'apagar' | 'saldo';
type ExpSortCol = 'description' | 'category' | 'fortnightType' | 'dueDate' | 'amount' | 'paymentStatus' | 'sourceType';
type IncSortCol = 'description' | 'fortnightType' | 'receivedAt' | 'amount';

@Component({
  selector: 'app-period-detail',
  standalone: true,
  imports: [HeaderComponent, CurrencyBrlPipe, RouterLink, MarioModalComponent, PaginationComponent],
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
  readonly expDesc         = signal('');
  readonly expCategoryId   = signal('');
  readonly expStatus       = signal<PaymentStatus | null>(null);
  readonly expFortnight    = signal<FortnightType | null>(null);
  readonly expSourceType   = signal<SourceType | null>(null);
  readonly expSortCol      = signal<ExpSortCol | null>(null);
  readonly expSortDir      = signal<'asc' | 'desc'>('asc');
  private expDescDebounce: ReturnType<typeof setTimeout> | null = null;

  readonly expHasFilters = computed(() =>
    !!this.expDesc() || !!this.expCategoryId() ||
    this.expStatus() != null || this.expFortnight() != null ||
    this.expSourceType() != null);

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
  readonly incDesc        = signal('');
  readonly incFortnight   = signal<FortnightType | null>(null);
  readonly incSortCol     = signal<IncSortCol | null>(null);
  readonly incSortDir     = signal<'asc' | 'desc'>('asc');
  private incDescDebounce: ReturnType<typeof setTimeout> | null = null;

  readonly incHasFilters = computed(() =>
    !!this.incDesc() || this.incFortnight() != null);

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

  private loadExpenses(): void {
    this.expLoadingList.set(true);
    this.api.getExpensesByPeriod(this.id(), {
      pageNumber:    this.expPage(),
      pageSize:      this.expPageSize(),
      description:   this.expDesc()       || undefined,
      categoryId:    this.expCategoryId() || undefined,
      paymentStatus: this.expStatus()      ?? undefined,
      fortnightType: this.expFortnight()   ?? undefined,
      sourceType:    this.expSourceType()  ?? undefined,
    }).subscribe({
      next: result => {
        this.expenses.set(result.items);
        this.expTotal.set(result.totalCount);
        this.expLoadingList.set(false);
      },
      error: () => this.expLoadingList.set(false),
    });
  }

  onExpDescChange(event: Event): void {
    this.expDesc.set((event.target as HTMLInputElement).value);
    if (this.expDescDebounce) clearTimeout(this.expDescDebounce);
    this.expDescDebounce = setTimeout(() => { this.expPage.set(1); this.loadExpenses(); }, 350);
  }

  onExpCategoryChange(event: Event): void {
    this.expCategoryId.set((event.target as HTMLSelectElement).value);
    this.expPage.set(1);
    this.loadExpenses();
  }

  onExpStatusChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.expStatus.set(val ? Number(val) as PaymentStatus : null);
    this.expPage.set(1);
    this.loadExpenses();
  }

  onExpFortnightChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.expFortnight.set(val ? Number(val) as FortnightType : null);
    this.expPage.set(1);
    this.loadExpenses();
  }

  onExpSourceTypeChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.expSourceType.set(val ? Number(val) as SourceType : null);
    this.expPage.set(1);
    this.loadExpenses();
  }

  clearExpFilters(): void {
    this.expDesc.set('');
    this.expCategoryId.set('');
    this.expStatus.set(null);
    this.expFortnight.set(null);
    this.expSourceType.set(null);
    this.expPage.set(1);
    this.loadExpenses();
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
      description:   this.incDesc()      || undefined,
      fortnightType: this.incFortnight() ?? undefined,
    }).subscribe({
      next: result => {
        this.incomes.set(result.items);
        this.incTotal.set(result.totalCount);
        this.incLoadingList.set(false);
      },
      error: () => this.incLoadingList.set(false),
    });
  }

  onIncDescChange(event: Event): void {
    this.incDesc.set((event.target as HTMLInputElement).value);
    if (this.incDescDebounce) clearTimeout(this.incDescDebounce);
    this.incDescDebounce = setTimeout(() => { this.incPage.set(1); this.loadIncomes(); }, 350);
  }

  onIncFortnightChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.incFortnight.set(val ? Number(val) as FortnightType : null);
    this.incPage.set(1);
    this.loadIncomes();
  }

  clearIncFilters(): void {
    this.incDesc.set('');
    this.incFortnight.set(null);
    this.incPage.set(1);
    this.loadIncomes();
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
        title = 'DESPESAS DO PERIODO';
        if (exps.length === 0) {
          lines = ['Nenhuma despesa\nregistrada neste periodo.'];
        } else {
          lines = exps.map(e => `• ${e.description}\n  ${this.fmt(e.amount)}`);
          lines.push('', `TOTAL: ${this.fmt(s.totalExpense)}`);
        }
        break;
      }
      case 'pago': {
        title = 'DESPESAS PAGAS';
        const pagas = exps.filter(e => e.paymentStatus === PaymentStatus.Paid);
        if (pagas.length === 0) {
          lines = ['Nenhuma despesa\npaga neste periodo.'];
        } else {
          lines = pagas.map(e => `• ${e.description}\n  ${this.fmt(e.amount)}`);
          lines.push('', `TOTAL PAGO: ${this.fmt(s.totalPaid)}`);
        }
        break;
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
