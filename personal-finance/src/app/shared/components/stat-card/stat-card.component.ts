import { Component, input } from '@angular/core';
import { CurrencyBrlPipe } from '../../pipes/currency-brl.pipe';

export type StatCardVariant = 'default' | 'success' | 'danger' | 'warning' | 'info';

@Component({
  selector: 'app-stat-card',
  standalone: true,
  imports: [CurrencyBrlPipe],
  template: `
    <div class="stat-card" [class]="'stat-card--' + variant()">
      <div class="stat-card-header">
        <span class="stat-card-label">{{ label() }}</span>
        @if (icon()) {
          <span class="stat-card-icon" [innerHTML]="icon()"></span>
        }
      </div>
      <div class="stat-card-value">
        @if (isCurrency() && isNumber(value())) {
          {{ asNumber(value()) | currencyBrl }}
        } @else {
          {{ value() }}
        }
      </div>
      @if (subtitle()) {
        <p class="stat-card-subtitle">{{ subtitle() }}</p>
      }
    </div>
  `,
  styles: [`
    .stat-card {
      padding: 20px;
      border-radius: var(--radius-lg);
      border: 1px solid var(--border);
      background: var(--surface-raised);
      box-shadow: var(--shadow-card);
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .stat-card-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }

    .stat-card-label {
      font-size: 0.8125rem;
      font-weight: 600;
      color: var(--ink3);
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .stat-card-icon {
      width: 32px;
      height: 32px;
      border-radius: var(--radius);
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .stat-card-value {
      font-size: 1.625rem;
      font-weight: 700;
      color: var(--ink);
      line-height: 1.2;
    }

    .stat-card-subtitle {
      font-size: 0.8125rem;
      color: var(--ink3);
      margin: 0;
    }

    /* Variantes */
    .stat-card--success .stat-card-value { color: var(--sage2); }
    .stat-card--success .stat-card-icon  { background: var(--color-success-bg); color: var(--sage2); }

    .stat-card--danger  .stat-card-value { color: var(--rust); }
    .stat-card--danger  .stat-card-icon  { background: var(--color-danger-bg); color: var(--rust); }

    .stat-card--warning .stat-card-value { color: var(--color-warning); }
    .stat-card--warning .stat-card-icon  { background: var(--color-warning-bg); color: var(--color-warning); }

    .stat-card--info    .stat-card-value { color: var(--color-info); }
    .stat-card--info    .stat-card-icon  { background: var(--color-info-bg); color: var(--color-info); }
  `]
})
export class StatCardComponent {
  readonly label      = input.required<string>();
  readonly value      = input.required<number | string>();
  readonly subtitle   = input<string>('');
  readonly icon       = input<string>('');
  readonly variant    = input<StatCardVariant>('default');
  readonly isCurrency = input<boolean>(true);

  isNumber(v: number | string): boolean { return typeof v === 'number'; }
  asNumber(v: number | string): number  { return v as number; }
}
