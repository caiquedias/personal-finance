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
      <button class="btn btn-primary btn-sm" (click)="showForm.set(true)">
        + Novo período
      </button>
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
              <button type="button" class="btn btn-secondary btn-sm" (click)="cancelForm()">Cancelar</button>
              <button type="submit" class="btn btn-primary btn-sm" [disabled]="loading()">
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
            <a [routerLink]="['/periods', period.id]" class="period-card card">
              <div class="period-card-header">
                <span class="period-month">{{ monthName(period.month) }}</span>
                <span class="period-year">{{ period.year }}</span>
              </div>
              <div class="period-card-footer">
                <span class="badge" [class]="period.isActive ? 'badge-success' : 'badge-neutral'">
                  {{ period.isActive ? 'Ativo' : 'Inativo' }}
                </span>
                <span class="period-link">Ver detalhes →</span>
              </div>
            </a>
          }
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

    .periods-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
      gap: 16px;
    }

    .period-card {
      padding: 20px;
      text-decoration: none;
      transition: all var(--transition);
      display: flex;
      flex-direction: column;
      gap: 16px;
      cursor: pointer;
    }

    .period-card:hover {
      box-shadow: var(--shadow-card-hover);
      transform: translateY(-1px);
    }

    .period-card-header {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .period-month {
      font-size: 1.25rem;
      font-weight: 700;
      color: var(--ink);
    }

    .period-year {
      font-size: 0.875rem;
      color: var(--ink3);
    }

    .period-card-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .period-link {
      font-size: 0.8125rem;
      color: var(--sage2);
    }

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

    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class PeriodsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly fb  = inject(FormBuilder);

  readonly periods    = signal<PeriodResponse[]>([]);
  readonly showForm   = signal(false);
  readonly loading    = signal(false);
  readonly loadingList = signal(true);
  readonly apiError   = signal<string | null>(null);

  readonly monthNames = MONTH_NAMES;

  readonly form = this.fb.group({
    month: [new Date().getMonth() + 1, Validators.required],
    year:  [new Date().getFullYear(), [Validators.required, Validators.min(2000), Validators.max(2099)]],
  });

  ngOnInit(): void {
    this.loadPeriods();
  }

  loadPeriods(): void {
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
    this.form.reset({ month: new Date().getMonth() + 1, year: new Date().getFullYear() });
  }

  monthName(month: number): string {
    return MONTH_NAMES[month - 1];
  }
}
