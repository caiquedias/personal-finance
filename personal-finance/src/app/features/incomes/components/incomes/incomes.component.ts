import {
  Component, inject, signal, computed, OnInit,
  ViewChild, ElementRef, input
} from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { trigger, style, animate, transition } from '@angular/animations';
import { ApiService } from '../../../../core/services/api.service';
import { HeaderComponent } from '../../../../shared/components/header/header.component';
import { SonicModalComponent } from '../../../../shared/components/modal/sonic-modal/sonic-modal.component';
import { PaginationComponent } from '../../../../shared/components/pagination/pagination.component';
import { CurrencyBrlPipe } from '../../../../shared/pipes/currency-brl.pipe';
import { FilterModalComponent } from '../../../../shared/components/filter-modal/filter-modal.component';
import { FilterButtonComponent } from '../../../../shared/components/filter-modal/filter-button.component';
import { FilterFieldConfig } from '../../../../shared/components/filter-modal/filter-field-config';
import {
  IncomeResponse, PeriodResponse,
  MONTH_NAMES, FORTNIGHT_TYPE_LABELS, FortnightType
} from '../../../../core/models/models';

type SortCol = 'description' | 'fortnightType' | 'receivedAt' | 'amount';

@Component({
  selector: 'app-incomes',
  standalone: true,
  imports: [
    HeaderComponent, CurrencyBrlPipe, ReactiveFormsModule,
    SonicModalComponent, PaginationComponent, FilterModalComponent, FilterButtonComponent
  ],
  templateUrl: './incomes.component.html',
  styleUrls: ['./incomes.component.css'],
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
})
export class IncomesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb  = inject(FormBuilder);

  readonly FortnightType = FortnightType;

  readonly periods = signal<PeriodResponse[]>([]);

  // Dados vindos da API (página atual)
  readonly incomes     = signal<IncomeResponse[]>([]);
  readonly totalCount  = signal(0);
  readonly currentPage = signal(1);
  readonly pageSize    = signal(20);

  // Filtros externos (server-side)
  readonly filterDescription   = signal('');
  readonly filterFortnightType = signal<FortnightType | null>(null);

  // Ordenação client-side (página atual)
  readonly sortCol = signal<SortCol | null>(null);
  readonly sortDir = signal<'asc' | 'desc'>('asc');

  readonly loadingList = signal(false);
  readonly saving      = signal(false);
  readonly apiError    = signal<string | null>(null);
  readonly modalOpen   = signal(false);
  readonly modalMode   = signal<'create' | 'edit'>('create');

  // Ring animation — campo de valor
  private _ringId = 0;
  readonly rings    = signal<{ id: number; x: number }[]>([]);
  readonly sparkles = signal<{ id: number; x: number; y: string }[]>([]);

  incrementAmount(): void {
    const cur = this.form.get('amount')?.value ?? 0;
    this.form.get('amount')?.setValue(Math.round((cur + 10) * 100) / 100);
    this._spawnRing();
  }

  decrementAmount(): void {
    const cur = this.form.get('amount')?.value ?? 0;
    const next = Math.max(0, Math.round((cur - 10) * 100) / 100);
    this.form.get('amount')?.setValue(next);
    this._spawnRing();
  }

  private _spawnRing(): void {
    const id = ++this._ringId;
    const x  = 20 + Math.random() * 140;
    this.rings.update(r => [...r, { id, x }]);
    this.sparkles.update(s => [
      ...s,
      { id: id * 100 + 1, x: x - 10 + Math.random() * 20, y: '30%' },
      { id: id * 100 + 2, x: x - 20 + Math.random() * 40, y: '60%' },
    ]);
    setTimeout(() => this.rings.update(r => r.filter(ring => ring.id !== id)), 900);
    setTimeout(() => this.sparkles.update(s => s.filter(sp => sp.id !== id * 100 + 1 && sp.id !== id * 100 + 2)), 500);
  }

  private editingId: string | null = null;
  selectedPeriodId: string | null = null;

  /** Pré-seleção via query param: /incomes?periodId=xxx */
  readonly periodId = input<string>();
  @ViewChild('periodFilterSelect') periodFilterSelect!: ElementRef<HTMLSelectElement>;

  readonly filterOpen = signal(false);

  readonly activeFilterCount = computed(() =>
    (this.filterDescription() ? 1 : 0) +
    (this.filterFortnightType() != null ? 1 : 0)
  );

  readonly filterFields = computed<FilterFieldConfig[]>(() => [
    { key: 'description',   label: 'Descrição', type: 'text',   value: this.filterDescription() },
    { key: 'fortnightType', label: 'Quinzena',   type: 'select',
      options: [{ value: '', label: 'Ambas' }, { value: FortnightType.First, label: '1ª Quinzena' }, { value: FortnightType.Second, label: '2ª Quinzena' }],
      value: this.filterFortnightType() ?? '' },
  ]);

  readonly displayedIncomes = computed<IncomeResponse[]>(() => {
    const col = this.sortCol();
    const dir = this.sortDir();
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

  readonly form = this.fb.group({
    periodId:      ['', Validators.required],
    fortnightType: [FortnightType.First],
    description:   ['', Validators.required],
    amount:        [null as number | null, [Validators.required, Validators.min(0.01)]],
    receivedAt:    ['', Validators.required],
    notes:         [''],
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
  }

  private loadPage(): void {
    if (!this.selectedPeriodId) return;
    this.loadingList.set(true);
    this.api.getIncomesByPeriod(this.selectedPeriodId, {
      pageNumber:    this.currentPage(),
      pageSize:      this.pageSize(),
      description:   this.filterDescription() || undefined,
      fortnightType: this.filterFortnightType() ?? undefined,
    }).subscribe({
      next: result => {
        this.incomes.set(result.items);
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
    this.form.reset({ periodId: this.selectedPeriodId ?? '', fortnightType: FortnightType.First });
    this.modalOpen.set(true);
  }

  openEditModal(income: IncomeResponse): void {
    this.editingId = income.id;
    this.modalMode.set('edit');
    this.apiError.set(null);
    this.form.patchValue({
      periodId:      income.periodId,
      fortnightType: income.fortnightType,
      description:   income.description,
      amount:        income.amount,
      receivedAt:    income.receivedAt.split('T')[0],
      notes:         income.notes ?? '',
    });
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.modalOpen.set(false);
    this.editingId = null;
    this.apiError.set(null);
  }

  onSubmit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.saving.set(true);
    this.apiError.set(null);

    const v = this.form.getRawValue();
    const payload = {
      periodId:      v.periodId!,
      fortnightType: Number(v.fortnightType) as FortnightType,
      description:   v.description!,
      amount:        v.amount!,
      receivedAt:    v.receivedAt!,
      notes:         v.notes || undefined,
    };

    if (this.modalMode() === 'create') {
      this.api.createIncome(payload).subscribe({
        next: () => {
          this.closeModal();
          this.saving.set(false);
          this.loadPage();
        },
        error: err => { this.apiError.set(err.error?.message ?? 'Erro ao criar receita.'); this.saving.set(false); }
      });
    } else {
      const id = this.editingId!;
      this.api.deleteIncome(id).subscribe({
        next: () => {
          this.api.createIncome(payload).subscribe({
            next: () => {
              this.closeModal();
              this.saving.set(false);
              this.loadPage();
            },
            error: err => {
              this.apiError.set(err.error?.message ?? 'Erro ao salvar alterações.');
              this.saving.set(false);
              this.loadPage();
            }
          });
        },
        error: err => { this.apiError.set(err.error?.message ?? 'Erro ao atualizar receita.'); this.saving.set(false); }
      });
    }
  }

  deleteIncome(id: string): void {
    if (!confirm('Confirma a exclusão desta receita?')) return;
    this.api.deleteIncome(id).subscribe({
      next: () => this.loadPage()
    });
  }

  onPeriodFilter(event: Event): void {
    const periodId = (event.target as HTMLSelectElement).value;
    this.selectedPeriodId = periodId || null;
    this.currentPage.set(1);
    this.incomes.set([]);
    this.totalCount.set(0);
    if (periodId) this.loadPage();
  }

  // ── Filtros externos ──────────────────────────────────────────────────────

  onApplyFilters(values: Record<string, unknown>): void {
    this.filterDescription.set((values['description'] as string) || '');
    const fortnight = values['fortnightType'];
    this.filterFortnightType.set(fortnight !== '' && fortnight != null ? Number(fortnight) as FortnightType : null);
    this.currentPage.set(1);
    this.loadPage();
  }

  onClearFilters(): void {
    this.filterDescription.set('');
    this.filterFortnightType.set(null);
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
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortCol.set(col);
      this.sortDir.set('asc');
    }
  }

  sortIndicator(col: SortCol): string {
    if (this.sortCol() !== col) return '';
    return this.sortDir() === 'asc' ? '↑' : '↓';
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  showError(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  fortnightLabel(type: FortnightType): string { return FORTNIGHT_TYPE_LABELS[type]; }
  monthName(month: number): string            { return MONTH_NAMES[month - 1].substring(0, 3); }
  formatDate(d: string): string               { return new Date(d).toLocaleDateString('pt-BR'); }
}
