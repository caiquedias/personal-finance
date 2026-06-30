import { Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { CsvReaderService } from '../../core/services/csv-reader.service';
import { PurgeWarningBannerComponent } from './purge-warning-banner.component';
import { StatCardComponent } from '../../shared/components/stat-card/stat-card.component';
import { HeaderComponent } from '../../shared/components/header/header.component';
import { FilterModalComponent } from '../../shared/components/filter-modal/filter-modal.component';
import { FilterButtonComponent } from '../../shared/components/filter-modal/filter-button.component';
import { FilterFieldConfig } from '../../shared/components/filter-modal/filter-field-config';
import { CurrencyBrlPipe } from '../../shared/pipes/currency-brl.pipe';
import {
  ExpenseResponse, IncomeResponse,
  PAYMENT_STATUS_LABELS, SOURCE_TYPE_LABELS, FORTNIGHT_TYPE_LABELS,
  PaymentStatus, FortnightType,
} from '../../core/models/models';

type ActiveTab = 'expenses' | 'incomes' | 'indicators';

@Component({
  selector: 'app-purge-detail',
  standalone: true,
  imports: [
    PurgeWarningBannerComponent,
    StatCardComponent,
    HeaderComponent,
    FilterModalComponent,
    FilterButtonComponent,
    CurrencyBrlPipe,
    DecimalPipe,
  ],
  template: `
    <!-- Cabeçalho padrão com info do CSV -->
    <app-header
      title="Análise Detalhe"
      [subtitle]="headerSubtitle()"
      [attr.subtitle]="headerSubtitle()"
    />

    <app-purge-warning-banner />

    <!-- Barra de abas + botão de filtro -->
    <div class="purge-toolbar">
      <div class="purge-tabs">
        <button
          class="purge-tab"
          [class.active]="activeTab() === 'expenses'"
          (click)="activeTab.set('expenses')"
        >
          Despesas ({{ filteredExpenses().length }})
        </button>
        <button
          class="purge-tab"
          [class.active]="activeTab() === 'incomes'"
          (click)="activeTab.set('incomes')"
        >
          Receitas ({{ filteredIncomes().length }})
        </button>
        <button
          class="purge-tab"
          [class.active]="activeTab() === 'indicators'"
          (click)="activeTab.set('indicators')"
        >
          Indicadores
        </button>
      </div>

      <!-- Botão de filtro -->
      <app-filter-button
        [activeCount]="activeFilterCount()"
        (toggled)="filterOpen.set(!filterOpen())"
      />
    </div>

    <!-- Modal de filtros -->
    <app-filter-modal
      [fields]="filterFields()"
      [open]="filterOpen()"
      (apply)="onFilterApply($event)"
      (clear)="onFilterClear()"
      (closed)="filterOpen.set(false)"
    />

    <!-- Aba Despesas -->
    @if (activeTab() === 'expenses') {
      @if (filteredExpenses().length > 0) {
        <section class="purge-section">
          <div class="table-wrap">
            <table class="table">
              <thead>
                <tr>
                  <th class="sortable" (click)="sortExpenses('description')">Descrição {{ expSortIcon('description') }}</th>
                  <th class="sortable" (click)="sortExpenses('amount')">Valor {{ expSortIcon('amount') }}</th>
                  <th class="sortable" (click)="sortExpenses('dueDate')">Vencimento {{ expSortIcon('dueDate') }}</th>
                  <th class="sortable" (click)="sortExpenses('paymentDate')">Pagamento {{ expSortIcon('paymentDate') }}</th>
                  <th class="sortable" (click)="sortExpenses('paymentStatus')">Status {{ expSortIcon('paymentStatus') }}</th>
                  <th class="sortable" (click)="sortExpenses('sourceType')">Fonte {{ expSortIcon('sourceType') }}</th>
                  <th class="sortable" (click)="sortExpenses('fortnightType')">Quinzena {{ expSortIcon('fortnightType') }}</th>
                </tr>
              </thead>
              <tbody>
                @for (exp of sortedExpenses(); track exp.id) {
                  <tr>
                    <td class="cell-primary">{{ exp.description }}</td>
                    <td class="font-semibold">{{ exp.amount | currencyBrl }}</td>
                    <td class="text-muted text-sm">{{ exp.dueDate }}</td>
                    <td class="text-muted text-sm">{{ exp.paymentDate ?? '—' }}</td>
                    <td><span class="badge" [class]="statusBadgeClass(exp.paymentStatus)">{{ paymentStatusLabel(exp.paymentStatus) }}</span></td>
                    <td class="text-muted text-sm">{{ sourceTypeLabel(exp.sourceType) }}</td>
                    <td class="text-muted text-sm">{{ fortnightTypeLabel(exp.fortnightType) }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>
      } @else {
        <div class="purge-empty">
          <p>Nenhum dado disponível para exibição.</p>
        </div>
      }
    }

    <!-- Aba Receitas -->
    @if (activeTab() === 'incomes') {
      @if (filteredIncomes().length > 0) {
        <section class="purge-section">
          <div class="table-wrap">
            <table class="table">
              <thead>
                <tr>
                  <th class="sortable" (click)="sortIncomes('description')">Descrição {{ incSortIcon('description') }}</th>
                  <th class="sortable" (click)="sortIncomes('amount')">Valor {{ incSortIcon('amount') }}</th>
                  <th class="sortable" (click)="sortIncomes('receivedAt')">Período {{ incSortIcon('receivedAt') }}</th>
                  <th>Notas</th>
                </tr>
              </thead>
              <tbody>
                @for (inc of sortedIncomes(); track inc.id) {
                  <tr>
                    <td class="cell-primary">{{ inc.description }}</td>
                    <td class="font-semibold">{{ inc.amount | currencyBrl }}</td>
                    <td class="text-muted text-sm">{{ inc.receivedAt }}</td>
                    <td class="text-muted text-sm">{{ inc.notes ?? '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>
      } @else {
        <div class="purge-empty">
          <p>Nenhum dado disponível para exibição.</p>
        </div>
      }
    }

    <!-- Aba Indicadores -->
    @if (activeTab() === 'indicators') {
      <div class="purge-indicators">
        <div class="purge-summary-cards">
          <app-stat-card
            label="Total Receita"
            [value]="kpiTotalIncome()"
            variant="success"
            [isCurrency]="true"
          />
          <app-stat-card
            label="Total Despesa"
            [value]="kpiTotalExpense()"
            variant="danger"
            [isCurrency]="true"
          />
          <app-stat-card
            label="Total Pago"
            [value]="kpiTotalPaid()"
            variant="success"
            [isCurrency]="true"
          />
          <app-stat-card
            label="A Pagar"
            [value]="kpiTotalOwed()"
            variant="danger"
            [isCurrency]="true"
          />
          <app-stat-card
            label="Saldo"
            [value]="kpiBalance()"
            [variant]="kpiBalance() >= 0 ? 'success' : 'danger'"
            [isCurrency]="true"
          />
        </div>

        <!-- Progresso de pagamento -->
        <div class="purge-progress-section">
          <p class="purge-progress-label">
            Progresso de Pagamento: {{ kpiPaymentProgress() | number:'1.0-1' }}%
          </p>
          <div class="purge-progress-bar">
            <div
              class="purge-progress-fill"
              [style.width.%]="kpiPaymentProgress()"
            ></div>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .purge-toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0.75rem 1.5rem;
      border-bottom: 1px solid var(--color-border, #e5e7eb);
      gap: 1rem;
    }

    .purge-tabs {
      display: flex;
      gap: 0.25rem;
    }

    .purge-tab {
      padding: 0.5rem 1rem;
      border: none;
      background: transparent;
      cursor: pointer;
      border-radius: 0.375rem;
      font-size: 0.875rem;
      color: var(--color-text-muted, #6b7280);
      transition: background 0.15s, color 0.15s;
    }

    .purge-tab.active {
      background: var(--color-primary, #3b82f6);
      color: #fff;
    }

    .purge-summary-cards {
      display: flex;
      gap: 1rem;
      padding: 1.5rem;
      flex-wrap: wrap;
    }

    .purge-indicators {
      display: flex;
      flex-direction: column;
    }

    .purge-progress-section {
      padding: 0 1.5rem 1.5rem;
    }

    .purge-progress-label {
      font-size: 0.875rem;
      margin-bottom: 0.5rem;
      color: var(--color-text, #111827);
    }

    .purge-progress-bar {
      width: 100%;
      height: 0.75rem;
      background: var(--color-border, #e5e7eb);
      border-radius: 9999px;
      overflow: hidden;
    }

    .purge-progress-fill {
      height: 100%;
      background: var(--color-primary, #3b82f6);
      border-radius: 9999px;
      transition: width 0.3s;
    }

    .purge-section {
      padding: 1rem 1.5rem;
    }

    .table-wrap {
      background: var(--v2-surface);
      backdrop-filter: blur(24px);
      border-radius: var(--v2-radius);
      border: 1px solid var(--v2-border);
      box-shadow: var(--v2-shadow);
      overflow-x: auto;
    }

    .table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.875rem;
      font-family: var(--font-b);
    }

    .table th {
      text-align: left;
      padding: 12px 16px;
      font-size: 10px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.1em;
      color: var(--text-subtle);
      border-bottom: 1px solid var(--v2-border);
      white-space: nowrap;
    }

    .table th.sortable { cursor: pointer; user-select: none; }
    .table th.sortable:hover { color: var(--text); }

    .table td {
      padding: 12px 16px;
      border-bottom: 1px solid var(--v2-border);
      color: var(--text-muted);
      vertical-align: middle;
    }

    .table tr:last-child td { border-bottom: none; }
    .table tbody tr:hover { background: rgba(255,255,255,0.03); }

    .cell-primary  { color: var(--text); font-weight: 500; }
    .text-sm       { font-size: 0.8125rem; }
    .text-muted    { color: var(--text-muted); }
    .font-semibold { font-family: var(--font-d); font-weight: 500; color: var(--text); }

    .purge-empty {
      padding: 2rem;
      text-align: center;
      color: var(--text-muted);
    }
  `],
})
export class PurgeDetailComponent {
  private readonly csvReaderService = inject(CsvReaderService);

  // Dados brutos do serviço
  readonly expenses = this.csvReaderService.expenses;
  readonly incomes  = this.csvReaderService.incomes;
  readonly summary  = this.csvReaderService.summary;

  // Aba ativa
  readonly activeTab = signal<ActiveTab>('expenses');

  // Filtros compartilhados
  readonly filterDesc      = signal<string>('');
  readonly filterFortnight = signal<FortnightType | null>(null);
  readonly filterOpen      = signal<boolean>(false);

  // Ordenação de despesas
  readonly expSortColumn = signal<keyof ExpenseResponse | ''>('');
  private readonly expSortAsc = signal<boolean>(true);

  // Ordenação de receitas
  private readonly incSortColumn = signal<keyof IncomeResponse | ''>('');
  private readonly incSortAsc    = signal<boolean>(true);

  // Contador de filtros ativos
  readonly activeFilterCount = computed<number>(() => {
    let count = 0;
    if (this.filterDesc().trim() !== '') count++;
    if (this.filterFortnight() !== null) count++;
    return count;
  });

  // Subtitle do header com info do CSV
  readonly headerSubtitle = computed<string>(() => {
    const expCount = this.expenses().length;
    const incCount = this.incomes().length;
    return `${expCount} despesas · ${incCount} receitas`;
  });

  // Campos de filtro para o FilterModalComponent
  readonly filterFields = computed<FilterFieldConfig[]>(() => [
    {
      key:   'description',
      label: 'Descrição',
      type:  'text',
      value: this.filterDesc(),
    },
    {
      key:     'fortnightType',
      label:   'Quinzena',
      type:    'select',
      value:   this.filterFortnight() !== null ? String(this.filterFortnight()) : '',
      options: [
        { value: '',                           label: 'Ambas' },
        { value: String(FortnightType.First),  label: '1ª Quinzena' },
        { value: String(FortnightType.Second), label: '2ª Quinzena' },
      ],
    },
  ]);

  // Despesas filtradas
  readonly filteredExpenses = computed<ExpenseResponse[]>(() => {
    const desc      = this.filterDesc().trim().toLowerCase();
    const fortnight = this.filterFortnight();

    return this.expenses().filter(exp => {
      if (desc && !exp.description.toLowerCase().includes(desc)) return false;
      if (fortnight !== null && exp.fortnightType !== fortnight) return false;
      return true;
    });
  });

  // Receitas filtradas
  readonly filteredIncomes = computed<IncomeResponse[]>(() => {
    const desc = this.filterDesc().trim().toLowerCase();

    return this.incomes().filter(inc => {
      if (desc && !inc.description.toLowerCase().includes(desc)) return false;
      return true;
    });
  });

  // Despesas ordenadas
  readonly sortedExpenses = computed<ExpenseResponse[]>(() => {
    const col = this.expSortColumn();
    const asc = this.expSortAsc();
    const list = [...this.filteredExpenses()];
    if (!col) return list;
    return list.sort((a, b) => {
      const av = a[col] ?? '';
      const bv = b[col] ?? '';
      if (av < bv) return asc ? -1 : 1;
      if (av > bv) return asc ? 1 : -1;
      return 0;
    });
  });

  // Receitas ordenadas
  readonly sortedIncomes = computed<IncomeResponse[]>(() => {
    const col = this.incSortColumn();
    const asc = this.incSortAsc();
    const list = [...this.filteredIncomes()];
    if (!col) return list;
    return list.sort((a, b) => {
      const av = a[col] ?? '';
      const bv = b[col] ?? '';
      if (av < bv) return asc ? -1 : 1;
      if (av > bv) return asc ? 1 : -1;
      return 0;
    });
  });

  // KPIs calculados sobre dados filtrados
  readonly kpiTotalIncome = computed<number>(() =>
    this.filteredIncomes().reduce((s, i) => s + i.amount, 0)
  );

  readonly kpiTotalExpense = computed<number>(() =>
    this.filteredExpenses().reduce((s, e) => s + e.amount, 0)
  );

  readonly kpiTotalPaid = computed<number>(() =>
    this.filteredExpenses()
      .filter(e => e.paymentStatus === PaymentStatus.Paid)
      .reduce((s, e) => s + e.amount, 0)
  );

  readonly kpiTotalOwed = computed<number>(() =>
    this.filteredExpenses()
      .filter(e => e.paymentStatus === PaymentStatus.Pending || e.paymentStatus === PaymentStatus.Partial)
      .reduce((s, e) => s + e.amount, 0)
  );

  readonly kpiBalance = computed<number>(() =>
    this.kpiTotalIncome() - this.kpiTotalExpense()
  );

  readonly kpiPaymentProgress = computed<number>(() => {
    const total = this.kpiTotalExpense();
    if (total === 0) return 0;
    return Math.min((this.kpiTotalPaid() / total) * 100, 100);
  });

  // Aplica filtros vindos do FilterModalComponent
  onFilterApply(values: Record<string, unknown>): void {
    const desc = (values['description'] as string) ?? '';
    this.filterDesc.set(desc);

    const ft = values['fortnightType'];
    if (ft === '' || ft === null || ft === undefined) {
      this.filterFortnight.set(null);
    } else {
      this.filterFortnight.set(Number(ft) as FortnightType);
    }

    this.filterOpen.set(false);
  }

  // Limpa todos os filtros
  onFilterClear(): void {
    this.filterDesc.set('');
    this.filterFortnight.set(null);
  }

  // Ordenação de despesas
  sortExpenses(col: keyof ExpenseResponse): void {
    if (this.expSortColumn() === col) {
      this.expSortAsc.update(v => !v);
    } else {
      this.expSortColumn.set(col);
      this.expSortAsc.set(true);
    }
  }

  expSortIcon(col: keyof ExpenseResponse): string {
    if (this.expSortColumn() !== col) return '↕';
    return this.expSortAsc() ? '↑' : '↓';
  }

  // Ordenação de receitas
  sortIncomes(col: keyof IncomeResponse): void {
    if (this.incSortColumn() === col) {
      this.incSortAsc.update(v => !v);
    } else {
      this.incSortColumn.set(col);
      this.incSortAsc.set(true);
    }
  }

  incSortIcon(col: keyof IncomeResponse): string {
    if (this.incSortColumn() !== col) return '↕';
    return this.incSortAsc() ? '↑' : '↓';
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

  // Helpers de label
  paymentStatusLabel(status: number): string {
    return PAYMENT_STATUS_LABELS[status as keyof typeof PAYMENT_STATUS_LABELS] ?? String(status);
  }

  sourceTypeLabel(source: number): string {
    return SOURCE_TYPE_LABELS[source as keyof typeof SOURCE_TYPE_LABELS] ?? String(source);
  }

  fortnightTypeLabel(fortnight: number): string {
    return FORTNIGHT_TYPE_LABELS[fortnight as keyof typeof FORTNIGHT_TYPE_LABELS] ?? String(fortnight);
  }
}
