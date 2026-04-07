import { Component, inject, signal, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { HeaderComponent } from '../../../shared/components/header/header.component';
import { PeriodResponse, MONTH_NAMES } from '../../../core/models/models';

@Component({
  selector: 'app-periods',
  standalone: true,
  imports: [HeaderComponent, RouterLink, ReactiveFormsModule],
  template: `
    <app-header title="Períodos" subtitle="Gerencie seus períodos mensais">
      @if (!showForm()) {
        <button class="btn btn-primary btn-sm" (click)="showForm.set(true)">
          + Novo período
        </button>
      }
    </app-header>

    <div class="page-content">

      <!-- Formulário de criação -->
      @if (showForm()) {
        <div class="card form-card">
          <h3 style="margin-bottom: 20px; font-size: 1rem;">Novo período</h3>
          <form [formGroup]="form" (ngSubmit)="onSubmit()" class="form-row">
            <div class="field">
              <label class="field-label">Mês</label>
              <select formControlName="month" class="input">
                @for (name of monthNames; track $index) {
                  <option [value]="$index + 1">{{ name }}</option>
                }
              </select>
            </div>
            <div class="field">
              <label class="field-label">Ano</label>
              <input
                type="number"
                formControlName="year"
                class="input"
                [class.error]="showError('year')"
                min="2000"
                max="2099"
              />
              @if (showError('year')) {
                <span class="field-error">Ano inválido</span>
              }
            </div>
            <div class="form-actions">
              <button
                type="button"
                class="btn btn-secondary btn-sm"
                (click)="cancelForm()"
                [disabled]="loading()"
              >
                Cancelar
              </button>
              <button
                type="submit"
                class="btn btn-primary btn-sm"
                [disabled]="loading()"
              >
                {{ loading() ? 'Criando...' : 'Criar' }}
              </button>
            </div>
          </form>
          @if (apiError()) {
            <div class="form-error">{{ apiError() }}</div>
          }
        </div>
      }

      <!-- Lista de períodos -->
      @if (loadingList()) {
        <div class="loading-state">
          <span class="spinner-lg"></span>
        </div>
      } @else if (periods().length === 0) {
        <div class="empty-state">
          <p>Nenhum período cadastrado.</p>
          <button class="btn btn-primary btn-sm" (click)="showForm.set(true)">
            Criar primeiro período
          </button>
        </div>
      } @else {
        <div class="periods-grid">
          @for (period of periods(); track period.id) {
            <div class="period-card card" [class.inactive]="!period.isActive">

              <!-- Cabeçalho do card -->
              <div class="period-card-header">
                <div class="period-date">
                  <span class="period-month">{{ monthName(period.month) }}</span>
                  <span class="period-year">{{ period.year }}</span>
                </div>
                <span
                  class="badge"
                  [class]="period.isActive ? 'badge-success' : 'badge-neutral'"
                >
                  {{ period.isActive ? 'Ativo' : 'Inativo' }}
                </span>
              </div>

              <!-- Rodapé do card -->
              <div class="period-card-footer">
                <a
                  [routerLink]="['/periods', period.id]"
                  class="btn btn-ghost btn-sm"
                  style="font-size: 0.8125rem"
                >
                  Ver detalhes →
                </a>
                <div class="card-actions">
                  <!-- Toggle ativo/inativo -->
                  <button
                    class="action-btn"
                    [class]="period.isActive ? 'action-warning' : 'action-success'"
                    [title]="period.isActive ? 'Desativar período' : 'Ativar período'"
                    [disabled]="actionLoading() === period.id"
                    (click)="toggleActive(period)"
                  >
                    @if (actionLoading() === period.id + '-toggle') {
                      <span class="spinner-xs"></span>
                    } @else {
                      {{ period.isActive ? '⏸' : '▶' }}
                    }
                  </button>

                  <!-- Excluir -->
                  <button
                    class="action-btn action-danger"
                    title="Excluir período"
                    [disabled]="actionLoading() === period.id"
                    (click)="deletePeriod(period)"
                  >
                    @if (actionLoading() === period.id + '-delete') {
                      <span class="spinner-xs"></span>
                    } @else {
                      <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="3 6 5 6 21 6"/>
                        <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/>
                        <path d="M10 11v6M14 11v6"/>
                        <path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/>
                      </svg>
                    }
                  </button>
                </div>
              </div>

            </div>
          }
        </div>
      }

      <!-- Feedback de erro de ação -->
      @if (actionError()) {
        <div class="action-error">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"/>
            <line x1="12" y1="8" x2="12" y2="12"/>
            <line x1="12" y1="16" x2="12.01" y2="16"/>
          </svg>
          {{ actionError() }}
          <button class="action-error-close" (click)="actionError.set(null)">✕</button>
        </div>
      }

    </div>
  `,
  styles: [`
    .page-content {
      padding: 28px;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }

    /* ── Formulário ── */
    .form-card { padding: 24px; }

    .form-row {
      display: flex;
      align-items: flex-end;
      gap: 16px;
      flex-wrap: wrap;
    }

    .field { display: flex; flex-direction: column; gap: 6px; min-width: 140px; }
    .field-label { font-size: 0.875rem; font-weight: 500; color: var(--ink2); }
    .field-error  { font-size: 0.8125rem; color: var(--color-danger); }

    .form-actions { display: flex; gap: 8px; align-items: flex-end; padding-bottom: 1px; }

    .form-error {
      margin-top: 12px;
      padding: 10px 14px;
      background: var(--color-danger-bg);
      color: var(--color-danger);
      border-radius: var(--radius);
      font-size: 0.875rem;
    }

    /* ── Grid de períodos ── */
    .periods-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 16px;
    }

    .period-card {
      padding: 20px;
      display: flex;
      flex-direction: column;
      gap: 16px;
      transition: all var(--transition);
    }

    .period-card.inactive {
      opacity: 0.6;
    }

    .period-card-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 8px;
    }

    .period-date {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .period-month { font-size: 1.25rem; font-weight: 700; color: var(--ink); }
    .period-year  { font-size: 0.875rem; color: var(--ink3); }

    .period-card-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    /* ── Ações do card ── */
    .card-actions { display: flex; gap: 4px; }

    .action-btn {
      width: 30px;
      height: 30px;
      border: 1px solid var(--border);
      background: var(--surface-raised);
      border-radius: var(--radius);
      cursor: pointer;
      font-size: 13px;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all var(--transition);
      color: var(--ink3);
    }

    .action-btn:disabled { opacity: 0.4; cursor: not-allowed; }

    .action-success:hover:not(:disabled) {
      background: var(--color-success-bg);
      border-color: var(--sage2);
      color: var(--sage2);
    }

    .action-warning:hover:not(:disabled) {
      background: var(--color-warning-bg);
      border-color: var(--color-warning);
      color: var(--color-warning);
    }

    .action-danger:hover:not(:disabled) {
      background: var(--color-danger-bg);
      border-color: var(--rust);
      color: var(--rust);
    }

    /* ── Erro de ação ── */
    .action-error {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px 16px;
      background: var(--color-danger-bg);
      color: var(--color-danger);
      border-radius: var(--radius);
      font-size: 0.875rem;
    }

    .action-error-close {
      margin-left: auto;
      background: none;
      border: none;
      cursor: pointer;
      color: var(--color-danger);
      font-size: 12px;
      padding: 2px 4px;
    }

    /* ── Estados ── */
    .loading-state, .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 16px;
      padding: 64px;
      color: var(--ink3);
    }

    .spinner-lg {
      width: 32px; height: 32px;
      border: 3px solid var(--border);
      border-top-color: var(--sage2);
      border-radius: 50%;
      animation: spin .8s linear infinite;
      display: block;
    }

    .spinner-xs {
      width: 12px; height: 12px;
      border: 2px solid currentColor;
      border-top-color: transparent;
      border-radius: 50%;
      animation: spin .6s linear infinite;
      display: inline-block;
    }

    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class PeriodsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb  = inject(FormBuilder);

  readonly periods       = signal<PeriodResponse[]>([]);
  readonly showForm      = signal(false);
  readonly loading       = signal(false);
  readonly loadingList   = signal(true);
  readonly apiError      = signal<string | null>(null);
  readonly actionLoading = signal<string | null>(null);
  readonly actionError   = signal<string | null>(null);

  readonly monthNames = MONTH_NAMES;

  readonly form = this.fb.group({
    month: [new Date().getMonth() + 1, Validators.required],
    year:  [new Date().getFullYear(), [Validators.required, Validators.min(2000), Validators.max(2099)]],
  });

  ngOnInit(): void {
    this.loadPeriods();
  }

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
