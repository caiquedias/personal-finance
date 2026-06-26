import { Component, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { CsvReaderService } from '../../core/services/csv-reader.service';
import { PurgeWarningBannerComponent } from './purge-warning-banner.component';
import { StatCardComponent } from '../../shared/components/stat-card/stat-card.component';
import {
  ExpenseResponse, IncomeResponse, PeriodSummary,
  PAYMENT_STATUS_LABELS, SOURCE_TYPE_LABELS, FORTNIGHT_TYPE_LABELS,
} from '../../core/models/models';

@Component({
  selector: 'app-purge-detail',
  standalone: true,
  imports: [PurgeWarningBannerComponent, StatCardComponent, DecimalPipe],
  template: `
    <app-purge-warning-banner />

    @if (summary()) {
      <div class="purge-summary-cards">
        <app-stat-card
          label="Receitas"
          [value]="summary()!.totalIncome"
          variant="success"
          [isCurrency]="true"
        />
        <app-stat-card
          label="Despesas"
          [value]="summary()!.totalExpense"
          variant="danger"
          [isCurrency]="true"
        />
        <app-stat-card
          label="Saldo"
          [value]="summary()!.balance"
          [variant]="summary()!.balance >= 0 ? 'success' : 'danger'"
          [isCurrency]="true"
        />
      </div>
    }

    @if (expenses().length > 0) {
      <section class="purge-section">
        <h3>Despesas ({{ expenses().length }})</h3>
        <div class="purge-table-wrapper">
          <table class="purge-table">
            <thead>
              <tr>
                <th>Descrição</th>
                <th>Valor</th>
                <th>Vencimento</th>
                <th>Pagamento</th>
                <th>Status</th>
                <th>Fonte</th>
                <th>Quinzena</th>
              </tr>
            </thead>
            <tbody>
              @for (exp of expenses(); track exp.id) {
                <tr>
                  <td>{{ exp.description }}</td>
                  <td>{{ exp.amount | number:'1.2-2' }}</td>
                  <td>{{ exp.dueDate }}</td>
                  <td>{{ exp.paymentDate ?? '—' }}</td>
                  <td>{{ paymentStatusLabel(exp.paymentStatus) }}</td>
                  <td>{{ sourceTypeLabel(exp.sourceType) }}</td>
                  <td>{{ fortnightTypeLabel(exp.fortnightType) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    }

    @if (incomes().length > 0) {
      <section class="purge-section">
        <h3>Receitas ({{ incomes().length }})</h3>
        <div class="purge-table-wrapper">
          <table class="purge-table">
            <thead>
              <tr>
                <th>Descrição</th>
                <th>Valor</th>
                <th>Recebido em</th>
                <th>Quinzena</th>
                <th>Notas</th>
              </tr>
            </thead>
            <tbody>
              @for (inc of incomes(); track inc.id) {
                <tr>
                  <td>{{ inc.description }}</td>
                  <td>{{ inc.amount | number:'1.2-2' }}</td>
                  <td>{{ inc.receivedAt }}</td>
                  <td>{{ fortnightTypeLabel(inc.fortnightType) }}</td>
                  <td>{{ inc.notes ?? '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    }

    @if (expenses().length === 0 && incomes().length === 0) {
      <div class="purge-empty">
        <p>Nenhum dado disponível para exibição.</p>
      </div>
    }
  `,
  styles: [`
    .purge-summary-cards {
      display: flex;
      gap: 1rem;
      padding: 1.5rem;
      flex-wrap: wrap;
    }

    .purge-section {
      padding: 1rem 1.5rem;
    }

    .purge-section h3 {
      margin-bottom: 0.75rem;
    }

    .purge-table-wrapper {
      overflow-x: auto;
    }

    .purge-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.875rem;
    }

    .purge-table th,
    .purge-table td {
      padding: 0.5rem 0.75rem;
      border-bottom: 1px solid var(--color-border, #e5e7eb);
      text-align: left;
    }

    .purge-table th {
      font-weight: 600;
      background: var(--color-surface, #f9fafb);
    }

    .purge-empty {
      padding: 2rem;
      text-align: center;
      color: var(--color-text-muted, #6b7280);
    }
  `],
})
export class PurgeDetailComponent {
  private readonly csvReaderService = inject(CsvReaderService);

  readonly expenses = this.csvReaderService.expenses;
  readonly incomes  = this.csvReaderService.incomes;
  readonly summary  = this.csvReaderService.summary;

  paymentStatusLabel(status: number): string {
    return PAYMENT_STATUS_LABELS[status as keyof typeof PAYMENT_STATUS_LABELS] ?? String(status);
  }

  sourceTypeLabel(source: number): string {
    return SOURCE_TYPE_LABELS[source as keyof typeof SOURCE_TYPE_LABELS] ?? String(source);
  }

  fortnightTypeLabel(fortnight: number): string {
    return FORTNIGHT_TYPE_LABELS[fortnight as keyof typeof FORTNIGHT_TYPE_LABELS] ?? String(fortnight);
  }
}
