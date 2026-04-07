import { Component, inject, signal, OnInit } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { HeaderComponent } from '../../../shared/components/header/header.component';
import { CurrencyBrlPipe } from '../../../shared/pipes/currency-brl.pipe';
import {
  ExpenseResponse, PeriodResponse, CategoryResponse,
  MONTH_NAMES, PAYMENT_STATUS_LABELS, FORTNIGHT_TYPE_LABELS,
  PaymentStatus, SourceType, FortnightType
} from '../../../core/models/models';

@Component({
  selector: 'app-expenses',
  standalone: true,
  imports: [HeaderComponent, CurrencyBrlPipe, ReactiveFormsModule],
  template: `
    <app-header title="Despesas" subtitle="Gerencie seus lançamentos de despesas">
      <button class="btn btn-primary btn-sm" (click)="toggleForm()">
        {{ showForm() ? 'Cancelar' : '+ Nova despesa' }}
      </button>
    </app-header>

    <div class="page-content">

      <!-- Formulário -->
      @if (showForm()) {
        <div class="card form-card">
          <h3 class="form-title">Nova despesa</h3>
          <form [formGroup]="form" (ngSubmit)="onSubmit()">
            <div class="form-grid">
              <!-- Período -->
              <div class="field">
                <label class="field-label">Período *</label>
                <select formControlName="periodId" class="input" [class.error]="showError('periodId')">
                  <option value="">Selecione</option>
                  @for (p of periods(); track p.id) {
                    <option [value]="p.id">{{ monthName(p.month) }}/{{ p.year }}</option>
                  }
                </select>
              </div>

              <!-- Categoria -->
              <div class="field">
                <label class="field-label">Categoria *</label>
                <select formControlName="categoryId" class="input" [class.error]="showError('categoryId')">
                  <option value="">Selecione</option>
                  @for (c of categories(); track c.id) {
                    <option [value]="c.id">{{ c.name }}</option>
                  }
                </select>
              </div>

              <!-- Descrição -->
              <div class="field field-wide">
                <label class="field-label">Descrição *</label>
                <input
                  formControlName="description"
                  class="input"
                  [class.error]="showError('description')"
                  placeholder="Ex: Aluguel, Internet..."
                />
              </div>

              <!-- Valor -->
              <div class="field">
                <label class="field-label">Valor (R$) *</label>
                <input
                  type="number"
                  formControlName="amount"
                  class="input"
                  [class.error]="showError('amount')"
                  placeholder="0,00"
                  step="0.01"
                  min="0.01"
                />
              </div>

              <!-- Vencimento -->
              <div class="field">
                <label class="field-label">Vencimento *</label>
                <input type="date" formControlName="dueDate" class="input" [class.error]="showError('dueDate')" />
              </div>

              <!-- Fonte -->
              <div class="field">
                <label class="field-label">Origem *</label>
                <select formControlName="sourceType" class="input">
                  <option [value]="1">Parental</option>
                  <option [value]="2">Própria</option>
                </select>
              </div>

              <!-- Quinzena -->
              <div class="field">
                <label class="field-label">Quinzena *</label>
                <select formControlName="fortnightType" class="input">
                  <option [value]="1">1ª Quinzena</option>
                  <option [value]="2">2ª Quinzena</option>
                </select>
              </div>

              <!-- Observações -->
              <div class="field field-wide">
                <label class="field-label">Observações</label>
                <input formControlName="notes" class="input" placeholder="Opcional..." />
              </div>
            </div>

            @if (apiError()) {
              <div class="form-error">{{ apiError() }}</div>
            }

            <div class="form-footer">
              <button type="button" class="btn btn-secondary" (click)="toggleForm()">Cancelar</button>
              <button type="submit" class="btn btn-primary" [disabled]="loading()">
                {{ loading() ? 'Salvando...' : 'Salvar despesa' }}
              </button>
            </div>
          </form>
        </div>
      }

      <!-- Filtro por período -->
      <div class="filter-row">
        <label class="field-label">Filtrar por período</label>
        <select class="input" style="width:auto; min-width:180px" (change)="onPeriodFilter($event)">
          <option value="">Todos</option>
          @for (p of periods(); track p.id) {
            <option [value]="p.id">{{ monthName(p.month) }}/{{ p.year }}</option>
          }
        </select>
      </div>

      <!-- Lista -->
      @if (loadingList()) {
        <div class="loading-state"><span class="spinner-lg"></span></div>
      } @else if (expenses().length === 0) {
        <div class="empty-state">
          <p>Nenhuma despesa encontrada.</p>
        </div>
      } @else {
        <div class="table-wrap card">
          <table class="table">
            <thead>
              <tr>
                <th>Descrição</th>
                <th>Categoria</th>
                <th>Quinzena</th>
                <th>Vencimento</th>
                <th>Valor</th>
                <th>Status</th>
                <th>Ações</th>
              </tr>
            </thead>
            <tbody>
              @for (expense of expenses(); track expense.id) {
                <tr>
                  <td>
                    <div class="cell-primary">{{ expense.description }}</div>
                    @if (expense.notes) {
                      <div class="cell-secondary">{{ expense.notes }}</div>
                    }
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
                  <td>
                    <div class="row-actions">
                      @if (expense.paymentStatus === PaymentStatus.Pending || expense.paymentStatus === PaymentStatus.Partial) {
                        <button class="action-btn action-success" (click)="markAsPaid(expense)" title="Marcar como pago">
                          ✓
                        </button>
                      }
                      <button class="action-btn action-danger" (click)="deleteExpense(expense.id)" title="Excluir">
                        ✕
                      </button>
                    </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    .page-content { padding: 28px; display: flex; flex-direction: column; gap: 20px; }

    .form-card { padding: 24px; }
    .form-title { font-size: 1rem; margin-bottom: 20px; }

    .form-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 16px;
    }

    @media (max-width: 960px) { .form-grid { grid-template-columns: repeat(2, 1fr); } }
    @media (max-width: 640px) { .form-grid { grid-template-columns: 1fr; } }

    .field { display: flex; flex-direction: column; gap: 6px; }
    .field-wide { grid-column: span 2; }
    .field-label { font-size: 0.875rem; font-weight: 500; color: var(--ink2); }

    .form-error {
      margin-top: 16px;
      padding: 10px 14px;
      background: var(--color-danger-bg);
      color: var(--color-danger);
      border-radius: var(--radius);
      font-size: 0.875rem;
    }

    .form-footer {
      display: flex;
      justify-content: flex-end;
      gap: 10px;
      margin-top: 20px;
    }

    .filter-row {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .table-wrap { overflow-x: auto; }
    .table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
    .table th {
      text-align: left; padding: 12px 16px;
      font-size: 0.75rem; font-weight: 600;
      text-transform: uppercase; letter-spacing: 0.04em;
      color: var(--ink3); border-bottom: 1px solid var(--border);
    }
    .table td {
      padding: 12px 16px;
      border-bottom: 1px solid var(--bg3);
      color: var(--ink2); vertical-align: middle;
    }
    .table tr:last-child td { border-bottom: none; }
    .table tbody tr:hover { background: var(--surface-overlay); }

    .cell-primary   { color: var(--ink); font-weight: 500; }
    .cell-secondary { font-size: 0.75rem; color: var(--ink3); margin-top: 2px; }
    .category-dot   { display: inline-block; width: 8px; height: 8px; border-radius: 50%; margin-right: 6px; }
    .text-sm        { font-size: 0.8125rem; }
    .font-semibold  { font-weight: 600; color: var(--ink); }

    .row-actions { display: flex; gap: 4px; }
    .action-btn {
      width: 28px; height: 28px;
      border: 1px solid var(--border);
      background: none; border-radius: var(--radius-sm);
      cursor: pointer; font-size: 12px;
      display: flex; align-items: center; justify-content: center;
      transition: all var(--transition);
    }
    .action-success:hover { background: var(--color-success-bg); border-color: var(--sage2); color: var(--sage2); }
    .action-danger:hover  { background: var(--color-danger-bg);  border-color: var(--rust);  color: var(--rust);  }

    .loading-state, .empty-state {
      display: flex; flex-direction: column;
      align-items: center; gap: 16px; padding: 64px; color: var(--ink3);
    }

    .spinner-lg {
      width: 32px; height: 32px;
      border: 3px solid var(--border);
      border-top-color: var(--sage2);
      border-radius: 50%;
      animation: spin .8s linear infinite; display: block;
    }

    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class ExpensesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb  = inject(FormBuilder);

  readonly PaymentStatus = PaymentStatus;

  readonly periods    = signal<PeriodResponse[]>([]);
  readonly categories = signal<CategoryResponse[]>([]);
  readonly expenses   = signal<ExpenseResponse[]>([]);
  readonly showForm   = signal(false);
  readonly loading    = signal(false);
  readonly loadingList = signal(false);
  readonly apiError   = signal<string | null>(null);

  private selectedPeriodId: string | null = null;

  readonly form = this.fb.group({
    periodId:      ['', Validators.required],
    categoryId:    ['', Validators.required],
    description:   ['', Validators.required],
    amount:        [null as number | null, [Validators.required, Validators.min(0.01)]],
    dueDate:       ['', Validators.required],
    sourceType:    [SourceType.Personal],
    fortnightType: [FortnightType.First],
    notes:         [''],
  });

  ngOnInit(): void {
    this.api.getPeriods().subscribe(p => this.periods.set(p));
    this.api.getCategories().subscribe(c => this.categories.set(c));
  }

  toggleForm(): void {
    this.showForm.update(v => !v);
    this.apiError.set(null);
  }

  showError(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  onPeriodFilter(event: Event): void {
    const periodId = (event.target as HTMLSelectElement).value;
    this.selectedPeriodId = periodId || null;
    if (periodId) {
      this.loadingList.set(true);
      this.api.getExpensesByPeriod(periodId).subscribe({
        next: e => { this.expenses.set(e); this.loadingList.set(false); },
        error: () => this.loadingList.set(false)
      });
    } else {
      this.expenses.set([]);
    }
  }

  onSubmit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    this.loading.set(true);
    this.apiError.set(null);

    const v = this.form.getRawValue();
    this.api.createExpense({
      periodId:      v.periodId!,
      categoryId:    v.categoryId!,
      description:   v.description!,
      amount:        v.amount!,
      dueDate:       v.dueDate!,
      sourceType:    Number(v.sourceType) as SourceType,
      fortnightType: Number(v.fortnightType) as FortnightType,
      notes:         v.notes || undefined,
    }).subscribe({
      next: e => {
        if (this.selectedPeriodId === e.periodId) {
          this.expenses.update(list => [e, ...list]);
        }
        this.form.reset({ sourceType: SourceType.Personal, fortnightType: FortnightType.First });
        this.showForm.set(false);
        this.loading.set(false);
      },
      error: err => {
        this.apiError.set(err.error?.message ?? 'Erro ao salvar despesa.');
        this.loading.set(false);
      }
    });
  }

  markAsPaid(expense: ExpenseResponse): void {
    const today = new Date().toISOString().split('T')[0];
    this.api.markExpenseAsPaid(expense.id, { paymentDate: today }).subscribe({
      next: () => {
        this.expenses.update(list =>
          list.map(e => e.id === expense.id
            ? { ...e, paymentStatus: PaymentStatus.Paid, paymentDate: today }
            : e
          )
        );
      }
    });
  }

  deleteExpense(id: string): void {
    if (!confirm('Confirma a exclusão desta despesa?')) return;
    this.api.deleteExpense(id).subscribe({
      next: () => this.expenses.update(list => list.filter(e => e.id !== id))
    });
  }

  categoryName(categoryId: string): string {
    return this.categories().find(c => c.id === categoryId)?.name ?? '—';
  }

  categoryColor(categoryId: string): string {
    return this.categories().find(c => c.id === categoryId)?.color ?? '#c8bfaf';
  }

  statusLabel(status: PaymentStatus): string     { return PAYMENT_STATUS_LABELS[status]; }
  fortnightLabel(type: FortnightType): string     { return FORTNIGHT_TYPE_LABELS[type];  }
  monthName(month: number): string               { return MONTH_NAMES[month - 1].substring(0, 3); }
  formatDate(d: string): string                  { return new Date(d).toLocaleDateString('pt-BR'); }

  statusBadgeClass(status: PaymentStatus): string {
    const map: Record<PaymentStatus, string> = {
      [PaymentStatus.Pending]:   'badge-warning',
      [PaymentStatus.Paid]:      'badge-success',
      [PaymentStatus.Cancelled]: 'badge-neutral',
      [PaymentStatus.Partial]:   'badge-info',
    };
    return map[status] ?? 'badge-neutral';
  }
}
