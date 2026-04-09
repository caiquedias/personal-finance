import {
  Component, inject, signal, OnInit,
  TemplateRef, ViewChild
} from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { trigger, style, animate, transition } from '@angular/animations';
import { ApiService } from '../../../core/services/api.service';
import { HeaderComponent } from '../../../shared/components/header/header.component';
import { SonicModalComponent } from '../../../shared/components/modal/sonic-modal.component';
import { CurrencyBrlPipe } from '../../../shared/pipes/currency-brl.pipe';
import {
  IncomeResponse, PeriodResponse,
  MONTH_NAMES, FORTNIGHT_TYPE_LABELS, FortnightType
} from '../../../core/models/models';

@Component({
  selector: 'app-incomes',
  standalone: true,
  imports: [
    HeaderComponent, CurrencyBrlPipe, ReactiveFormsModule,
    SonicModalComponent
  ],
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
  template: `
    <app-header title="Receitas" subtitle="Gerencie seus lançamentos de receitas">
      <button class="btn btn-primary btn-sm" (click)="openCreateModal()">
        + Nova receita
      </button>
    </app-header>

    <div class="page-content">

      <!-- Filtro -->
      <div class="filter-row">
        <label class="field-label">Filtrar por período</label>
        <select
          class="input"
          style="width:auto; min-width:180px"
          (change)="onPeriodFilter($event)"
        >
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
        <div class="empty-state"><p>Nenhuma receita encontrada.</p></div>
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
                    <div class="row-actions">
                      <button class="action-btn action-edit" title="Editar" (click)="openEditModal(income)">
                        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                          <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/>
                          <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>
                        </svg>
                      </button>
                      <button class="action-btn action-danger" title="Excluir" (click)="deleteIncome(income.id)">
                        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                          <polyline points="3 6 5 6 21 6"/>
                          <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/>
                          <path d="M10 11v6M14 11v6"/>
                          <path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/>
                        </svg>
                      </button>
                    </div>
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

    <!-- ── Modal inline com @if — Angular controla :enter/:leave ── -->
    @if (modalOpen()) {
      <!-- Backdrop -->
      <div class="modal-overlay" @backdropAnim (click)="closeModal()"></div>

      <!-- Painel centralizado -->
      <div class="modal-center" @modalAnim>
        <app-sonic-modal
          [title]="modalMode() === 'create' ? 'Nova Receita' : 'Editar Receita'"
          (closed)="closeModal()"
        >
          <form [formGroup]="form" (ngSubmit)="onSubmit()">
            <div class="modal-form-grid">

              <div class="field">
                <label class="field-label">Período *</label>
                <select formControlName="periodId" class="input" [class.error]="showError('periodId')">
                  <option value="">Selecione</option>
                  @for (p of periods(); track p.id) {
                    <option [value]="p.id">{{ monthName(p.month) }}/{{ p.year }}</option>
                  }
                </select>
                @if (showError('periodId')) { <span class="field-error">Obrigatório</span> }
              </div>

              <div class="field">
                <label class="field-label">Quinzena *</label>
                <select formControlName="fortnightType" class="input">
                  <option [value]="1">1ª Quinzena</option>
                  <option [value]="2">2ª Quinzena</option>
                </select>
              </div>

              <div class="field field-full">
                <label class="field-label">Descrição *</label>
                <input
                  formControlName="description"
                  class="input"
                  [class.error]="showError('description')"
                  placeholder="Ex: Adiantamento, Saldo, Freelance..."
                />
                @if (showError('description')) { <span class="field-error">Obrigatório</span> }
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
                @if (showError('amount')) { <span class="field-error">Valor inválido</span> }
              </div>

              <div class="field">
                <label class="field-label">Recebido em *</label>
                <input
                  type="date"
                  formControlName="receivedAt"
                  class="input"
                  [class.error]="showError('receivedAt')"
                />
                @if (showError('receivedAt')) { <span class="field-error">Obrigatório</span> }
              </div>

              <div class="field field-full">
                <label class="field-label">Observações</label>
                <input formControlName="notes" class="input" placeholder="Opcional..." />
              </div>

            </div>

            @if (apiError()) {
              <div class="form-error">{{ apiError() }}</div>
            }

            <div class="modal-footer">
              <button type="button" class="btn btn-secondary" (click)="closeModal()">Cancelar</button>
              <button type="submit" class="btn btn-primary" [disabled]="saving()">
                {{ saving() ? 'Salvando...' : (modalMode() === 'create' ? 'Criar receita' : 'Salvar') }}
              </button>
            </div>
          </form>
        </app-sonic-modal>
      </div>
    }
  `,
  styles: [`
    .page-content { padding: 28px; display: flex; flex-direction: column; gap: 20px; }
    .filter-row   { display: flex; align-items: center; gap: 12px; }

    .table-wrap { overflow-x: auto; }
    .table { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
    .table th {
      text-align: left; padding: 12px 16px;
      font-size: 0.75rem; font-weight: 600;
      text-transform: uppercase; letter-spacing: 0.04em;
      color: var(--ink3); border-bottom: 1px solid var(--border);
    }
    .table td {
      padding: 12px 16px; border-bottom: 1px solid var(--bg3);
      color: var(--ink2); vertical-align: middle;
    }
    .table tr:last-child td { border-bottom: none; }
    .table tbody tr:hover   { background: var(--surface-overlay); }

    .cell-primary   { color: var(--ink); font-weight: 500; }
    .cell-secondary { font-size: 0.75rem; color: var(--ink3); margin-top: 2px; }
    .text-sm        { font-size: 0.8125rem; }
    .text-muted     { color: var(--ink3); }
    .amount-positive { font-weight: 600; color: var(--sage2); }

    .total-row td { background: var(--bg2); font-weight: 600; }
    .total-label  { color: var(--ink2); font-size: 0.875rem; }

    .row-actions { display: flex; gap: 4px; }
    .action-btn {
      width: 28px; height: 28px; border: 1px solid var(--border);
      background: none; border-radius: var(--radius-sm);
      cursor: pointer; font-size: 12px;
      display: flex; align-items: center; justify-content: center;
      transition: all var(--transition); color: var(--ink3);
    }
    .action-edit:hover   { background: var(--color-info-bg);   border-color: var(--color-info);  color: var(--color-info);  }
    .action-danger:hover { background: var(--color-danger-bg); border-color: var(--rust);        color: var(--rust);        }

    /* ── Modal overlay e painel ── */
    .modal-overlay {
      position: fixed;
      inset: 0;
      background: rgba(26, 20, 14, 0.55);
      backdrop-filter: blur(3px);
      z-index: 900;
    }

    .modal-center {
      position: fixed;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 901;
      pointer-events: none;  /* deixa clique passar para o overlay */
    }

    .modal-center > * {
      pointer-events: all;   /* reativa nos filhos */
    }

    /* ── Modal form ── */
    .modal-form-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }

    .field       { display: flex; flex-direction: column; gap: 6px; }
    .field-full  { grid-column: span 2; }
    .field-label { font-size: 0.875rem; font-weight: 500; color: var(--ink2); }
    .field-error { font-size: 0.8125rem; color: var(--color-danger); }

    .form-error {
      margin-top: 12px; padding: 10px 14px;
      background: var(--color-danger-bg); color: var(--color-danger);
      border-radius: var(--radius); font-size: 0.875rem;
    }

    .modal-footer {
      display: flex; justify-content: flex-end; gap: 10px;
      margin-top: 20px; padding-top: 16px;
      border-top: 1px solid var(--border);
    }

    /* ── States ── */
    .loading-state, .empty-state {
      display: flex; flex-direction: column;
      align-items: center; gap: 16px; padding: 64px; color: var(--ink3);
    }
    .spinner-lg {
      width: 32px; height: 32px;
      border: 3px solid var(--border); border-top-color: var(--sage2);
      border-radius: 50%; animation: spin .8s linear infinite; display: block;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class IncomesComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb  = inject(FormBuilder);

  readonly periods     = signal<PeriodResponse[]>([]);
  readonly incomes     = signal<IncomeResponse[]>([]);
  readonly loadingList = signal(false);
  readonly saving      = signal(false);
  readonly apiError    = signal<string | null>(null);
  readonly modalOpen   = signal(false);
  readonly modalMode   = signal<'create' | 'edit'>('create');

  private editingId: string | null = null;
  private selectedPeriodId: string | null = null;

  readonly total = () => this.incomes().reduce((s, i) => s + i.amount, 0);

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
        next: income => {
          if (this.selectedPeriodId === income.periodId)
            this.incomes.update(list => [income, ...list]);
          this.closeModal();
          this.saving.set(false);
        },
        error: err => { this.apiError.set(err.error?.message ?? 'Erro ao criar receita.'); this.saving.set(false); }
      });
    } else {
      const id = this.editingId!;
      this.api.deleteIncome(id).subscribe({
        next: () => {
          this.api.createIncome(payload).subscribe({
            next: updated => {
              this.incomes.update(list => list.map(i => i.id === id ? updated : i));
              this.closeModal();
              this.saving.set(false);
            },
            error: err => {
              this.apiError.set(err.error?.message ?? 'Erro ao salvar alterações.');
              this.saving.set(false);
              if (this.selectedPeriodId)
                this.api.getIncomesByPeriod(this.selectedPeriodId).subscribe(i => this.incomes.set(i));
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
      next: () => this.incomes.update(list => list.filter(i => i.id !== id))
    });
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

  showError(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  fortnightLabel(type: FortnightType): string { return FORTNIGHT_TYPE_LABELS[type]; }
  monthName(month: number): string            { return MONTH_NAMES[month - 1].substring(0, 3); }
  formatDate(d: string): string               { return new Date(d).toLocaleDateString('pt-BR'); }
}
