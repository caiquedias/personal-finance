import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ApiService } from '../../../../core/services/api.service';
import { HeaderComponent } from '../../../../shared/components/header/header.component';
import { PeriodResponse, MONTH_NAMES } from '../../../../core/models/models';

@Component({
  selector: 'app-periods',
  standalone: true,
  imports: [HeaderComponent, RouterLink, ReactiveFormsModule],
  templateUrl: './periods.component.html',
  styleUrls: ['./periods.component.css'],
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

  // ── Filtros ───────────────────────────────────────────────────────────────
  readonly selectedYear = signal<number | null>(new Date().getFullYear());
  readonly monthFrom    = signal<number | null>(null);
  readonly monthTo      = signal<number | null>(null);
  readonly filterStatus = signal<'all' | 'active' | 'inactive'>('all');

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
}
