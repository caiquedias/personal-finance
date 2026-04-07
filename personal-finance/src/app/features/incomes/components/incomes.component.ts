import { Component, inject, signal, OnInit } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { HeaderComponent } from '../../../shared/components/header/header.component';
import { CurrencyBrlPipe } from '../../../shared/pipes/currency-brl.pipe';
import {
  IncomeResponse, PeriodResponse,
  MONTH_NAMES, FORTNIGHT_TYPE_LABELS, FortnightType
} from '../../../core/models/models';

@Component({
  selector: 'app-incomes',
  standalone: true,
  imports: [HeaderComponent, CurrencyBrlPipe, ReactiveFormsModule],
  template: `
    <app-header title="Receitas" subtitle="Gerencie seus lançamentos de receitas">
      <button class="btn btn-primary btn-sm" (click)="toggleForm()">
        {{ showForm() ? 'Cancelar' : '+ Nova receita' }}
      </button>
    </app-header>

    <div class="page-content">

      <!-- Formulário -->
      @if (showForm()) {
        <div class="card form-card">
          <h3 class="form-title">Nova receita</h3>
          <form [formGroup]="form" (ngSubmit)="onSubmit()">
            <div class="form-grid">
              <div class="field">
                <label class="field-label">Período *</label>
                <select formControlName="periodId" class="input" [class.error]="showError('periodId')">
                  <option value="">Selecione</option>
                  @for (p of periods(); track p.id) {
                    <option [value]="p.id">{{ monthName(p.month) }}/{{ p.year }}</option>
                  }
                </select>
              </div>

              <div class="field">
                <label class="field-label">Quinzena *</label>
                <select formControlName="fortnightType" class="input">
                  <option [value]="1">1ª Quinzena</option>
                  <option [value]="2">2ª Quinzena</option>
                </select>
              </div>

              <div class="field field-wide">
                <label class="field-label">Descrição *</label>
                <input
                  formControlName="description"
                  class="input"
                  [class.error]="showError('description')"
                  placeholder="Ex: Adiantamento MDS, Saldo..."
                />
              </div>

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

              <div class="field">
                <label class="field-label">Recebido em *</label>
                <input type="date" formControlName="receivedAt" class="input" [class.error]="showError('receivedAt')" />
              </div>

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
                {{ loading() ? 'Salvando...' : 'Salvar receita' }}
              </button>
            </div>
          </form>
        </div>
      }

      <!-- Filtro -->
      <div class="filter-row">
        <label class="field-label">Filtrar por período</label>
        <select class="input" style="width:auto; min-width:180px" (change)="onPeriodFilter($event)">
          <option value="">Selecione um período</option>
          @for (p of periods(); track p.id) {
            <option [value]="p.id">{{ monthName(p.month) }}/{{ p.year }}</option>
          }
        </select>
      </div>

      <!-- Lista -->
      @if (loadingList()) {
        <div class="loading-state"><span class="spinner-lg"></span></div>
      } @else if (incomes().length === 0) {
        <div class="empty-state">
          <p>Nenhuma receita encontrada.</p>
        </div>
      } @else {
        <div class="table-wrap card">
          <table class="table">
            <thead>
              <tr>
                <th>Descrição</th>
                <th>Quinzena</th>
                <th>Recebido em</th>
                <th>Valor</th>
                <th>Ações</th>
              </tr>
            </thead>
            <tbody>
              @for (income of incomes(); track income.id) {
                <tr>
                  <td>
                    <div class="cell-primary">{{ income.description }}</div>
                    @if (income.notes) {
                      <div class="cell-secondary">{{ income.notes }}</div>
                    }
                  </td>
                  <td class="text-muted text-sm">{{ fortnightLabel(income.fortnightType) }}</td>
                  <td class="text-muted text-sm">{{ formatDate(income.receivedAt) }}</td>
                  <td class="amount-positive">{{ income.amount | currencyBrl }}</td>
                  <td>
                    <button class="action-btn action-danger" (click)="deleteIncome(income.id)" title="Excluir">✕</button>
                  </td>
                </tr>
              }
            </tbody>
            <tfoot>
              <tr class="total-row">
                <td colspan="3" class="total-label">Total no período</td>
                <td class="amount-positive">{{ total() | currencyBrl }}</td>
                <td></td>
              </tr>
            </tfoot>
          </table>
        </div>
      }
    </div>
  `,
  styles: [`
    .page-content { padding: 28px; display: flex; flex-direction: column; gap: 20px; }
    .form-card  { padding: 24px; }
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
      margin-top: 16px; padding: 10px 14px;
      background: var(--color-danger-bg); color: var(--color-danger);
      border-radius: var(--radius); font-size: 0.875rem;
    }

    .form-footer { display: flex; justify-content: flex-end; gap: 10px; margin-top: 20px; }

    .filter-row { display: flex; align-items: center; gap: 12px; }

    .table-wrap { overflow-x: auto; }
    .table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
    .table th {
      text-align: left; padding: 12px 16px;
      font-size: 0.75rem; font-weight: 600;
      text-transform: uppercase; letter-spacing: 0.04em;
      color: var(--ink3); border-bottom: 1px solid var(--border);
    }
    .table td { padding: 12px 16px; border-bottom: 1px solid var(--bg3); color: var(--ink2); vertical-align: middle; }
    .table tr:last-child td { border-bottom: none; }
    .table tbody tr:hover { background: var(--surface-overlay); }

    .cell-primary   { color: var(--ink); font-weight: 500; }
    .cell-secondary { font-size: 0.75rem; color: var(--ink3); margin-top: 2px; }
    .text-sm        { font-size: 0.8125rem; }
    .text-muted     { color: var(--ink3); }
    .amount-positive { font-weight: 600; color: var(--sage2); }

    .total-row td { background: var(--bg2); font-weight: 600; }
    .total-label  { color: var(--ink2); font-size: 0.875rem; }

    .action-btn {
      width: 28px; height: 28px;
      border: 1px solid var(--border); background: none;
      border-radius: var(--radius-sm); cursor: pointer;
      font-size: 12px; display: flex;
      align-items: center; justify-content: center;
      transition: all var(--transition);
    }
    .action-danger:hover { background: var(--color-danger-bg); border-color: var(--rust); color: var(--rust); }

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
export class IncomesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb  = inject(FormBuilder);

  readonly periods     = signal<PeriodResponse[]>([]);
  readonly incomes     = signal<IncomeResponse[]>([]);
  readonly showForm    = signal(false);
  readonly loading     = signal(false);
  readonly loadingList = signal(false);
  readonly apiError    = signal<string | null>(null);

  private selectedPeriodId: string | null = null;

  readonly total = () => this.incomes().reduce((sum, i) => sum + i.amount, 0);

  readonly form = this.fb.group({
    periodId:      ['', Validators.required],
    fortnightType: [FortnightType.First],
    description:   ['', Validators.required],
    amount:        [null as number | null, [Validators.required, Validators.min(0.01)]],
    receivedAt:    ['', Validators.required],
    notes:         [''],
  });

  ngOnInit(): void {
    this.api.getPeriods().subscribe(p => this.periods.set(p));
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
      this.api.getIncomesByPeriod(periodId).subscribe({
        next: i => { this.incomes.set(i); this.loadingList.set(false); },
        error: () => this.loadingList.set(false)
      });
    } else {
      this.incomes.set([]);
    }
  }

  onSubmit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    this.loading.set(true);
    this.apiError.set(null);
    const v = this.form.getRawValue();

    this.api.createIncome({
      periodId:      v.periodId!,
      fortnightType: Number(v.fortnightType) as FortnightType,
      description:   v.description!,
      amount:        v.amount!,
      receivedAt:    v.receivedAt!,
      notes:         v.notes || undefined,
    }).subscribe({
      next: income => {
        if (this.selectedPeriodId === income.periodId) {
          this.incomes.update(list => [income, ...list]);
        }
        this.form.reset({ fortnightType: FortnightType.First });
        this.showForm.set(false);
        this.loading.set(false);
      },
      error: err => {
        this.apiError.set(err.error?.message ?? 'Erro ao salvar receita.');
        this.loading.set(false);
      }
    });
  }

  deleteIncome(id: string): void {
    if (!confirm('Confirma a exclusão desta receita?')) return;
    this.api.deleteIncome(id).subscribe({
      next: () => this.incomes.update(list => list.filter(i => i.id !== id))
    });
  }

  fortnightLabel(type: FortnightType): string { return FORTNIGHT_TYPE_LABELS[type]; }
  monthName(month: number): string            { return MONTH_NAMES[month - 1].substring(0, 3); }
  formatDate(d: string): string               { return new Date(d).toLocaleDateString('pt-BR'); }
}
