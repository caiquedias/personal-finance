import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'currencyBrl', standalone: true, pure: true })
export class CurrencyBrlPipe implements PipeTransform {
  transform(value: number | null | undefined, showSign = false): string {
    if (value == null) return 'R$ —';
    const formatted = new Intl.NumberFormat('pt-BR', {
      style: 'currency', currency: 'BRL'
    }).format(Math.abs(value));

    if (!showSign) return formatted;
    if (value > 0)  return `+${formatted}`;
    if (value < 0)  return `-${formatted}`;
    return formatted;
  }
}
