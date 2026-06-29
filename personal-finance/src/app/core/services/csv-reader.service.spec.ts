import { TestBed } from '@angular/core/testing';
import { CsvReaderService } from './csv-reader.service';
import { ExpenseResponse, IncomeResponse, FortnightType, PaymentStatus, SourceType } from '../models/models';

// Cabeçalho exportado pelo CsvExportService (12 colunas)
const HEADER = 'Type,PeriodYear,PeriodMonth,Description,Amount,Category,FortnightType,PaymentStatus,SourceType,DueDate,PaymentDate,Notes';

// Linha de Expense completa com 12 colunas; enums como strings
function buildExpenseLine(overrides: Partial<Record<string, string>> = {}): string {
  const defaults: Record<string, string> = {
    Type:          'Expense',
    PeriodYear:    '2024',
    PeriodMonth:   '1',
    Description:   'Aluguel',
    Amount:        '1500.00',
    Category:      '',
    FortnightType: 'First',
    PaymentStatus: 'Pending',
    SourceType:    'Personal',
    DueDate:       '2024-01-10',
    PaymentDate:   '2024-01-09',
    Notes:         'Pago com desconto',
  };
  const row = { ...defaults, ...overrides };
  return [
    row['Type'], row['PeriodYear'], row['PeriodMonth'], row['Description'],
    row['Amount'], row['Category'], row['FortnightType'], row['PaymentStatus'],
    row['SourceType'], row['DueDate'], row['PaymentDate'], row['Notes'],
  ].join(',');
}

// Linha de Income com 12 colunas; colunas específicas de Expense ficam vazias
function buildIncomeLine(overrides: Partial<Record<string, string>> = {}): string {
  const defaults: Record<string, string> = {
    Type:          'Income',
    PeriodYear:    '2024',
    PeriodMonth:   '1',
    Description:   'Salário',
    Amount:        '3000.00',
    Category:      '',
    FortnightType: '',
    PaymentStatus: '',
    SourceType:    '',
    DueDate:       '',
    PaymentDate:   '',
    Notes:         '',
  };
  const row = { ...defaults, ...overrides };
  return [
    row['Type'], row['PeriodYear'], row['PeriodMonth'], row['Description'],
    row['Amount'], row['Category'], row['FortnightType'], row['PaymentStatus'],
    row['SourceType'], row['DueDate'], row['PaymentDate'], row['Notes'],
  ].join(',');
}

describe('CsvReaderService', () => {
  let service: CsvReaderService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CsvReaderService],
    });
    service = TestBed.inject(CsvReaderService);
  });

  // ── Estado inicial ─────────────────────────────────────────────────────────

  it('deve iniciar com expenses vazio', () => {
    expect(service.expenses()).toEqual([]);
  });

  it('deve iniciar com incomes vazio', () => {
    expect(service.incomes()).toEqual([]);
  });

  it('deve iniciar com summary nulo', () => {
    expect(service.summary()).toBeNull();
  });

  // ── Parsing de Expense ─────────────────────────────────────────────────────

  it('deve parsear linha Expense e popular signal expenses', () => {
    const csv = [HEADER, buildExpenseLine()].join('\n');
    service.parseCsv(csv);

    const expenses = service.expenses();
    expect(expenses.length).toBe(1);

    const expense: ExpenseResponse = expenses[0];
    expect(expense.sourceType).toBe(SourceType.Personal);
    expect(expense.fortnightType).toBe(FortnightType.First);
    expect(expense.paymentStatus).toBe(PaymentStatus.Pending);
    expect(expense.description).toBe('Aluguel');
    expect(expense.amount).toBe(1500.00);
    expect(expense.dueDate).toBe('2024-01-10');
    expect(expense.paymentDate).toBe('2024-01-09');
    expect(expense.notes).toBe('Pago com desconto');
  });

  it('linha Expense não deve poluir signal incomes', () => {
    const csv = [HEADER, buildExpenseLine()].join('\n');
    service.parseCsv(csv);
    expect(service.incomes().length).toBe(0);
  });

  // ── Parsing de Income ──────────────────────────────────────────────────────

  it('deve parsear linha Income e popular signal incomes', () => {
    const csv = [HEADER, buildIncomeLine()].join('\n');
    service.parseCsv(csv);

    const incomes = service.incomes();
    expect(incomes.length).toBe(1);

    const income: IncomeResponse = incomes[0];
    expect(income.description).toBe('Salário');
    expect(income.amount).toBe(3000.00);
    expect(income.receivedAt).toBe('01/2024');
    expect(income.notes).toBeNull();
  });

  it('linha Income não deve poluir signal expenses', () => {
    const csv = [HEADER, buildIncomeLine()].join('\n');
    service.parseCsv(csv);
    expect(service.expenses().length).toBe(0);
  });

  // ── Campos opcionais vazios → null ─────────────────────────────────────────

  it('PaymentDate vazio em Expense deve resultar em null', () => {
    const csv = [HEADER, buildExpenseLine({ PaymentDate: '' })].join('\n');
    service.parseCsv(csv);
    expect(service.expenses()[0].paymentDate).toBeNull();
  });

  it('Notes vazio em Expense deve resultar em null', () => {
    const csv = [HEADER, buildExpenseLine({ Notes: '' })].join('\n');
    service.parseCsv(csv);
    expect(service.expenses()[0].notes).toBeNull();
  });

  it('Notes vazio em Income deve resultar em null', () => {
    const csv = [HEADER, buildIncomeLine({ Notes: '' })].join('\n');
    service.parseCsv(csv);
    expect(service.incomes()[0].notes).toBeNull();
  });

  // ── UTF-8 BOM ──────────────────────────────────────────────────────────────

  it('deve ignorar BOM UTF-8 no início do arquivo e parsear normalmente', () => {
    const bom = '﻿';
    const csv = bom + [HEADER, buildExpenseLine()].join('\n');
    service.parseCsv(csv);
    expect(service.expenses().length).toBe(1);
  });

  it('deve ignorar BOM UTF-8 antes de linha Income', () => {
    const bom = '﻿';
    const csv = bom + [HEADER, buildIncomeLine()].join('\n');
    service.parseCsv(csv);
    expect(service.incomes().length).toBe(1);
  });

  // ── Schema mismatch → ignorar linha ───────────────────────────────────────

  it('linha com colunas insuficientes não deve lançar exceção', () => {
    const csv = [HEADER, 'Expense,apenas-dois-campos'].join('\n');
    expect(() => service.parseCsv(csv)).not.toThrow();
  });

  it('linha com colunas insuficientes deve ser ignorada (não inserida)', () => {
    const csv = [HEADER, 'Expense,apenas-dois-campos'].join('\n');
    service.parseCsv(csv);
    expect(service.expenses().length).toBe(0);
  });

  it('linha vazia intercalada não deve interromper o parsing das linhas válidas', () => {
    const csv = [HEADER, buildExpenseLine(), '', buildIncomeLine()].join('\n');
    service.parseCsv(csv);
    expect(service.expenses().length).toBe(1);
    expect(service.incomes().length).toBe(1);
  });

  it('Type desconhecido deve ser ignorado sem lançar exceção', () => {
    const csv = [HEADER, buildExpenseLine({ Type: 'Unknown' })].join('\n');
    expect(() => service.parseCsv(csv)).not.toThrow();
    expect(service.expenses().length).toBe(0);
    expect(service.incomes().length).toBe(0);
  });

  // ── Summary calculado em memória ───────────────────────────────────────────

  it('summary deve ser calculado após parseCsv com Expense e Income', () => {
    const csv = [HEADER, buildExpenseLine(), buildIncomeLine()].join('\n');
    service.parseCsv(csv);

    const summary = service.summary();
    expect(summary).not.toBeNull();
    expect(summary!.totalExpense).toBe(1500.00);
    expect(summary!.totalIncome).toBe(3000.00);
    expect(summary!.balance).toBe(1500.00); // income - expense
  });

  it('summary.totalExpense deve somar todas as despesas', () => {
    const exp1 = buildExpenseLine({ Amount: '500.00' });
    const exp2 = buildExpenseLine({ Amount: '250.50' });
    const csv  = [HEADER, exp1, exp2].join('\n');
    service.parseCsv(csv);
    expect(service.summary()!.totalExpense).toBeCloseTo(750.50, 2);
  });

  it('summary.totalIncome deve somar todos os rendimentos', () => {
    const inc1 = buildIncomeLine({ Amount: '2000.00' });
    const inc2 = buildIncomeLine({ Amount: '500.00' });
    const csv  = [HEADER, inc1, inc2].join('\n');
    service.parseCsv(csv);
    expect(service.summary()!.totalIncome).toBeCloseTo(2500.00, 2);
  });

  it('summary.balance deve ser negativo quando despesas superam receitas', () => {
    const exp = buildExpenseLine({ Amount: '5000.00' });
    const inc = buildIncomeLine({ Amount: '2000.00' });
    const csv = [HEADER, exp, inc].join('\n');
    service.parseCsv(csv);
    expect(service.summary()!.balance).toBeCloseTo(-3000.00, 2);
  });

  it('parseCsv chamado novamente deve resetar signals anteriores', () => {
    const csv1 = [HEADER, buildExpenseLine()].join('\n');
    service.parseCsv(csv1);
    expect(service.expenses().length).toBe(1);

    const csv2 = [HEADER, buildIncomeLine()].join('\n');
    service.parseCsv(csv2);
    // Após segundo parse, expenses deve estar vazio (reset)
    expect(service.expenses().length).toBe(0);
    expect(service.incomes().length).toBe(1);
  });

  // ── Escape CSV: campo com vírgula entre aspas ──────────────────────────────

  it('campo com vírgula entre aspas deve ser parseado como campo único', () => {
    // Description contém vírgula, então fica entre aspas no CSV
    const line = `Expense,2024,1,"Aluguel, residencial",1500.00,,First,Pending,Personal,2024-01-10,2024-01-09,`;
    const csv = [HEADER, line].join('\n');
    service.parseCsv(csv);

    expect(service.expenses().length).toBe(1);
    expect(service.expenses()[0].description).toBe('Aluguel, residencial');
  });

  it('aspas escapadas duplicadas devem ser resolvidas para aspas simples', () => {
    // Description = Diz "olá" → CSV: "Diz ""olá"""
    const line = `Expense,2024,1,"Diz ""olá""",500.00,,First,Pending,Personal,2024-01-10,,`;
    const csv = [HEADER, line].join('\n');
    service.parseCsv(csv);

    expect(service.expenses().length).toBe(1);
    expect(service.expenses()[0].description).toBe('Diz "olá"');
  });

  // ── receivedAt de Income sintetizado a partir de PeriodYear + PeriodMonth ──

  it('receivedAt de Income deve ser sintetizado como "MM/YYYY"', () => {
    const csv = [HEADER, buildIncomeLine({ PeriodYear: '2024', PeriodMonth: '1' })].join('\n');
    service.parseCsv(csv);
    expect(service.incomes()[0].receivedAt).toBe('01/2024');
  });

  it('receivedAt de Income com mês >= 10 deve usar zero-padding correto', () => {
    const csv = [HEADER, buildIncomeLine({ PeriodYear: '2024', PeriodMonth: '10' })].join('\n');
    service.parseCsv(csv);
    expect(service.incomes()[0].receivedAt).toBe('10/2024');
  });

  // ── Enums como strings: FortnightType, PaymentStatus, SourceType ──────────

  it('FortnightType "Second" deve ser parseado como FortnightType.Second (2)', () => {
    const csv = [HEADER, buildExpenseLine({ FortnightType: 'Second' })].join('\n');
    service.parseCsv(csv);
    expect(service.expenses()[0].fortnightType).toBe(FortnightType.Second);
  });

  it('PaymentStatus "Paid" deve ser parseado como PaymentStatus.Paid (2)', () => {
    const csv = [HEADER, buildExpenseLine({ PaymentStatus: 'Paid' })].join('\n');
    service.parseCsv(csv);
    expect(service.expenses()[0].paymentStatus).toBe(PaymentStatus.Paid);
  });

  it('SourceType "Parental" deve ser parseado como SourceType.Parental (1)', () => {
    const csv = [HEADER, buildExpenseLine({ SourceType: 'Parental' })].join('\n');
    service.parseCsv(csv);
    expect(service.expenses()[0].sourceType).toBe(SourceType.Parental);
  });

  // ── Fallback para enum string inválida ────────────────────────────────────

  it('FortnightType string inválida deve resultar em FortnightType.First (fallback)', () => {
    const csv = [HEADER, buildExpenseLine({ FortnightType: 'Invalid' })].join('\n');
    service.parseCsv(csv);
    expect(service.expenses()[0].fortnightType).toBe(FortnightType.First);
  });

  it('PaymentStatus string inválida deve resultar em PaymentStatus.Pending (fallback)', () => {
    const csv = [HEADER, buildExpenseLine({ PaymentStatus: 'Invalid' })].join('\n');
    service.parseCsv(csv);
    expect(service.expenses()[0].paymentStatus).toBe(PaymentStatus.Pending);
  });

  it('SourceType string inválida deve resultar em SourceType.Personal (fallback)', () => {
    const csv = [HEADER, buildExpenseLine({ SourceType: 'Invalid' })].join('\n');
    service.parseCsv(csv);
    expect(service.expenses()[0].sourceType).toBe(SourceType.Personal);
  });
});
