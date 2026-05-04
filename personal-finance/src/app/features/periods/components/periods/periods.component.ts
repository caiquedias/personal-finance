import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { trigger, style, animate, transition } from '@angular/animations';
import { ApiService } from '../../../../core/services/api.service';
import { HeaderComponent } from '../../../../shared/components/header/header.component';
import { RecurringExpensesModalComponent } from '../../../../shared/components/modal/recurring-expenses-modal/recurring-expenses-modal.component';
import { FilterModalComponent } from '../../../../shared/components/filter-modal/filter-modal.component';
import { FilterButtonComponent } from '../../../../shared/components/filter-modal/filter-button.component';
import { FilterFieldConfig } from '../../../../shared/components/filter-modal/filter-field-config';
import { PeriodResponse, RecurringExpenseResponse, MONTH_NAMES } from '../../../../core/models/models';

@Component({
  selector: 'app-periods',
  standalone: true,
  imports: [HeaderComponent, RouterLink, ReactiveFormsModule, RecurringExpensesModalComponent, FilterModalComponent, FilterButtonComponent],
  templateUrl: './periods.component.html',
  styleUrls: ['./periods.component.css'],
  animations: [
    trigger('backdropAnim', [
      transition(':enter', [style({ opacity: 0 }), animate('200ms ease', style({ opacity: 1 }))]),
      transition(':leave', [animate('180ms ease', style({ opacity: 0 }))])
    ]),
    trigger('modalAnim', [
      transition(':enter', [
        style({ opacity: 0, transform: 'scale(0.88) translateY(-16px)' }),
        animate('260ms cubic-bezier(0.34,1.56,0.64,1)', style({ opacity: 1, transform: 'scale(1) translateY(0)' }))
      ]),
      transition(':leave', [
        animate('180ms ease-in', style({ opacity: 0, transform: 'scale(0.93) translateY(10px)' }))
      ])
    ])
  ]
})
export class PeriodsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb  = inject(FormBuilder);

  // ── Dados brutos ──────────────────────────────────────────────────────────
  readonly periods       = signal<PeriodResponse[]>([]);
  readonly showForm      = signal(false);
  readonly loading       = signal(false);
  readonly loadingList   = signal(true);
  readonly apiError      = signal<string | null>(null);
  readonly actionLoading = signal<string | null>(null);
  readonly actionError   = signal<string | null>(null);

  // ── Modal de despesas recorrentes ─────────────────────────────────────────
  readonly recurringModalOpen     = signal(false);
  readonly recurringExpenses      = signal<RecurringExpenseResponse[]>([]);
  readonly recurringTargetPeriodId = signal<string | null>(null);
  readonly recurringLoading       = signal(false);

  // ── Filtros ───────────────────────────────────────────────────────────────
  readonly selectedYear = signal<number | null>(new Date().getFullYear());
  readonly monthFrom    = signal<number | null>(null);
  readonly monthTo      = signal<number | null>(null);
  readonly filterStatus = signal<'all' | 'active' | 'inactive'>('all');
  readonly filterOpen   = signal(false);

  // ── Paginação ─────────────────────────────────────────────────────────────
  readonly currentPage = signal(1);
  readonly pageSize    = 12;

  // ── Computed ──────────────────────────────────────────────────────────────
  readonly availableYears = computed(() =>
    [...new Set(this.periods().map(p => p.year))].sort((a, b) => b - a)
  );

  readonly filteredPeriods = computed(() => {
    let result = this.periods();
    const year = this.selectedYear();
    const from = this.monthFrom();
    const to   = this.monthTo();
    const st   = this.filterStatus();

    if (year !== null)     result = result.filter(p => p.year === year);
    if (from !== null)     result = result.filter(p => p.month >= from);
    if (to   !== null)     result = result.filter(p => p.month <= to);
    if (st === 'active')   result = result.filter(p => p.isActive);
    if (st === 'inactive') result = result.filter(p => !p.isActive);

    return result;
  });

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredPeriods().length / this.pageSize))
  );

  readonly paginatedPeriods = computed(() => {
    const page  = Math.min(this.currentPage(), this.totalPages());
    const start = (page - 1) * this.pageSize;
    return this.filteredPeriods().slice(start, start + this.pageSize);
  });

  readonly pageNumbers = computed(() =>
    Array.from({ length: this.totalPages() }, (_, i) => i + 1)
  );

  readonly activeFilterCount = computed(() => {
    let count = 0;
    if (this.selectedYear() !== new Date().getFullYear()) count++;
    if (this.monthFrom() !== null) count++;
    if (this.monthTo()   !== null) count++;
    if (this.filterStatus() !== 'all') count++;
    return count;
  });

  readonly filterFields = computed<FilterFieldConfig[]>(() => [
    {
      key: 'year', label: 'Ano', type: 'select',
      value: this.selectedYear() ?? '',
      options: [
        { value: '', label: 'Todos' },
        ...this.availableYears().map(y => ({ value: y, label: String(y) }))
      ]
    },
    {
      key: 'monthFrom', label: 'De', type: 'select',
      value: this.monthFrom() ?? '',
      options: [
        { value: '', label: '--' },
        ...this.monthsShort.map(m => ({ value: m.value, label: m.label }))
      ]
    },
    {
      key: 'monthTo', label: 'Até', type: 'select',
      value: this.monthTo() ?? '',
      options: [
        { value: '', label: '--' },
        ...this.monthsShort.map(m => ({ value: m.value, label: m.label }))
      ]
    },
    {
      key: 'status', label: 'Status', type: 'select',
      value: this.filterStatus(),
      options: [
        { value: 'all',      label: 'Todos'   },
        { value: 'active',   label: 'Ativo'   },
        { value: 'inactive', label: 'Inativo' }
      ]
    }
  ]);

  // ── Constantes ────────────────────────────────────────────────────────────
  readonly monthNames  = MONTH_NAMES;
  readonly monthsShort = MONTH_NAMES.map((label, i) => ({ value: i + 1, label }));

  readonly form = this.fb.group({
    month: [new Date().getMonth() + 1, Validators.required],
    year:  [new Date().getFullYear(), [Validators.required, Validators.min(2000), Validators.max(2099)]],
  });

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  ngOnInit(): void {
    this.loadPeriods();
  }

  // ── Setores de filtro (reset automático de página) ────────────────────────
  setYear(v: string): void      { this.selectedYear.set(v ? +v : null); this.currentPage.set(1); }
  setMonthFrom(v: string): void { this.monthFrom.set(v ? +v : null);   this.currentPage.set(1); }
  setMonthTo(v: string): void   { this.monthTo.set(v ? +v : null);     this.currentPage.set(1); }
  setStatus(v: string): void    { this.filterStatus.set(v as 'all' | 'active' | 'inactive'); this.currentPage.set(1); }

  clearFilters(): void {
    this.selectedYear.set(new Date().getFullYear());
    this.monthFrom.set(null);
    this.monthTo.set(null);
    this.filterStatus.set('all');
    this.currentPage.set(1);
  }

  onApplyFilters(values: Record<string, unknown>): void {
    this.setYear(String(values['year'] ?? ''));
    this.setMonthFrom(String(values['monthFrom'] ?? ''));
    this.setMonthTo(String(values['monthTo'] ?? ''));
    this.setStatus(String(values['status'] ?? 'all'));
  }

  onClearFilters(): void {
    this.clearFilters();
  }

  // ── Dados ─────────────────────────────────────────────────────────────────
  loadPeriods(): void {
    this.loadingList.set(true);
    this.api.getPeriods().subscribe({
      next: p => { this.periods.set(p); this.loadingList.set(false); },
      error: () => this.loadingList.set(false)
    });
  }

  showError(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  onSubmit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    this.loading.set(true);
    this.apiError.set(null);
    const { year, month } = this.form.getRawValue();

    this.api.createPeriod({ year: year!, month: month! }).subscribe({
      next: p => {
        this.periods.update(list => [p, ...list]);
        this.cancelForm();
        this.loading.set(false);
        this.openRecurringModal(p.id);
      },
      error: err => {
        this.apiError.set(err.error?.message ?? 'Erro ao criar período.');
        this.loading.set(false);
      }
    });
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.apiError.set(null);
    this.form.reset({
      month: new Date().getMonth() + 1,
      year:  new Date().getFullYear()
    });
  }

  toggleActive(period: PeriodResponse): void {
    this.actionLoading.set(`${period.id}-toggle`);
    this.actionError.set(null);

    this.api.togglePeriodActive(period.id).subscribe({
      next: () => {
        this.periods.update(list =>
          list.map(p => p.id === period.id
            ? { ...p, isActive: !p.isActive }
            : p
          )
        );
        this.actionLoading.set(null);
      },
      error: err => {
        this.actionError.set(err.error?.message ?? 'Erro ao alterar status do período.');
        this.actionLoading.set(null);
      }
    });
  }

  deletePeriod(period: PeriodResponse): void {
    const label = `${this.monthName(period.month)}/${period.year}`;
    if (!confirm(`Excluir o período ${label}? Esta ação não pode ser desfeita.`)) return;

    this.actionLoading.set(`${period.id}-delete`);
    this.actionError.set(null);

    this.api.deletePeriod(period.id).subscribe({
      next: () => {
        this.periods.update(list => list.filter(p => p.id !== period.id));
        this.actionLoading.set(null);
      },
      error: err => {
        this.actionError.set(err.error?.message ?? 'Erro ao excluir período.');
        this.actionLoading.set(null);
      }
    });
  }

  monthName(month: number): string {
    return MONTH_NAMES[month - 1];
  }

  openRecurringModal(periodId: string): void {
    this.api.getRecurringExpenses(periodId).subscribe({
      next: expenses => {
        if (expenses.length === 0) return;
        this.recurringExpenses.set(expenses);
        this.recurringTargetPeriodId.set(periodId);
        this.recurringModalOpen.set(true);
      }
    });
  }

  closeRecurringModal(): void {
    this.recurringModalOpen.set(false);
    this.recurringExpenses.set([]);
    this.recurringTargetPeriodId.set(null);
  }

  onReplicateExpenses(expenseIds: string[]): void {
    const periodId = this.recurringTargetPeriodId();
    if (!periodId) return;

    this.recurringLoading.set(true);
    this.api.replicateExpenses(periodId, expenseIds).subscribe({
      next: () => {
        this.recurringLoading.set(false);
        this.closeRecurringModal();
      },
      error: () => this.recurringLoading.set(false)
    });
  }
}
