import { Component, inject, signal, computed, OnInit, effect, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { CdkDragDrop, CdkDropList, CdkDrag, moveItemInArray } from '@angular/cdk/drag-drop';
import { NgxEchartsDirective } from 'ngx-echarts';
import type { EChartsOption } from 'echarts';
import { ApiService } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { ThemeService } from '../../../../core/services/theme.service';
import { HeaderComponent } from '../../../../shared/components/header/header.component';
import { CurrencyBrlPipe } from '../../../../shared/pipes/currency-brl.pipe';
import { PeriodResponse, PeriodSummary, MONTH_NAMES, PaymentStatus, ExpensesReport } from '../../../../core/models/models';

export interface DashWidget {
  id: string;
  label: string;
}

const DEFAULT_WIDGETS: DashWidget[] = [
  { id: 'fortnight', label: 'Por quinzena'          },
  { id: 'payment',   label: 'Status de pagamento'   },
  { id: 'actions',   label: 'Ações rápidas'          },
  { id: 'chart1',    label: 'Despesas anuais'        },
  { id: 'chart2',    label: 'Despesas mensais'       },
];

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    HeaderComponent, CurrencyBrlPipe, DecimalPipe, RouterLink,
    NgxEchartsDirective, CdkDropList, CdkDrag,
  ],
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

  readonly widgets = signal<DashWidget[]>([...DEFAULT_WIDGETS]);

  private readonly chartTextColor = computed(() => this.theme.isDark() ? '#F0E8DC' : '#3a2e22');

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

  readonly balancePositive = computed(() => (this.summary()?.balance ?? 0) >= 0);

  /** Janela fixa de até 3 meses para o segmented do chart2. Não muda ao clicar. */
  readonly chart2AvailableMonths = computed(() => {
    const today = new Date().getMonth() + 1; // 1–12
    return [today - 2, today - 1, today].filter(m => m >= 1);
  });

  constructor() {
    effect(() => {
      this.theme.isDark();
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
          const years = [...new Set(periods.map(p => p.year))].sort((a, b) => b - a);
          if (!years.includes(this.selectedYear())) this.selectedYear.set(years[0]);
          this.selectPeriod(periods[0].id);
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
      next: s  => { this.summary.set(s);  this.loadingSummary.set(false); },
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

  dropWidget(event: CdkDragDrop<DashWidget[]>): void {
    const list = [...this.widgets()];
    moveItemInArray(list, event.previousIndex, event.currentIndex);
    this.widgets.set(list);
  }

  private loadCharts(year: number, month: number): void {
    this.loadChart1(year);
    this.loadChart2(year, month);
  }

  private loadChart1(year: number): void {
    this.loadingChart1.set(true);
    this.chart1Options.set(null);
    this.api.getExpensesReport(year).subscribe({
      next:  report => { this.chart1Options.set(this.buildDonutOptions(report)); this.loadingChart1.set(false); },
      error: ()     => this.loadingChart1.set(false),
    });
  }

  private loadChart2(year: number, month: number): void {
    this.loadingChart2.set(true);
    this.chart2Options.set(null);
    this.api.getExpensesReport(year, month).subscribe({
      next:  report => { this.chart2Options.set(this.buildDonutOptions(report)); this.loadingChart2.set(false); },
      error: ()     => this.loadingChart2.set(false),
    });
  }

  private buildDonutOptions(report: ExpensesReport): EChartsOption | null {
    if (!report.items.length) return null;
    const textColor = this.chartTextColor();
    const total = report.items.reduce((sum, i) => sum + i.total, 0);
    return {
      tooltip: {
        trigger: 'item',
        backgroundColor: 'rgba(24,19,16,0.92)',
        borderColor: 'transparent',
        borderRadius: 12,
        padding: 11,
        textStyle: { color: '#F0E8DC', fontSize: 12 },
        formatter: (params: any) =>
          `${params.name}<br/><b>R$ ${params.value.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</b> (${params.percent}%)`,
      },
      legend: {
        orient: 'vertical',
        right: 4,
        top: 'center',
        textStyle: { fontSize: 11, color: textColor },
        icon: 'circle',
        itemWidth: 8,
        itemHeight: 8,
        itemGap: 10,
      },
      graphic: [{
        type: 'group',
        left: 'center',
        top: 'center',
        children: [
          {
            type: 'text',
            style: {
              text: `R$ ${total.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}`,
              textAlign: 'center',
              fill: textColor,
              fontSize: 14,
              fontFamily: 'DM Serif Display, serif',
              fontWeight: '500',
            },
            top: -12,
          } as any,
          {
            type: 'text',
            style: {
              text: 'total',
              textAlign: 'center',
              fill: this.theme.isDark() ? '#9A8E81' : '#8a7a68',
              fontSize: 10,
            },
            top: 10,
          } as any,
        ],
      }],
      series: [{
        type: 'pie',
        radius: ['54%', '74%'],
        center: ['36%', '50%'],
        avoidLabelOverlap: false,
        label: { show: false },
        emphasis: {
          label: { show: false },
          scaleSize: 4,
        },
        animationType: 'expansion',
        animationDuration: 950,
        animationEasing: 'cubicInOut',
        data: report.items.map(i => ({
          name: i.categoryName,
          value: i.total,
          itemStyle: { color: i.categoryColor, borderRadius: 4 },
        })),
      }],
    };
  }
}
