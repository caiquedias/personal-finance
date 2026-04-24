import { Component, input } from '@angular/core';
import { CurrencyBrlPipe } from '../../pipes/currency-brl.pipe';

export type StatCardVariant = 'default' | 'success' | 'danger' | 'warning' | 'info';

@Component({
  selector: 'app-stat-card',
  standalone: true,
  imports: [CurrencyBrlPipe],
  templateUrl: './stat-card.component.html',
  styleUrls: ['./stat-card.component.css']
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
