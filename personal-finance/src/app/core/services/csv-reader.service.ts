import { Injectable, signal } from '@angular/core';
import { ExpenseResponse, IncomeResponse, PeriodSummary } from '../models/models';

// Índices do CSV exportado pelo sistema
const IDX_TYPE           = 0;
const IDX_ID             = 1;
const IDX_PERIOD_ID      = 2;
const IDX_USER_ID        = 3;
const IDX_CATEGORY_ID    = 4;
const IDX_SOURCE_TYPE    = 5;
const IDX_FORTNIGHT_TYPE = 6;
const IDX_PAYMENT_STATUS = 7;
const IDX_DESCRIPTION    = 8;
const IDX_AMOUNT         = 9;
const IDX_DUE_DATE       = 10;
const IDX_PAYMENT_DATE   = 11;
const IDX_NOTES          = 12;
const IDX_IS_ACTIVE      = 13;
const IDX_IS_RECURRING   = 14;
const IDX_UPDATED_AT     = 15;
const IDX_RECEIVED_AT    = 16;

const REQUIRED_COLUMNS = 17;

// BOM UTF-8
const UTF8_BOM = '﻿';

@Injectable()
export class CsvReaderService {

  readonly expenses = signal<ExpenseResponse[]>([]);
  readonly incomes  = signal<IncomeResponse[]>([]);
  readonly summary  = signal<PeriodSummary | null>(null);

  parseCsv(content: string): void {
    // Reseta signals
    this.expenses.set([]);
    this.incomes.set([]);
    this.summary.set(null);

    // Strip UTF-8 BOM
    if (content.startsWith(UTF8_BOM)) {
      content = content.slice(1);
    }

    const lines = content.split('\n');
    const parsedExpenses: ExpenseResponse[] = [];
    const parsedIncomes: IncomeResponse[]   = [];

    // Ignora a primeira linha (header)
    for (let i = 1; i < lines.length; i++) {
      const line = lines[i].trim();
      if (!line) continue;

      const cols = line.split(',');
      if (cols.length < REQUIRED_COLUMNS) continue;

      const type = cols[IDX_TYPE];

      if (type === 'Expense') {
        parsedExpenses.push({
          id:            cols[IDX_ID],
          periodId:      cols[IDX_PERIOD_ID],
          userId:        cols[IDX_USER_ID],
          categoryId:    cols[IDX_CATEGORY_ID],
          sourceType:    Number(cols[IDX_SOURCE_TYPE]),
          fortnightType: Number(cols[IDX_FORTNIGHT_TYPE]),
          paymentStatus: Number(cols[IDX_PAYMENT_STATUS]),
          description:   cols[IDX_DESCRIPTION],
          amount:        Number(cols[IDX_AMOUNT]),
          dueDate:       cols[IDX_DUE_DATE],
          paymentDate:   cols[IDX_PAYMENT_DATE] || null,
          notes:         cols[IDX_NOTES] || null,
          isActive:      cols[IDX_IS_ACTIVE] === 'true',
          isRecurring:   cols[IDX_IS_RECURRING] === 'true',
          updatedAt:     cols[IDX_UPDATED_AT],
        });
      } else if (type === 'Income') {
        parsedIncomes.push({
          id:            cols[IDX_ID],
          periodId:      cols[IDX_PERIOD_ID],
          userId:        cols[IDX_USER_ID],
          fortnightType: Number(cols[IDX_FORTNIGHT_TYPE]),
          description:   cols[IDX_DESCRIPTION],
          amount:        Number(cols[IDX_AMOUNT]),
          receivedAt:    cols[IDX_RECEIVED_AT],
          notes:         cols[IDX_NOTES] || null,
          isActive:      cols[IDX_IS_ACTIVE] === 'true',
        });
      }
      // Type desconhecido ou linha inválida → ignorar silenciosamente
    }

    const totalExpense = parsedExpenses.reduce((sum, e) => sum + e.amount, 0);
    const totalIncome  = parsedIncomes.reduce((sum, i) => sum + i.amount, 0);
    const balance      = totalIncome - totalExpense;

    this.expenses.set(parsedExpenses);
    this.incomes.set(parsedIncomes);
    this.summary.set({
      periodId:             '',
      userId:               '',
      year:                 0,
      month:                0,
      totalIncome,
      totalExpense,
      totalPaid:            0,
      totalOwed:            0,
      totalFirstFortnight:  0,
      totalSecondFortnight: 0,
      balance,
    });
  }
}
