import { Component, inject, signal, computed, OnInit, effect, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { NgxEchartsDirective } from 'ngx-echarts';
import type { EChartsOption } from 'echarts';
import { ApiService } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { ThemeService } from '../../../../core/services/theme.service';
import { HeaderComponent } from '../../../../shared/components/header/header.component';
import { StatCardComponent } from '../../../../shared/components/stat-card/stat-card.component';
import { CurrencyBrlPipe } from '../../../../shared/pipes/currency-brl.pipe';
import { PeriodResponse, PeriodSummary, MONTH_NAMES, MONTH_NAMES as MONTHS, PaymentStatus, ExpensesReport } from '../../../../core/models/models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [HeaderComponent, StatCardComponent, CurrencyBrlPipe, DecimalPipe, RouterLink, NgxEchartsDirective],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css'],
})
export class DashboardComponent implements OnInit {
  private readonly api   = inject(ApiService);
  private readonly auth  = inject(AuthService);
  private readonly theme = inject(ThemeService);

  readonly MONTH_NAMES = MONTH_NAMES;

  readonly periods          = signal<PeriodResponse[]>([]);
  readonly selectedYear     = signal<number>(new Date().getFullYear());
  readonly selectedPeriodId = signal<string | null>(null);
  readonly summary          = signal<PeriodSummary | null>(null);
  readonly loadingPeriods   = signal(true);
  readonly loadingSummary   = signal(false);

  readonly selectedMonth  = signal<number>(new Date().getMonth() + 1);
  readonly loadingChart1  = signal(false);
  readonly loadingChart2  = signal(false);
  readonly chart1Options  = signal<EChartsOption | null>(null);
  readonly chart2Options  = signal<EChartsOption | null>(null);

  private readonly chartTextColor = computed(() => this.theme.isDark() ? '#ccc' : '#444');

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

  constructor() {
    // Reconstrói os gráficos ao trocar de tema para aplicar cor de texto correta.
    // untracked() evita dependência nos signals de opções, prevenindo loop.
    effect(() => {
      this.theme.isDark(); // única dependência reativa: mudança de tema
      untracked(() => {
        if (this.chart1Options()) this.loadChart1(this.selectedYear());
        if (this.chart2Options()) this.loadChart2(this.selectedYear(), this.selectedMonth());
      });
    });
  }

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
          // Carrega gráficos com o ano selecionado
          this.loadCharts(this.selectedYear(), this.selectedMonth());
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
    this.loadCharts(year, this.selectedMonth());
  }

  selectMonth(month: number): void {
    this.selectedMonth.set(month);
    this.loadChart2(this.selectedYear(), month);
  }

  monthName(month: number): string {
    return MONTH_NAMES[month - 1].substring(0, 3);
  }

  private loadCharts(year: number, month: number): void {
    this.loadChart1(year);
    this.loadChart2(year, month);
  }

  private loadChart1(year: number): void {
    this.loadingChart1.set(true);
    this.chart1Options.set(null);
    this.api.getExpensesReport(year).subscribe({
      next: report => {
        this.chart1Options.set(this.buildPieOptions(report));
        this.loadingChart1.set(false);
      },
      error: () => this.loadingChart1.set(false),
    });
  }

  private loadChart2(year: number, month: number): void {
    this.loadingChart2.set(true);
    this.chart2Options.set(null);
    this.api.getExpensesReport(year, month).subscribe({
      next: report => {
        this.chart2Options.set(this.buildPieOptions(report));
        this.loadingChart2.set(false);
      },
      error: () => this.loadingChart2.set(false),
    });
  }

  private buildPieOptions(report: ExpensesReport): EChartsOption | null {
    if (!report.items.length) return null;
    const textColor = this.chartTextColor();
    return {
      tooltip: {
        trigger: 'item',
        formatter: (params: any) =>
          `${params.name}<br/><b>R$ ${params.value.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</b> (${params.percent}%)`,
      },
      legend: {
        orient: 'vertical',
        right: 10,
        top: 'center',
        textStyle: { fontSize: 12, color: textColor },
      },
      series: [{
        type: 'pie',
        radius: ['40%', '70%'],
        center: ['38%', '50%'],
        avoidLabelOverlap: false,
        label: { show: false },
        emphasis: {
          label: { show: true, fontSize: 13, fontWeight: 'bold', color: textColor },
        },
        data: report.items.map(i => ({
          name: i.categoryName,
          value: i.total,
          itemStyle: { color: i.categoryColor },
        })),
      }],
    };
  }
}
