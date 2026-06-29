import { Injectable, signal } from '@angular/core';
import { ExpenseResponse, IncomeResponse, PeriodSummary, FortnightType, PaymentStatus, SourceType } from '../models/models';

// Índices do CSV exportado pelo sistema (12 colunas)
// Type,PeriodYear,PeriodMonth,Description,Amount,Category,FortnightType,PaymentStatus,SourceType,DueDate,PaymentDate,Notes
const IDX_TYPE           = 0;
const IDX_PERIOD_YEAR    = 1;
const IDX_PERIOD_MONTH   = 2;
const IDX_DESCRIPTION    = 3;
const IDX_AMOUNT         = 4;
const IDX_CATEGORY       = 5;
const IDX_FORTNIGHT_TYPE = 6;
const IDX_PAYMENT_STATUS = 7;
const IDX_SOURCE_TYPE    = 8;
const IDX_DUE_DATE       = 9;
const IDX_PAYMENT_DATE   = 10;
const IDX_NOTES          = 11;

const REQUIRED_COLUMNS = 12;

// Mapeamentos de string → enum numérico
const FORTNIGHT_TYPE_MAP: Record<string, FortnightType> = {
  First:  FortnightType.First,
  Second: FortnightType.Second,
};

const PAYMENT_STATUS_MAP: Record<string, PaymentStatus> = {
  Pending:   PaymentStatus.Pending,
  Paid:      PaymentStatus.Paid,
  Cancelled: PaymentStatus.Cancelled,
  Partial:   PaymentStatus.Partial,
};

const SOURCE_TYPE_MAP: Record<string, SourceType> = {
  Personal: SourceType.Personal,
  Parental: SourceType.Parental,
};

// Parser CSV que respeita campos entre aspas (RFC 4180)
function parseCsvLine(line: string): string[] {
  const result: string[] = [];
  let current = '';
  let inQuotes = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (ch === '"') {
      if (inQuotes && line[i + 1] === '"') { current += '"'; i++; }
      else { inQuotes = !inQuotes; }
    } else if (ch === ',' && !inQuotes) {
      result.push(current); current = '';
    } else {
      current += ch;
    }
  }
  result.push(current);
  return result;
}

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

      const cols = parseCsvLine(line);
      if (cols.length < REQUIRED_COLUMNS) continue;

      const type = cols[IDX_TYPE];
      const lineIndex = i - 1; // lineIndex começa em 0 (desconta o header)

      if (type === 'Expense') {
        parsedExpenses.push({
          id:            `expense-${lineIndex}`,
          periodId:      '',
          userId:        '',
          categoryId:    '',
          sourceType:    SOURCE_TYPE_MAP[cols[IDX_SOURCE_TYPE]] ?? SourceType.Personal,
          fortnightType: FORTNIGHT_TYPE_MAP[cols[IDX_FORTNIGHT_TYPE]] ?? FortnightType.First,
          paymentStatus: PAYMENT_STATUS_MAP[cols[IDX_PAYMENT_STATUS]] ?? PaymentStatus.Pending,
          description:   cols[IDX_DESCRIPTION],
          amount:        Number(cols[IDX_AMOUNT]),
          dueDate:       cols[IDX_DUE_DATE],
          paymentDate:   cols[IDX_PAYMENT_DATE] || null,
          notes:         cols[IDX_NOTES] || null,
          isActive:      true,
          isRecurring:   false,
          updatedAt:     '',
        });
      } else if (type === 'Income') {
        const year  = cols[IDX_PERIOD_YEAR];
        const month = cols[IDX_PERIOD_MONTH].padStart(2, '0');
        parsedIncomes.push({
          id:            `income-${lineIndex}`,
          periodId:      '',
          userId:        '',
          fortnightType: 0 as FortnightType,
          description:   cols[IDX_DESCRIPTION],
          amount:        Number(cols[IDX_AMOUNT]),
          receivedAt:    `${month}/${year}`,
          notes:         cols[IDX_NOTES] || null,
          isActive:      true,
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
