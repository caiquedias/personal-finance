import { TestBed } from '@angular/core/testing';
import { CsvReaderService } from './csv-reader.service';
import { ExpenseResponse, IncomeResponse, FortnightType, PaymentStatus, SourceType } from '../models/models';

// Cabeçalho padrão exportado pelo sistema
const HEADER = 'Type,Id,PeriodId,UserId,CategoryId,SourceType,FortnightType,PaymentStatus,Description,Amount,DueDate,PaymentDate,Notes,IsActive,IsRecurring,UpdatedAt,ReceivedAt';

// Linha de Expense completa (sem campos opcionais vazios)
function buildExpenseLine(overrides: Partial<Record<string, string>> = {}): string {
  const defaults: Record<string, string> = {
    Type:          'Expense',
    Id:            'exp-uuid-001',
    PeriodId:      'period-uuid-001',
    UserId:        'user-uuid-001',
    CategoryId:    'cat-uuid-001',
    SourceType:    '2',
    FortnightType: '1',
    PaymentStatus: '1',
    Description:   'Aluguel',
    Amount:        '1500.00',
    DueDate:       '2024-01-10',
    PaymentDate:   '2024-01-09',
    Notes:         'Pago com desconto',
    IsActive:      'true',
    IsRecurring:   'true',
    UpdatedAt:     '2024-01-09T10:00:00Z',
    ReceivedAt:    '',
  };
  const row = { ...defaults, ...overrides };
  return [
    row['Type'], row['Id'], row['PeriodId'], row['UserId'], row['CategoryId'],
    row['SourceType'], row['FortnightType'], row['PaymentStatus'], row['Description'],
    row['Amount'], row['DueDate'], row['PaymentDate'], row['Notes'],
    row['IsActive'], row['IsRecurring'], row['UpdatedAt'], row['ReceivedAt'],
  ].join(',');
}

// Linha de Income completa
function buildIncomeLine(overrides: Partial<Record<string, string>> = {}): string {
  const defaults: Record<string, string> = {
    Type:          'Income',
    Id:            'inc-uuid-001',
    PeriodId:      'period-uuid-001',
    UserId:        'user-uuid-001',
    CategoryId:    '',
    SourceType:    '',
    FortnightType: '2',
    PaymentStatus: '',
    Description:   'Salário',
    Amount:        '3000.00',
    DueDate:       '',
    PaymentDate:   '',
    Notes:         '',
    IsActive:      'true',
    IsRecurring:   '',
    UpdatedAt:     '',
    ReceivedAt:    '2024-01-05',
  };
  const row = { ...defaults, ...overrides };
  return [
    row['Type'], row['Id'], row['PeriodId'], row['UserId'], row['CategoryId'],
    row['SourceType'], row['FortnightType'], row['PaymentStatus'], row['Description'],
    row['Amount'], row['DueDate'], row['PaymentDate'], row['Notes'],
    row['IsActive'], row['IsRecurring'], row['UpdatedAt'], row['ReceivedAt'],
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
    expect(expense.id).toBe('exp-uuid-001');
    expect(expense.periodId).toBe('period-uuid-001');
    expect(expense.userId).toBe('user-uuid-001');
    expect(expense.categoryId).toBe('cat-uuid-001');
    expect(expense.sourceType).toBe(SourceType.Personal);
    expect(expense.fortnightType).toBe(FortnightType.First);
    expect(expense.paymentStatus).toBe(PaymentStatus.Pending);
    expect(expense.description).toBe('Aluguel');
    expect(expense.amount).toBe(1500.00);
    expect(expense.dueDate).toBe('2024-01-10');
    expect(expense.paymentDate).toBe('2024-01-09');
    expect(expense.notes).toBe('Pago com desconto');
    expect(expense.isActive).toBeTrue();
    expect(expense.isRecurring).toBeTrue();
    expect(expense.updatedAt).toBe('2024-01-09T10:00:00Z');
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
    expect(income.id).toBe('inc-uuid-001');
    expect(income.periodId).toBe('period-uuid-001');
    expect(income.userId).toBe('user-uuid-001');
    expect(income.fortnightType).toBe(FortnightType.Second);
    expect(income.description).toBe('Salário');
    expect(income.amount).toBe(3000.00);
    expect(income.receivedAt).toBe('2024-01-05');
    expect(income.notes).toBeNull();
    expect(income.isActive).toBeTrue();
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
    expect(service.expenses()[0].id).toBe('exp-uuid-001');
  });

  it('deve ignorar BOM UTF-8 antes de linha Income', () => {
    const bom = '﻿';
    const csv = bom + [HEADER, buildIncomeLine()].join('\n');
    service.parseCsv(csv);
    expect(service.incomes().length).toBe(1);
    expect(service.incomes()[0].id).toBe('inc-uuid-001');
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
    const exp1 = buildExpenseLine({ Id: 'e1', Amount: '500.00' });
    const exp2 = buildExpenseLine({ Id: 'e2', Amount: '250.50' });
    const csv  = [HEADER, exp1, exp2].join('\n');
    service.parseCsv(csv);
    expect(service.summary()!.totalExpense).toBeCloseTo(750.50, 2);
  });

  it('summary.totalIncome deve somar todos os rendimentos', () => {
    const inc1 = buildIncomeLine({ Id: 'i1', Amount: '2000.00' });
    const inc2 = buildIncomeLine({ Id: 'i2', Amount: '500.00' });
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
    const csv1 = [HEADER, buildExpenseLine({ Id: 'e1' })].join('\n');
    service.parseCsv(csv1);
    expect(service.expenses().length).toBe(1);

    const csv2 = [HEADER, buildIncomeLine({ Id: 'i1' })].join('\n');
    service.parseCsv(csv2);
    // Após segundo parse, expenses deve estar vazio (reset)
    expect(service.expenses().length).toBe(0);
    expect(service.incomes().length).toBe(1);
  });
});
