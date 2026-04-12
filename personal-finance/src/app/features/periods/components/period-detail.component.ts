import { Component, inject, signal, computed, OnInit, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { animate, style, transition, trigger } from '@angular/animations';
import { ApiService } from '../../../core/services/api.service';
import { HeaderComponent } from '../../../shared/components/header/header.component';
import { CurrencyBrlPipe } from '../../../shared/pipes/currency-brl.pipe';
import { MarioModalComponent } from '../../../shared/components/modal/mario-modal.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import {
  PeriodSummary, ExpenseResponse, IncomeResponse, CategoryResponse,
  MONTH_NAMES, PAYMENT_STATUS_LABELS, SOURCE_TYPE_LABELS,
  FORTNIGHT_TYPE_LABELS, PaymentStatus, FortnightType
} from '../../../core/models/models';

type MarioTarget = 'receitas' | 'despesas' | 'pago' | 'apagar' | 'saldo';
type ExpSortCol = 'description' | 'category' | 'fortnightType' | 'dueDate' | 'amount' | 'paymentStatus';
type IncSortCol = 'description' | 'fortnightType' | 'receivedAt' | 'amount';

@Component({
  selector: 'app-period-detail',
  standalone: true,
  imports: [HeaderComponent, CurrencyBrlPipe, RouterLink, MarioModalComponent, PaginationComponent],
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
  template: `
    <app-header
      [title]="headerTitle()"
      subtitle="Detalhes do período"
    >
      <!-- Indicador somente-leitura -->
      <span class="readonly-indicator" tabindex="0">
        <img class="pswitch-icon" src="icons/readonly-indicator.png" width="22" height="22" alt="Somente leitura" />
        <span class="readonly-tooltip">Tela apenas para consulta/visualização</span>
      </span>
      <a routerLink="/periods" class="btn btn-secondary btn-sm">← Períodos</a>
      <a routerLink="/expenses" class="btn btn-primary btn-sm">+ Despesa</a>
      <a routerLink="/incomes" class="btn btn-ghost btn-sm">+ Receita</a>
    </app-header>

    <div class="page-content">
      @if (loading()) {
        <div class="loading-state"><span class="spinner-lg"></span></div>
      } @else {

        <!-- Resumo compacto -->
        @if (summary()) {
          <div class="summary-bar card">
            <div class="summary-item">
              <span class="summary-label">
                Receitas
                <button class="mario-q" (click)="openMario('receitas')" title="Detalhes de Receitas">?</button>
              </span>
              <span class="summary-value success">{{ summary()!.totalIncome | currencyBrl }}</span>
            </div>
            <div class="summary-divider"></div>
            <div class="summary-item">
              <span class="summary-label">
                Despesas
                <button class="mario-q" (click)="openMario('despesas')" title="Detalhes de Despesas">?</button>
              </span>
              <span class="summary-value danger">{{ summary()!.totalExpense | currencyBrl }}</span>
            </div>
            <div class="summary-divider"></div>
            <div class="summary-item">
              <span class="summary-label">
                Pago
                <button class="mario-q" (click)="openMario('pago')" title="Detalhes de Pago">?</button>
              </span>
              <span class="summary-value">{{ summary()!.totalPaid | currencyBrl }}</span>
            </div>
            <div class="summary-divider"></div>
            <div class="summary-item">
              <span class="summary-label">
                A pagar
                <button class="mario-q" (click)="openMario('apagar')" title="Detalhes de A pagar">?</button>
              </span>
              <span class="summary-value warning">{{ summary()!.totalOwed | currencyBrl }}</span>
            </div>
            <div class="summary-divider"></div>
            <div class="summary-item">
              <span class="summary-label">
                Saldo
                <button class="mario-q" (click)="openMario('saldo')" title="Detalhes do Saldo">?</button>
              </span>
              <span class="summary-value" [class]="summary()!.balance >= 0 ? 'success' : 'danger'">
                {{ summary()!.balance | currencyBrl: true }}
              </span>
            </div>
          </div>
        }

        <!-- Abas -->
        <div class="tabs">
          <button class="tab" [class.active]="activeTab() === 'expenses'" (click)="activeTab.set('expenses')">
            Despesas ({{ expTotal() }})
          </button>
          <button class="tab" [class.active]="activeTab() === 'incomes'" (click)="activeTab.set('incomes')">
            Receitas ({{ incTotal() }})
          </button>
        </div>

        <!-- ── Despesas ── -->
        @if (activeTab() === 'expenses') {
          <div class="tab-shortcut">
            <a routerLink="/expenses" [queryParams]="{ periodId: id() }" class="btn btn-purple btn-sm">
              Ir para Despesas →
            </a>
          </div>

          <!-- Filtros externos -->
          <div class="external-filters card">
            <div class="filters-grid">
              <div class="filter-field">
                <label class="field-label">Descrição</label>
                <input
                  class="input input-sm"
                  placeholder="Buscar..."
                  [value]="expDesc()"
                  (input)="onExpDescChange($event)"
                />
              </div>
              <div class="filter-field">
                <label class="field-label">Categoria</label>
                <select class="input input-sm" (change)="onExpCategoryChange($event)">
                  <option value="">Todas</option>
                  @for (c of categories(); track c.id) {
                    <option [value]="c.id">{{ c.name }}</option>
                  }
                </select>
              </div>
              <div class="filter-field">
                <label class="field-label">Status</label>
                <select class="input input-sm" (change)="onExpStatusChange($event)">
                  <option value="">Todos</option>
                  <option [value]="PaymentStatus.Pending">Pendente</option>
                  <option [value]="PaymentStatus.Paid">Pago</option>
                  <option [value]="PaymentStatus.Partial">Parcial</option>
                  <option [value]="PaymentStatus.Cancelled">Cancelado</option>
                </select>
              </div>
              <div class="filter-field">
                <label class="field-label">Quinzena</label>
                <select class="input input-sm" (change)="onExpFortnightChange($event)">
                  <option value="">Ambas</option>
                  <option [value]="FortnightType.First">1ª Quinzena</option>
                  <option [value]="FortnightType.Second">2ª Quinzena</option>
                </select>
              </div>
            </div>
            @if (expHasFilters()) {
              <button class="btn-clear-filters" (click)="clearExpFilters()">Limpar filtros</button>
            }
          </div>

          @if (expLoadingList()) {
            <div class="loading-state"><span class="spinner-lg"></span></div>
          } @else if (expTotal() === 0) {
            <div class="empty-state">
              <p>Nenhuma despesa encontrada.</p>
              <a routerLink="/expenses" class="btn btn-primary btn-sm">Adicionar despesa</a>
            </div>
          } @else {
            <div class="table-wrap card">
              <table class="table">
                <thead>
                  <tr>
                    <th class="sortable" (click)="toggleExpSort('description')">Descrição {{ expSortIcon('description') }}</th>
                    <th class="sortable" (click)="toggleExpSort('category')">Categoria {{ expSortIcon('category') }}</th>
                    <th class="sortable" (click)="toggleExpSort('fortnightType')">Quinzena {{ expSortIcon('fortnightType') }}</th>
                    <th class="sortable" (click)="toggleExpSort('dueDate')">Vencimento {{ expSortIcon('dueDate') }}</th>
                    <th class="sortable" (click)="toggleExpSort('amount')">Valor {{ expSortIcon('amount') }}</th>
                    <th class="sortable" (click)="toggleExpSort('paymentStatus')">Status {{ expSortIcon('paymentStatus') }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (expense of displayedExpenses(); track expense.id) {
                    <tr>
                      <td>
                        <div class="cell-primary">{{ expense.description }}</div>
                        <div class="cell-secondary">{{ sourceLabel(expense.sourceType) }}</div>
                      </td>
                      <td>
                        <span class="category-dot" [style.background]="categoryColor(expense.categoryId)"></span>
                        {{ categoryName(expense.categoryId) }}
                      </td>
                      <td class="text-muted text-sm">{{ fortnightLabel(expense.fortnightType) }}</td>
                      <td class="text-muted text-sm">{{ formatDate(expense.dueDate) }}</td>
                      <td class="font-semibold">{{ expense.amount | currencyBrl }}</td>
                      <td>
                        <span class="badge" [class]="statusBadgeClass(expense.paymentStatus)">
                          {{ statusLabel(expense.paymentStatus) }}
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <app-pagination
              [totalCount]="expTotal()"
              [pageSize]="expPageSize()"
              [currentPage]="expPage()"
              (pageChange)="onExpPageChange($event)"
              (pageSizeChange)="onExpPageSizeChange($event)"
            />
          }
        }

        <!-- ── Receitas ── -->
        @if (activeTab() === 'incomes') {
          <div class="tab-shortcut">
            <a routerLink="/incomes" [queryParams]="{ periodId: id() }" class="btn btn-purple btn-sm">
              Ir para Receitas →
            </a>
          </div>

          <!-- Filtros externos -->
          <div class="external-filters card">
            <div class="filters-grid">
              <div class="filter-field">
                <label class="field-label">Descrição</label>
                <input
                  class="input input-sm"
                  placeholder="Buscar..."
                  [value]="incDesc()"
                  (input)="onIncDescChange($event)"
                />
              </div>
              <div class="filter-field">
                <label class="field-label">Quinzena</label>
                <select class="input input-sm" (change)="onIncFortnightChange($event)">
                  <option value="">Ambas</option>
                  <option [value]="FortnightType.First">1ª Quinzena</option>
                  <option [value]="FortnightType.Second">2ª Quinzena</option>
                </select>
              </div>
            </div>
            @if (incHasFilters()) {
              <button class="btn-clear-filters" (click)="clearIncFilters()">Limpar filtros</button>
            }
          </div>

          @if (incLoadingList()) {
            <div class="loading-state"><span class="spinner-lg"></span></div>
          } @else if (incTotal() === 0) {
            <div class="empty-state">
              <p>Nenhuma receita encontrada.</p>
              <a routerLink="/incomes" class="btn btn-primary btn-sm">Adicionar receita</a>
            </div>
          } @else {
            <div class="table-wrap card">
              <table class="table">
                <thead>
                  <tr>
                    <th class="sortable" (click)="toggleIncSort('description')">Descrição {{ incSortIcon('description') }}</th>
                    <th class="sortable" (click)="toggleIncSort('fortnightType')">Quinzena {{ incSortIcon('fortnightType') }}</th>
                    <th class="sortable" (click)="toggleIncSort('receivedAt')">Recebido em {{ incSortIcon('receivedAt') }}</th>
                    <th class="sortable" (click)="toggleIncSort('amount')">Valor {{ incSortIcon('amount') }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (income of displayedIncomes(); track income.id) {
                    <tr>
                      <td class="cell-primary">{{ income.description }}</td>
                      <td class="text-muted text-sm">{{ fortnightLabel(income.fortnightType) }}</td>
                      <td class="text-muted text-sm">{{ formatDate(income.receivedAt) }}</td>
                      <td class="font-semibold success-text">{{ income.amount | currencyBrl }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <app-pagination
              [totalCount]="incTotal()"
              [pageSize]="incPageSize()"
              [currentPage]="incPage()"
              (pageChange)="onIncPageChange($event)"
              (pageSizeChange)="onIncPageSizeChange($event)"
            />
          }
        }

        <!-- Modal Mario -->
        @if (marioOpen()) {
          <div class="mario-overlay" @backdropAnim (click)="marioOpen.set(false)"></div>
          <div class="mario-center" @modalAnim>
            <app-mario-modal
              [title]="marioTitle()"
              [content]="marioContent()"
              (closed)="marioOpen.set(false)"
            />
          </div>
        }

      }
    </div>
  `,
  styles: [`
    .page-content {
      padding: 28px;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }

    /* Summary bar */
    .summary-bar {
      display: flex;
      align-items: center;
      padding: 16px 24px;
      gap: 0;
    }

    .summary-item {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 4px;
      align-items: center;
    }

    .summary-label {
      font-size: 0.75rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--ink3);
      display: flex;
      align-items: center;
      gap: 4px;
    }

    .summary-value {
      font-size: 1.0625rem;
      font-weight: 700;
      color: var(--ink);
    }

    .summary-value.success { color: var(--sage2); }
    .summary-value.danger  { color: var(--rust);  }
    .summary-value.warning { color: var(--color-warning); }

    .summary-divider {
      width: 1px;
      height: 32px;
      background: var(--border);
      margin: 0 8px;
    }

    /* Botão ? estilo bloco Mario */
    .mario-q {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 18px;
      height: 18px;
      background: #f5c518;
      color: #5c3800;
      font-size: 0.65rem;
      font-weight: 900;
      border: 2px solid #8a5400;
      border-radius: 3px;
      cursor: pointer;
      box-shadow: 0 2px 0 #8a5400;
      transition: transform 80ms ease, box-shadow 80ms ease;
      line-height: 1;
      padding: 0;
      flex-shrink: 0;
    }

    .mario-q:hover {
      transform: translateY(-2px);
      box-shadow: 0 4px 0 #8a5400;
    }

    .mario-q:active {
      transform: translateY(1px);
      box-shadow: 0 1px 0 #8a5400;
    }

    /* Overlay Mario */
    .mario-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.72);
      z-index: 900;
    }

    .mario-center {
      position: fixed;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 901;
      pointer-events: none;
    }

    .mario-center > * {
      pointer-events: all;
    }

    /* Tabs */
    .tabs {
      display: flex;
      gap: 4px;
      border-bottom: 1px solid var(--border);
    }

    .tab {
      padding: 10px 16px;
      border: none;
      background: none;
      color: var(--ink3);
      font-size: 0.875rem;
      font-weight: 500;
      cursor: pointer;
      border-bottom: 2px solid transparent;
      margin-bottom: -1px;
      transition: all var(--transition);
    }

    .tab:hover  { color: var(--ink); }
    .tab.active { color: var(--sage2); border-bottom-color: var(--sage2); }

    /* External filters */
    .external-filters {
      padding: 16px;
    }

    .filters-grid {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
    }

    .filter-field {
      display: flex;
      flex-direction: column;
      gap: 4px;
      min-width: 160px;
    }

    .btn-clear-filters {
      margin-top: 12px;
      background: none;
      border: none;
      color: var(--ink3);
      font-size: 0.8125rem;
      cursor: pointer;
      padding: 0;
      text-decoration: underline;
    }

    .btn-clear-filters:hover { color: var(--rust); }

    /* Table */
    .table-wrap { overflow-x: auto; }

    .table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.875rem;
    }

    .table th {
      text-align: left;
      padding: 12px 16px;
      font-size: 0.75rem;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--ink3);
      border-bottom: 1px solid var(--border);
      white-space: nowrap;
    }

    .table th.sortable {
      cursor: pointer;
      user-select: none;
    }

    .table th.sortable:hover { color: var(--ink); }

    .table td {
      padding: 12px 16px;
      border-bottom: 1px solid var(--bg3);
      color: var(--ink2);
      vertical-align: middle;
    }

    .table tr:last-child td { border-bottom: none; }

    .table tbody tr:hover { background: var(--surface-overlay); }

    .cell-primary   { color: var(--ink); font-weight: 500; }
    .cell-secondary { font-size: 0.75rem; color: var(--ink3); margin-top: 2px; }

    .category-dot {
      display: inline-block;
      width: 8px;
      height: 8px;
      border-radius: 50%;
      margin-right: 6px;
    }

    .text-sm   { font-size: 0.8125rem; }
    .font-semibold { font-weight: 600; color: var(--ink); }
    .success-text  { color: var(--sage2); }

    .loading-state, .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
      padding: 64px;
      color: var(--ink3);
    }

    .spinner-lg {
      width: 32px; height: 32px;
      border: 3px solid var(--border);
      border-top-color: var(--sage2);
      border-radius: 50%;
      animation: spin .8s linear infinite;
      display: block;
    }

    @keyframes spin { to { transform: rotate(360deg); } }

    /* ── P-Switch readonly indicator ── */
    .readonly-indicator {
      position: relative;
      display: inline-flex;
      align-items: center;
      cursor: default;
      outline: none;
    }

    .pswitch-icon {
      display: block;
      image-rendering: pixelated;
      animation: pswitch-bob 1.4s ease-in-out infinite;
    }

    @keyframes pswitch-bob {
      0%   { transform: scale(1)    translateY(0);    opacity: 1;    }
      30%  { transform: scale(1.08) translateY(-3px); opacity: 0.85; }
      60%  { transform: scale(1)    translateY(0);    opacity: 1;    }
      80%  { transform: scale(0.97) translateY(2px);  opacity: 0.9;  }
      100% { transform: scale(1)    translateY(0);    opacity: 1;    }
    }

    .readonly-tooltip {
      visibility: hidden;
      opacity: 0;
      position: absolute;
      top: calc(100% + 8px);
      right: 0;
      background: var(--ink);
      color: var(--bg);
      font-size: 0.75rem;
      font-weight: 500;
      white-space: nowrap;
      padding: 5px 10px;
      border-radius: var(--radius);
      box-shadow: var(--shadow-modal);
      pointer-events: none;
      transition: opacity 150ms ease, visibility 150ms ease;
      z-index: 100;
    }

    .readonly-tooltip::before {
      content: '';
      position: absolute;
      bottom: 100%;
      right: 8px;
      border: 5px solid transparent;
      border-bottom-color: var(--ink);
    }

    .readonly-indicator:hover .readonly-tooltip,
    .readonly-indicator:focus .readonly-tooltip {
      visibility: visible;
      opacity: 1;
    }

    /* ── Botão roxo pastel ── */
    .btn-purple {
      background: #c4b5e8;
      color: #4a2d8a;
      border: 1px solid #b0a0d8;
    }
    .btn-purple:hover:not(:disabled) {
      background: #b3a2dc;
      border-color: #9e8dcc;
      color: #3a2070;
    }
    :host-context(.dark) .btn-purple {
      background: #7c5cc4;
      color: #f0ebff;
      border-color: #6a4db0;
    }
    :host-context(.dark) .btn-purple:hover:not(:disabled) {
      background: #8d6ed4;
      border-color: #7a5dc0;
      color: #ffffff;
    }

    /* ── Linha de atalho acima da tabela ── */
    .tab-shortcut {
      display: flex;
      justify-content: flex-end;
    }
  `]
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
  readonly expSortCol      = signal<ExpSortCol | null>(null);
  readonly expSortDir      = signal<'asc' | 'desc'>('asc');
  private expDescDebounce: ReturnType<typeof setTimeout> | null = null;

  readonly expHasFilters = computed(() =>
    !!this.expDesc() || !!this.expCategoryId() ||
    this.expStatus() != null || this.expFortnight() != null);

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
      paymentStatus: this.expStatus()     ?? undefined,
      fortnightType: this.expFortnight()  ?? undefined,
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

  clearExpFilters(): void {
    this.expDesc.set('');
    this.expCategoryId.set('');
    this.expStatus.set(null);
    this.expFortnight.set(null);
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
    let title = '';
    let lines: string[] = [];

    switch (target) {
      case 'receitas': {
        title = 'RECEITAS DO PERIODO';
        lines = [
          `Total: ${this.fmt(s.totalIncome)}`,
          '',
          '(Veja a aba Receitas para o detalhamento)',
        ];
        break;
      }
      case 'despesas': {
        title = 'DESPESAS DO PERIODO';
        lines = [
          `Total: ${this.fmt(s.totalExpense)}`,
          '',
          '(Veja a aba Despesas para o detalhamento)',
        ];
        break;
      }
      case 'pago': {
        title = 'DESPESAS PAGAS';
        lines = [`Total pago: ${this.fmt(s.totalPaid)}`];
        break;
      }
      case 'apagar': {
        title = 'DESPESAS A PAGAR';
        lines = [`Total a pagar: ${this.fmt(s.totalOwed)}`];
        break;
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
