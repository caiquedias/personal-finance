import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { switchMap, catchError, of } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { HeaderComponent } from '../../../shared/components/header/header.component';
import { StatCardComponent } from '../../../shared/components/stat-card/stat-card.component';
import { CurrencyBrlPipe } from '../../../shared/pipes/currency-brl.pipe';
import { PeriodResponse, PeriodSummary, MONTH_NAMES, PaymentStatus } from '../../../core/models/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [HeaderComponent, StatCardComponent, CurrencyBrlPipe, DecimalPipe, RouterLink],
  template: `
    <app-header
      title="Dashboard"
      [subtitle]="headerSubtitle()"
    >
      <button class="btn btn-primary btn-sm" routerLink="/periods">
        Ver períodos
      </button>
    </app-header>

    <div class="page-content">

      <!-- Seletor de período -->
      <div class="period-selector">
        <div class="period-selector-header">
          <span class="section-title">Período</span>
          @if (availableYears().length > 1) {
            <div class="year-selector">
              @for (year of availableYears(); track year) {
                <button
                  class="year-tab"
                  [class.active]="selectedYear() === year"
                  (click)="selectYear(year)"
                >{{ year }}</button>
              }
            </div>
          }
        </div>
        <div class="period-tabs">
          @for (period of filteredPeriods(); track period.id) {
            <button
              class="period-tab"
              [class.active]="selectedPeriodId() === period.id"
              (click)="selectPeriod(period.id)"
            >
              {{ monthName(period.month) }}/{{ period.year }}
            </button>
          }
          @if (filteredPeriods().length === 0 && !loadingPeriods()) {
            <span class="text-muted" style="font-size: 0.875rem">
              Nenhum período cadastrado.
              <a routerLink="/periods">Criar período</a>
            </span>
          }
        </div>
      </div>

      @if (loadingSummary()) {
        <div class="loading-state">
          <span class="spinner-lg"></span>
          <span class="text-muted">Carregando resumo...</span>
        </div>
      } @else if (summary()) {
        <!-- Cards de resumo -->
        <div class="stats-grid">
          <app-stat-card
            label="Saldo"
            [value]="summary()!.balance"
            [variant]="summary()!.balance >= 0 ? 'success' : 'danger'"
            [subtitle]="summary()!.balance >= 0 ? 'Positivo no período' : 'Negativo no período'"
            [icon]="balanceIcon()"
          />
          <app-stat-card
            label="Receitas"
            [value]="summary()!.totalIncome"
            variant="success"
            subtitle="Total recebido"
            [icon]="incomeIcon"
          />
          <app-stat-card
            label="Despesas"
            [value]="summary()!.totalExpense"
            variant="danger"
            subtitle="Total de gastos"
            [icon]="expenseIcon"
          />
          <app-stat-card
            label="A pagar"
            [value]="summary()!.totalOwed"
            variant="warning"
            subtitle="Pendente + Parcial"
            [icon]="owedIcon"
          />
        </div>

        <!-- Detalhes por quinzena e pagamento -->
        <div class="detail-grid">
          <!-- Por quinzena -->
          <div class="card detail-card">
            <h3 class="detail-card-title">Por quinzena</h3>
            <div class="detail-row">
              <span class="detail-label">1ª Quinzena</span>
              <span class="detail-value">{{ summary()!.totalFirstFortnight | currencyBrl }}</span>
            </div>
            <div class="detail-row">
              <span class="detail-label">2ª Quinzena</span>
              <span class="detail-value">{{ summary()!.totalSecondFortnight | currencyBrl }}</span>
            </div>
            <hr class="divider" style="margin: 12px 0">
            <div class="detail-row">
              <span class="detail-label" style="font-weight: 600">Total</span>
              <span class="detail-value" style="font-weight: 700">{{ summary()!.totalExpense | currencyBrl }}</span>
            </div>
          </div>

          <!-- Por status -->
          <div class="card detail-card">
            <h3 class="detail-card-title">Status de pagamento</h3>
            <div class="detail-row">
              <span class="detail-label">
                <span class="status-dot paid"></span>
                Pago
              </span>
              <span class="detail-value paid">{{ summary()!.totalPaid | currencyBrl }}</span>
            </div>
            <div class="detail-row">
              <span class="detail-label">
                <span class="status-dot owed"></span>
                Pendente
              </span>
              <span class="detail-value owed">{{ summary()!.totalOwed | currencyBrl }}</span>
            </div>
            <hr class="divider" style="margin: 12px 0">
            <div class="progress-bar-wrap">
              <div
                class="progress-bar-fill"
                [style.width.%]="paymentProgress()"
              ></div>
            </div>
            <span class="detail-label" style="font-size:0.75rem">
              {{ paymentProgress() | number:'1.0-0' }}% pago
            </span>
          </div>

          <!-- Atalhos rápidos -->
          <div class="card detail-card">
            <h3 class="detail-card-title">Ações rápidas</h3>
            <div class="quick-actions">
              <a [routerLink]="['/periods', selectedPeriodId()]" class="quick-action">
                <span class="quick-action-icon">📋</span>
                <span>Ver lançamentos</span>
              </a>
              <a routerLink="/expenses" class="quick-action">
                <span class="quick-action-icon">➕</span>
                <span>Nova despesa</span>
              </a>
              <a routerLink="/incomes" class="quick-action">
                <span class="quick-action-icon">💰</span>
                <span>Nova receita</span>
              </a>
            </div>
          </div>
        </div>
      } @else if (!loadingPeriods() && filteredPeriods().length > 0) {
        <div class="empty-state">
          <span>Selecione um período para ver o resumo</span>
        </div>
      }

    </div>
  `,
  styles: [`
    .page-content {
      padding: 28px;
      display: flex;
      flex-direction: column;
      gap: 24px;
      flex: 1;
    }

    /* ── Seletor de período ── */
    .period-selector {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }

    .period-selector-header {
      display: flex;
      align-items: center;
      gap: 16px;
    }

    .year-selector {
      display: flex;
      gap: 4px;
    }

    .year-tab {
      padding: 3px 12px;
      border-radius: 20px;
      border: 1px solid var(--border);
      background: var(--surface-raised);
      color: var(--ink2);
      font-size: 0.8125rem;
      font-weight: 500;
      cursor: pointer;
      transition: all var(--transition);
    }

    .year-tab:hover { background: var(--bg2); }

    .year-tab.active {
      background: var(--ink3);
      border-color: var(--ink3);
      color: #fff;
    }

    .period-tabs {
      display: flex;
      gap: 6px;
      flex-wrap: wrap;
    }

    .period-tab {
      padding: 6px 14px;
      border-radius: 20px;
      border: 1px solid var(--border);
      background: var(--surface-raised);
      color: var(--ink2);
      font-size: 0.8125rem;
      font-weight: 500;
      cursor: pointer;
      transition: all var(--transition);
    }

    .period-tab:hover { background: var(--bg2); }

    .period-tab.active {
      background: var(--sage2);
      border-color: var(--sage2);
      color: #fff;
    }

    /* ── Grid de stats ── */
    .stats-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 16px;
    }

    @media (max-width: 1200px) { .stats-grid { grid-template-columns: repeat(2, 1fr); } }
    @media (max-width: 640px)  { .stats-grid { grid-template-columns: 1fr; } }

    /* ── Grid de detalhes ── */
    .detail-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 16px;
    }

    @media (max-width: 960px) { .detail-grid { grid-template-columns: 1fr; } }

    .detail-card { padding: 20px; }

    .detail-card-title {
      font-size: 0.9375rem;
      font-weight: 600;
      margin-bottom: 16px;
      color: var(--ink);
    }

    .detail-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 6px 0;
    }

    .detail-label {
      font-size: 0.875rem;
      color: var(--ink2);
      display: flex;
      align-items: center;
      gap: 6px;
    }

    .detail-value {
      font-size: 0.9375rem;
      font-weight: 600;
      color: var(--ink);
    }

    .detail-value.paid  { color: var(--sage2); }
    .detail-value.owed  { color: var(--color-warning); }

    .status-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      display: inline-block;
    }

    .status-dot.paid { background: var(--sage2); }
    .status-dot.owed { background: var(--color-warning); }

    /* Progress bar */
    .progress-bar-wrap {
      height: 6px;
      background: var(--bg3);
      border-radius: 3px;
      overflow: hidden;
      margin-bottom: 6px;
    }

    .progress-bar-fill {
      height: 100%;
      background: var(--sage2);
      border-radius: 3px;
      transition: width 0.5s ease;
    }

    /* Quick actions */
    .quick-actions {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .quick-action {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 10px;
      border-radius: var(--radius);
      color: var(--ink2);
      font-size: 0.875rem;
      text-decoration: none;
      transition: all var(--transition);
    }

    .quick-action:hover {
      background: var(--bg2);
      color: var(--ink);
    }

    .quick-action-icon { font-size: 16px; }

    /* States */
    .loading-state, .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 12px;
      padding: 64px;
      color: var(--ink3);
    }

    .spinner-lg {
      width: 32px;
      height: 32px;
      border: 3px solid var(--border);
      border-top-color: var(--sage2);
      border-radius: 50%;
      animation: spin .8s linear infinite;
      display: block;
    }

    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class DashboardComponent implements OnInit {
  private readonly api  = inject(ApiService);
  private readonly auth = inject(AuthService);

  readonly periods          = signal<PeriodResponse[]>([]);
  readonly selectedYear     = signal<number>(new Date().getFullYear());
  readonly selectedPeriodId = signal<string | null>(null);
  readonly summary          = signal<PeriodSummary | null>(null);
  readonly loadingPeriods   = signal(true);
  readonly loadingSummary   = signal(false);

  readonly availableYears = computed(() =>
    [...new Set(this.periods().map(p => p.year))].sort((a, b) => b - a)
  );

  readonly filteredPeriods = computed(() =>
    this.periods().filter(p => p.year === this.selectedYear())
  );

  readonly headerSubtitle = computed(() => {
    const period = this.periods().find(p => p.id === this.selectedPeriodId());
    if (!period) return 'Selecione um período';
    return `${MONTH_NAMES[period.month - 1]} de ${period.year}`;
  });

  readonly paymentProgress = computed(() => {
    const s = this.summary();
    if (!s || s.totalExpense === 0) return 0;
    return Math.min((s.totalPaid / s.totalExpense) * 100, 100);
  });

  readonly balanceIcon = computed(() => {
    const pos = this.summary()?.balance ?? 0 >= 0;
    return pos
      ? `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="23 6 13.5 15.5 8.5 10.5 1 18"/><polyline points="17 6 23 6 23 12"/></svg>`
      : `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="23 18 13.5 8.5 8.5 13.5 1 6"/><polyline points="17 18 23 18 23 12"/></svg>`;
  });

  readonly incomeIcon = `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="19" x2="12" y2="5"/><polyline points="5 12 12 5 19 12"/></svg>`;
  readonly expenseIcon = `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="5" x2="12" y2="19"/><polyline points="19 12 12 19 5 12"/></svg>`;
  readonly owedIcon    = `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>`;

  ngOnInit(): void {
    this.api.getPeriods().subscribe({
      next: periods => {
        this.periods.set(periods);
        this.loadingPeriods.set(false);
        if (periods.length > 0) {
          // Garante que o ano selecionado existe nos períodos carregados
          const years = [...new Set(periods.map(p => p.year))].sort((a, b) => b - a);
          if (!years.includes(this.selectedYear())) {
            this.selectedYear.set(years[0]);
          }
          // Seleciona o período mais recente por padrão
          this.selectPeriod(periods[0].id);
        }
      },
      error: () => this.loadingPeriods.set(false)
    });
  }

  selectPeriod(id: string): void {
    this.selectedPeriodId.set(id);
    this.loadingSummary.set(true);
    this.summary.set(null);

    this.api.getPeriodSummary(id).subscribe({
      next: s => {
        this.summary.set(s);
        this.loadingSummary.set(false);
      },
      error: () => this.loadingSummary.set(false)
    });
  }

  selectYear(year: number): void {
    this.selectedYear.set(year);
    this.selectedPeriodId.set(null);
    this.summary.set(null);
  }

  monthName(month: number): string {
    return MONTH_NAMES[month - 1].substring(0, 3);
  }
}
