import { TestBed } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { PurgeDetailComponent } from './purge-detail.component';
import { CsvReaderService } from '../../core/services/csv-reader.service';
import {
  ExpenseResponse, IncomeResponse, PeriodSummary,
  PaymentStatus, FortnightType, SourceType,
} from '../../core/models/models';

// ── fixtures ──────────────────────────────────────────────────────────────────

const SUMMARY: PeriodSummary = {
  periodId:             'purge-period-1',
  userId:               'u',
  year:                 2024,
  month:                3,
  totalIncome:          5000,
  totalExpense:         3000,
  totalPaid:            2000,
  totalOwed:            1000,
  totalFirstFortnight:  1500,
  totalSecondFortnight: 1500,
  balance:              2000,
};

const EXPENSE_1: ExpenseResponse = {
  id:             'e-1',
  periodId:       'purge-period-1',
  userId:         'u',
  categoryId:     'cat-1',
  description:    'Aluguel',
  amount:         1500,
  dueDate:        '2024-03-05',
  paymentDate:    '2024-03-05',
  paymentStatus:  PaymentStatus.Paid,
  sourceType:     SourceType.Personal,
  fortnightType:  FortnightType.First,
  notes:          null,
  isActive:       true,
  isRecurring:    false,
  updatedAt:      '2024-03-05T10:00:00',
};

const EXPENSE_2: ExpenseResponse = {
  id:             'e-2',
  periodId:       'purge-period-1',
  userId:         'u',
  categoryId:     'cat-1',
  description:    'Internet',
  amount:         100,
  dueDate:        '2024-03-10',
  paymentDate:    null,
  paymentStatus:  PaymentStatus.Pending,
  sourceType:     SourceType.Parental,
  fortnightType:  FortnightType.Second,
  notes:          null,
  isActive:       true,
  isRecurring:    false,
  updatedAt:      '2024-03-10T10:00:00',
};

const INCOME_1: IncomeResponse = {
  id:            'i-1',
  periodId:      'purge-period-1',
  userId:        'u',
  fortnightType: FortnightType.First,
  description:   'Salário',
  amount:        5000,
  receivedAt:    '03/2024',
  notes:         null,
  isActive:      true,
};

// Fixtures extras — cobertura dos 4 valores de PaymentStatus e filtro combinado
const EXPENSE_3: ExpenseResponse = {
  id:             'e-3',
  periodId:       'purge-period-1',
  userId:         'u',
  categoryId:     'cat-1',
  description:    'Assinatura Cancelada',
  amount:         50,
  dueDate:        '2024-03-12',
  paymentDate:    null,
  paymentStatus:  PaymentStatus.Cancelled,
  sourceType:     SourceType.Personal,
  fortnightType:  FortnightType.First,
  notes:          null,
  isActive:       true,
  isRecurring:    false,
  updatedAt:      '2024-03-12T10:00:00',
};

const EXPENSE_4: ExpenseResponse = {
  id:             'e-4',
  periodId:       'purge-period-1',
  userId:         'u',
  categoryId:     'cat-1',
  description:    'Parcela Parcial',
  amount:         200,
  dueDate:        '2024-03-15',
  paymentDate:    '2024-03-15',
  paymentStatus:  PaymentStatus.Partial,
  sourceType:     SourceType.Parental,
  fortnightType:  FortnightType.Second,
  notes:          null,
  isActive:       true,
  isRecurring:    false,
  updatedAt:      '2024-03-15T10:00:00',
};

// Dataset para o filtro combinado (edge case 4): só a combinação exata de
// descrição + fonte + status + quinzena deve restringir ao subconjunto correto.
const COMBO_A: ExpenseResponse = {
  ...EXPENSE_1, id: 'combo-a', description: 'Aluguel Casa',
  sourceType: SourceType.Personal, paymentStatus: PaymentStatus.Paid, fortnightType: FortnightType.First,
};
const COMBO_B: ExpenseResponse = {
  ...EXPENSE_1, id: 'combo-b', description: 'Aluguel Carro',
  sourceType: SourceType.Personal, paymentStatus: PaymentStatus.Paid, fortnightType: FortnightType.Second,
};
const COMBO_C: ExpenseResponse = {
  ...EXPENSE_1, id: 'combo-c', description: 'Aluguel Casa',
  sourceType: SourceType.Parental, paymentStatus: PaymentStatus.Paid, fortnightType: FortnightType.First,
};
const COMBO_D: ExpenseResponse = {
  ...EXPENSE_1, id: 'combo-d', description: 'Aluguel Casa',
  sourceType: SourceType.Personal, paymentStatus: PaymentStatus.Pending, fortnightType: FortnightType.First,
};

// Alias tipado para acessar membros ainda não existentes (RED phase)
type AnyComponent = any;

// ── suite ─────────────────────────────────────────────────────────────────────

describe('PurgeDetailComponent', () => {
  let fixture: ComponentFixture<PurgeDetailComponent>;
  let component: PurgeDetailComponent;
  let c: AnyComponent; // alias para acesso sem type errors
  let csvSpy: jasmine.SpyObj<CsvReaderService>;

  beforeEach(async () => {
    // CsvReaderService usa signals — criamos spies que funcionam como signal()
    const expensesSignal = jasmine.createSpy('expenses').and.returnValue([]);
    const incomesSignal  = jasmine.createSpy('incomes').and.returnValue([]);
    const summarySignal  = jasmine.createSpy('summary').and.returnValue(null);

    csvSpy = {
      expenses: expensesSignal as any,
      incomes:  incomesSignal  as any,
      summary:  summarySignal  as any,
      parseCsv: jasmine.createSpy('parseCsv'),
    } as any;

    await TestBed.configureTestingModule({
      imports:   [PurgeDetailComponent, NoopAnimationsModule],
      providers: [{ provide: CsvReaderService, useValue: csvSpy }],
      schemas:   [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(PurgeDetailComponent);
    component = fixture.componentInstance;
    c         = component as AnyComponent;
  });

  // ── criação ────────────────────────────────────────────────────────────────

  it('cria o componente', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  // ── HeaderComponent ────────────────────────────────────────────────────────

  describe('HeaderComponent — app-header', () => {
    it('renderiza app-header no template', () => {
      fixture.detectChanges();
      const el = fixture.nativeElement.querySelector('app-header');
      expect(el).withContext('app-header deve existir no template do PurgeDetailComponent').not.toBeNull();
    });

    it('passa title="Análise Detalhe" ao app-header', () => {
      fixture.detectChanges();
      const el = fixture.nativeElement.querySelector('app-header');
      expect(el).not.toBeNull();
      const titleAttr = el.getAttribute('title') ?? el.getAttribute('ng-reflect-title') ?? '';
      expect(titleAttr).withContext('title deve ser "Análise Detalhe"').toContain('Análise Detalhe');
    });

    it('passa subtitle com info do CSV ao app-header', () => {
      fixture.detectChanges();
      const el = fixture.nativeElement.querySelector('app-header');
      expect(el).not.toBeNull();
      const subtitleAttr = el.getAttribute('subtitle') ?? el.getAttribute('ng-reflect-subtitle') ?? '';
      // subtitle deve ser uma string não vazia (info do CSV)
      expect(subtitleAttr.length).withContext('subtitle deve ter conteúdo informativo').toBeGreaterThan(0);
    });
  });

  // ── Sinal activeTab ────────────────────────────────────────────────────────

  describe('sinal activeTab', () => {
    it('possui sinal activeTab', () => {
      fixture.detectChanges();
      expect(c.activeTab).withContext('activeTab deve existir').toBeDefined();
    });

    it('activeTab inicia com "expenses"', () => {
      fixture.detectChanges();
      expect(c.activeTab()).withContext('activeTab inicial deve ser "expenses"').toBe('expenses');
    });

    it('activeTab aceita valor "incomes"', () => {
      fixture.detectChanges();
      c.activeTab.set('incomes');
      expect(c.activeTab()).toBe('incomes');
    });

    it('activeTab aceita valor "indicators"', () => {
      fixture.detectChanges();
      c.activeTab.set('indicators');
      expect(c.activeTab()).toBe('indicators');
    });
  });

  // ── Aba Despesas — grid ────────────────────────────────────────────────────

  describe('aba Despesas — grid de colunas', () => {
    beforeEach(() => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      csvSpy.incomes.and.returnValue([]);
      csvSpy.summary.and.returnValue(SUMMARY);
    });

    it('exibe cabeçalho "Descrição" na aba de despesas', () => {
      fixture.detectChanges();
      c.activeTab.set('expenses');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Descrição');
    });

    it('exibe cabeçalho "Valor" na aba de despesas', () => {
      fixture.detectChanges();
      c.activeTab.set('expenses');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Valor');
    });

    it('exibe cabeçalho "Vencimento" na aba de despesas', () => {
      fixture.detectChanges();
      c.activeTab.set('expenses');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Vencimento');
    });

    it('exibe cabeçalho "Pagamento" na aba de despesas', () => {
      fixture.detectChanges();
      c.activeTab.set('expenses');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Pagamento');
    });

    it('exibe cabeçalho "Status" na aba de despesas', () => {
      fixture.detectChanges();
      c.activeTab.set('expenses');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Status');
    });

    it('exibe cabeçalho "Fonte" na aba de despesas', () => {
      fixture.detectChanges();
      c.activeTab.set('expenses');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Fonte');
    });

    it('exibe cabeçalho "Quinzena" na aba de despesas', () => {
      fixture.detectChanges();
      c.activeTab.set('expenses');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Quinzena');
    });

    it('exibe dados das despesas filtradas', () => {
      fixture.detectChanges();
      c.activeTab.set('expenses');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Aluguel');
    });

    it('colunas do grid de despesas são clicáveis para ordenação', () => {
      fixture.detectChanges();
      c.activeTab.set('expenses');
      fixture.detectChanges();
      // Verifica que o componente tem método de sort ou sinal de ordenação exposto
      const hasSortMethod = typeof c.sortExpenses === 'function'
        || typeof c.sortColumn !== 'undefined'
        || typeof c.setSortColumn === 'function'
        || typeof c.sortExp === 'function'
        || typeof c.expSortColumn !== 'undefined';
      const ths = fixture.nativeElement.querySelectorAll('th[class*="sortable"], th.col-sortable');
      expect(hasSortMethod || ths.length > 0).withContext(
        'grid de despesas deve ter colunas clicáveis para ordenação (método sort ou classe sortable)'
      ).toBeTrue();
    });
  });

  // ── Aba Receitas — grid ────────────────────────────────────────────────────

  describe('aba Receitas — grid de colunas', () => {
    beforeEach(() => {
      csvSpy.expenses.and.returnValue([]);
      csvSpy.incomes.and.returnValue([INCOME_1]);
      csvSpy.summary.and.returnValue(SUMMARY);
    });

    it('exibe cabeçalho "Descrição" na aba de receitas', () => {
      fixture.detectChanges();
      c.activeTab.set('incomes');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Descrição');
    });

    it('exibe cabeçalho "Valor" na aba de receitas', () => {
      fixture.detectChanges();
      c.activeTab.set('incomes');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Valor');
    });

    it('exibe cabeçalho "Período" na aba de receitas', () => {
      fixture.detectChanges();
      c.activeTab.set('incomes');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Período');
    });

    it('exibe cabeçalho "Notas" na aba de receitas', () => {
      fixture.detectChanges();
      c.activeTab.set('incomes');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Notas');
    });

    it('exibe dados das receitas filtradas', () => {
      fixture.detectChanges();
      c.activeTab.set('incomes');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).toContain('Salário');
    });
  });

  // ── Aba Indicadores — KPIs ─────────────────────────────────────────────────

  describe('aba Indicadores — KPIs', () => {
    beforeEach(() => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      csvSpy.incomes.and.returnValue([INCOME_1]);
      csvSpy.summary.and.returnValue(SUMMARY);
    });

    it('exibe KPI totalIncome na aba de indicadores', () => {
      fixture.detectChanges();
      c.activeTab.set('indicators');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      const hasIncome = text.includes('Receita') || text.includes('5.000') || text.includes('5000');
      expect(hasIncome).withContext('totalIncome deve ser exibido na aba Indicadores').toBeTrue();
    });

    it('exibe KPI totalExpense na aba de indicadores', () => {
      fixture.detectChanges();
      c.activeTab.set('indicators');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      const hasExpense = text.includes('Despesa') || text.includes('3.000') || text.includes('3000');
      expect(hasExpense).withContext('totalExpense deve ser exibido na aba Indicadores').toBeTrue();
    });

    it('exibe KPI balance na aba de indicadores', () => {
      fixture.detectChanges();
      c.activeTab.set('indicators');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      expect(text).withContext('Saldo deve ser exibido na aba Indicadores').toContain('Saldo');
    });

    it('exibe KPI totalPaid na aba de indicadores', () => {
      fixture.detectChanges();
      c.activeTab.set('indicators');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      const hasPaid = text.includes('Pago') || text.includes('Total Pago') || text.includes('2.000') || text.includes('2000');
      expect(hasPaid).withContext('totalPaid deve ser exibido na aba Indicadores').toBeTrue();
    });

    it('exibe KPI totalOwed na aba de indicadores', () => {
      fixture.detectChanges();
      c.activeTab.set('indicators');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      const hasOwed = text.includes('A Pagar') || text.includes('Pendente') || text.includes('1.000') || text.includes('1000');
      expect(hasOwed).withContext('totalOwed deve ser exibido na aba Indicadores').toBeTrue();
    });

    it('exibe KPI de progresso de pagamento na aba de indicadores', () => {
      fixture.detectChanges();
      c.activeTab.set('indicators');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      const hasProgress = text.includes('Progresso') || text.includes('%');
      expect(hasProgress).withContext('Progresso de pagamento deve ser exibido na aba Indicadores').toBeTrue();
    });

    it('kpiTotalPaid é computed signal baseado nos dados filtrados de despesas', () => {
      fixture.detectChanges();
      expect(c.kpiTotalPaid).withContext('kpiTotalPaid deve ser um computed signal').toBeDefined();
      const val = c.kpiTotalPaid();
      expect(typeof val).withContext('kpiTotalPaid() deve retornar um número').toBe('number');
    });

    it('kpiTotalOwed é computed signal baseado nos dados filtrados de despesas', () => {
      fixture.detectChanges();
      expect(c.kpiTotalOwed).withContext('kpiTotalOwed deve ser um computed signal').toBeDefined();
      const val = c.kpiTotalOwed();
      expect(typeof val).withContext('kpiTotalOwed() deve retornar um número').toBe('number');
    });

    it('kpiBalance é computed signal', () => {
      fixture.detectChanges();
      expect(c.kpiBalance).withContext('kpiBalance deve ser um computed signal').toBeDefined();
      const val = c.kpiBalance();
      expect(typeof val).withContext('kpiBalance() deve retornar um número').toBe('number');
    });

    it('kpiPaymentProgress é computed signal entre 0 e 100', () => {
      fixture.detectChanges();
      expect(c.kpiPaymentProgress).withContext('kpiPaymentProgress deve ser um computed signal').toBeDefined();
      const val = c.kpiPaymentProgress();
      expect(val).toBeGreaterThanOrEqual(0);
      expect(val).toBeLessThanOrEqual(100);
    });

    it('kpiTotalPaid calcula apenas despesas com PaymentStatus.Paid nos filteredExpenses', () => {
      // EXPENSE_1 é Paid (1500), EXPENSE_2 é Pending (100)
      fixture.detectChanges();
      expect(c.kpiTotalPaid()).toBe(1500);
    });

    it('kpiTotalOwed calcula despesas Pending + Partial nos filteredExpenses', () => {
      // EXPENSE_2 é Pending (100)
      fixture.detectChanges();
      expect(c.kpiTotalOwed()).toBe(100);
    });
  });

  // ── filteredExpenses — computed signal (filtro por aba Despesas) ──────────

  describe('filteredExpenses — computed signal', () => {
    it('existe sinal filteredExpenses no componente', () => {
      fixture.detectChanges();
      expect(c.filteredExpenses).withContext('filteredExpenses deve ser um computed signal').toBeDefined();
    });

    it('retorna todas as despesas quando sem filtro', () => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      fixture.detectChanges();
      expect(c.filteredExpenses().length).toBe(2);
    });

    it('filtra despesas por expFilterDesc', () => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      fixture.detectChanges();
      c.expFilterDesc.set('Aluguel');
      expect(c.filteredExpenses().length).toBe(1);
      expect(c.filteredExpenses()[0].description).toBe('Aluguel');
    });

    it('filtra despesas por expFortnight', () => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      fixture.detectChanges();
      // EXPENSE_1 é First, EXPENSE_2 é Second
      c.expFortnight.set(FortnightType.First);
      expect(c.filteredExpenses().length).toBe(1);
      expect(c.filteredExpenses()[0].id).toBe('e-1');
    });

    it('filtra despesas por expStatus isoladamente — cobre os 4 valores de PaymentStatus', () => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2, EXPENSE_3, EXPENSE_4]);
      fixture.detectChanges();

      c.expStatus.set(PaymentStatus.Pending);
      expect(c.filteredExpenses().map((e: ExpenseResponse) => e.id)).toEqual(['e-2']);

      c.expStatus.set(PaymentStatus.Paid);
      expect(c.filteredExpenses().map((e: ExpenseResponse) => e.id)).toEqual(['e-1']);

      c.expStatus.set(PaymentStatus.Cancelled);
      expect(c.filteredExpenses().map((e: ExpenseResponse) => e.id)).toEqual(['e-3']);

      c.expStatus.set(PaymentStatus.Partial);
      expect(c.filteredExpenses().map((e: ExpenseResponse) => e.id)).toEqual(['e-4']);
    });

    it('filtra despesas por expSourceType isoladamente — cobre Parental e Personal', () => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      fixture.detectChanges();

      c.expSourceType.set(SourceType.Personal);
      expect(c.filteredExpenses().map((e: ExpenseResponse) => e.id)).toEqual(['e-1']);

      c.expSourceType.set(SourceType.Parental);
      expect(c.filteredExpenses().map((e: ExpenseResponse) => e.id)).toEqual(['e-2']);
    });

    it('filtro combinado (descrição + fonte + status + quinzena) restringe corretamente', () => {
      csvSpy.expenses.and.returnValue([COMBO_A, COMBO_B, COMBO_C, COMBO_D]);
      fixture.detectChanges();

      // Somente descrição → A, C, D (3 resultados)
      c.expFilterDesc.set('Aluguel Casa');
      expect(c.filteredExpenses().length).toBe(3);

      // + fonte Personal → A, D (2 resultados)
      c.expSourceType.set(SourceType.Personal);
      expect(c.filteredExpenses().length).toBe(2);

      // + status Paid → apenas A (1 resultado)
      c.expStatus.set(PaymentStatus.Paid);
      expect(c.filteredExpenses().length).toBe(1);

      // + quinzena First → continua apenas A
      c.expFortnight.set(FortnightType.First);
      expect(c.filteredExpenses().length).toBe(1);
      expect(c.filteredExpenses()[0].id).toBe('combo-a');
    });

    it('retorna lista vazia quando filtro não tem correspondência', () => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      fixture.detectChanges();
      c.expFilterDesc.set('XYZ não existe');
      expect(c.filteredExpenses().length).toBe(0);
    });

    it('setar expFilterDesc não afeta filteredIncomes (independência entre abas)', () => {
      const INCOME_2: IncomeResponse = { ...INCOME_1, id: 'i-2', description: 'Freelance' };
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      csvSpy.incomes.and.returnValue([INCOME_1, INCOME_2]);
      fixture.detectChanges();

      const incomesBefore = c.filteredIncomes().length;
      c.expFilterDesc.set('Aluguel');
      expect(c.filteredIncomes().length).toBe(incomesBefore);
    });
  });

  // ── filteredIncomes — computed signal (filtro por aba Receitas) ───────────

  describe('filteredIncomes — computed signal', () => {
    it('existe sinal filteredIncomes no componente', () => {
      fixture.detectChanges();
      expect(c.filteredIncomes).withContext('filteredIncomes deve ser um computed signal').toBeDefined();
    });

    it('retorna todas as receitas quando sem filtro', () => {
      csvSpy.incomes.and.returnValue([INCOME_1]);
      fixture.detectChanges();
      expect(c.filteredIncomes().length).toBe(1);
    });

    it('filtra receitas por incFilterDesc', () => {
      const INCOME_2: IncomeResponse = { ...INCOME_1, id: 'i-2', description: 'Freelance' };
      csvSpy.incomes.and.returnValue([INCOME_1, INCOME_2]);
      fixture.detectChanges();
      c.incFilterDesc.set('Freela');
      expect(c.filteredIncomes().length).toBe(1);
      expect(c.filteredIncomes()[0].description).toBe('Freelance');
    });

    it('setar incFilterDesc não afeta filteredExpenses (independência entre abas)', () => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      csvSpy.incomes.and.returnValue([INCOME_1]);
      fixture.detectChanges();

      const expensesBefore = c.filteredExpenses().length;
      c.incFilterDesc.set('Salário');
      expect(c.filteredExpenses().length).toBe(expensesBefore);
    });
  });

  // ── FilterModalComponent e FilterButtonComponent ───────────────────────────

  describe('imports — FilterModalComponent e FilterButtonComponent', () => {
    it('FilterModalComponent está nos imports do PurgeDetailComponent', () => {
      const rawDeps: any = (PurgeDetailComponent as any).ɵcmp?.dependencies;
      const deps: any[] = Array.isArray(rawDeps) ? rawDeps
        : (typeof rawDeps === 'function' ? rawDeps() : []);
      const hasFilterModal = deps.some((dep: any) => {
        const sel: string = dep?.ɵcmp?.selectors?.[0]?.[0] ?? dep?.ɵdir?.selectors?.[0]?.[0] ?? '';
        return sel === 'app-filter-modal';
      });
      expect(hasFilterModal).withContext('FilterModalComponent deve estar nos imports do PurgeDetailComponent').toBeTrue();
    });

    it('FilterButtonComponent está nos imports do PurgeDetailComponent', () => {
      const rawDeps: any = (PurgeDetailComponent as any).ɵcmp?.dependencies;
      const deps: any[] = Array.isArray(rawDeps) ? rawDeps
        : (typeof rawDeps === 'function' ? rawDeps() : []);
      const hasFilterButton = deps.some((dep: any) => {
        const sel: string = dep?.ɵcmp?.selectors?.[0]?.[0] ?? dep?.ɵdir?.selectors?.[0]?.[0] ?? '';
        return sel === 'app-filter-button';
      });
      expect(hasFilterButton).withContext('FilterButtonComponent deve estar nos imports do PurgeDetailComponent').toBeTrue();
    });
  });

  // ── Filtros por aba — sinais expFilterDesc / incFilterDesc / expFortnight ─

  describe('filtros por aba — independência entre Despesas e Receitas', () => {
    it('existe sinal expFilterDesc no componente', () => {
      fixture.detectChanges();
      expect(c.expFilterDesc).withContext('expFilterDesc deve existir no componente').toBeDefined();
    });

    it('expFilterDesc inicia como string vazia', () => {
      fixture.detectChanges();
      expect(c.expFilterDesc()).toBe('');
    });

    it('existe sinal incFilterDesc no componente', () => {
      fixture.detectChanges();
      expect(c.incFilterDesc).withContext('incFilterDesc deve existir no componente').toBeDefined();
    });

    it('incFilterDesc inicia como string vazia', () => {
      fixture.detectChanges();
      expect(c.incFilterDesc()).toBe('');
    });

    it('existe sinal expFortnight no componente', () => {
      fixture.detectChanges();
      expect(c.expFortnight).withContext('expFortnight deve existir no componente').toBeDefined();
    });

    it('expFortnight inicia como null', () => {
      fixture.detectChanges();
      expect(c.expFortnight()).toBeNull();
    });

    it('existe sinal expStatus no componente, iniciando como null', () => {
      fixture.detectChanges();
      expect(c.expStatus).withContext('expStatus deve existir no componente').toBeDefined();
      expect(c.expStatus()).toBeNull();
    });

    it('existe sinal expSourceType no componente, iniciando como null', () => {
      fixture.detectChanges();
      expect(c.expSourceType).withContext('expSourceType deve existir no componente').toBeDefined();
      expect(c.expSourceType()).toBeNull();
    });

    it('expFilterDesc e incFilterDesc são independentes — setar um não altera o outro', () => {
      fixture.detectChanges();
      c.expFilterDesc.set('Aluguel');
      expect(c.incFilterDesc()).toBe('');

      c.incFilterDesc.set('Salário');
      expect(c.expFilterDesc()).toBe('Aluguel');
    });
  });

  // ── onExpFilterApply() — aplica filtros da aba Despesas ────────────────────

  describe('onExpFilterApply() — aplica filtros da aba Despesas', () => {
    it('existe método onExpFilterApply no componente', () => {
      fixture.detectChanges();
      const hasMeth = typeof c.onExpFilterApply === 'function';
      expect(hasMeth).withContext('onExpFilterApply deve ser um método do componente').toBeTrue();
    });

    it('onExpFilterApply define expFilterDesc a partir da chave "description"', () => {
      fixture.detectChanges();
      c.onExpFilterApply({ description: 'aluguel' });
      expect(c.expFilterDesc()).toBe('aluguel');
    });

    it('onExpFilterApply define expFortnight a partir da chave "fortnightType" (string numérica → enum)', () => {
      fixture.detectChanges();
      c.onExpFilterApply({ fortnightType: `${FortnightType.Second}` });
      expect(c.expFortnight()).toBe(FortnightType.Second);
    });

    it('onExpFilterApply define expStatus a partir da chave "paymentStatus" (string numérica → enum)', () => {
      fixture.detectChanges();
      c.onExpFilterApply({ paymentStatus: `${PaymentStatus.Partial}` });
      expect(c.expStatus()).toBe(PaymentStatus.Partial);
    });

    it('onExpFilterApply define expSourceType a partir da chave "sourceType" (string numérica → enum)', () => {
      fixture.detectChanges();
      c.onExpFilterApply({ sourceType: `${SourceType.Parental}` });
      expect(c.expSourceType()).toBe(SourceType.Parental);
    });

    it('onExpFilterApply com valores vazios ("") resulta em null — não em 0 ou NaN', () => {
      fixture.detectChanges();
      c.expFortnight.set(FortnightType.First);
      c.expStatus.set(PaymentStatus.Paid);
      c.expSourceType.set(SourceType.Personal);

      c.onExpFilterApply({ fortnightType: '', paymentStatus: '', sourceType: '' });

      expect(c.expFortnight()).toBeNull();
      expect(c.expStatus()).toBeNull();
      expect(c.expSourceType()).toBeNull();
    });

    it('onExpFilterApply fecha expFilterOpen ao aplicar', () => {
      fixture.detectChanges();
      c.expFilterOpen.set(true);
      c.onExpFilterApply({ description: 'x' });
      expect(c.expFilterOpen()).toBeFalse();
    });

    it('onExpFilterApply não altera incFilterOpen nem os filtros da aba Receitas', () => {
      fixture.detectChanges();
      c.incFilterOpen.set(true);
      c.onExpFilterApply({ description: 'aluguel' });
      expect(c.incFilterOpen()).toBeTrue();
      expect(c.incFilterDesc()).toBe('');
    });
  });

  // ── onIncFilterApply() — aplica filtro da aba Receitas ─────────────────────

  describe('onIncFilterApply() — aplica filtro da aba Receitas', () => {
    it('existe método onIncFilterApply no componente', () => {
      fixture.detectChanges();
      const hasMeth = typeof c.onIncFilterApply === 'function';
      expect(hasMeth).withContext('onIncFilterApply deve ser um método do componente').toBeTrue();
    });

    it('onIncFilterApply define incFilterDesc a partir da chave "description"', () => {
      fixture.detectChanges();
      c.onIncFilterApply({ description: 'salário' });
      expect(c.incFilterDesc()).toBe('salário');
    });

    it('onIncFilterApply com description vazia resulta em string vazia', () => {
      fixture.detectChanges();
      c.incFilterDesc.set('algo');
      c.onIncFilterApply({ description: '' });
      expect(c.incFilterDesc()).toBe('');
    });

    it('onIncFilterApply fecha incFilterOpen ao aplicar', () => {
      fixture.detectChanges();
      c.incFilterOpen.set(true);
      c.onIncFilterApply({ description: 'x' });
      expect(c.incFilterOpen()).toBeFalse();
    });

    it('onIncFilterApply não altera expFilterOpen nem os filtros da aba Despesas', () => {
      fixture.detectChanges();
      c.expFilterOpen.set(true);
      c.onIncFilterApply({ description: 'salário' });
      expect(c.expFilterOpen()).toBeTrue();
      expect(c.expFilterDesc()).toBe('');
    });
  });

  // ── onExpFilterClear() / onIncFilterClear() — limpam filtros da respectiva aba ──

  describe('onExpFilterClear() e onIncFilterClear() — limpam filtros por aba', () => {
    it('existe método onExpFilterClear no componente', () => {
      fixture.detectChanges();
      const hasMeth = typeof c.onExpFilterClear === 'function';
      expect(hasMeth).withContext('onExpFilterClear deve ser um método do componente').toBeTrue();
    });

    it('existe método onIncFilterClear no componente', () => {
      fixture.detectChanges();
      const hasMeth = typeof c.onIncFilterClear === 'function';
      expect(hasMeth).withContext('onIncFilterClear deve ser um método do componente').toBeTrue();
    });

    it('onExpFilterClear reseta expFilterDesc, expFortnight, expStatus e expSourceType para o estado neutro', () => {
      fixture.detectChanges();
      c.expFilterDesc.set('algo');
      c.expFortnight.set(FortnightType.First);
      c.expStatus.set(PaymentStatus.Paid);
      c.expSourceType.set(SourceType.Personal);

      c.onExpFilterClear();

      expect(c.expFilterDesc()).toBe('');
      expect(c.expFortnight()).toBeNull();
      expect(c.expStatus()).toBeNull();
      expect(c.expSourceType()).toBeNull();
    });

    it('onExpFilterClear não altera os filtros da aba Receitas', () => {
      fixture.detectChanges();
      c.incFilterDesc.set('salário');
      c.onExpFilterClear();
      expect(c.incFilterDesc()).toBe('salário');
    });

    it('onIncFilterClear reseta incFilterDesc para o estado neutro', () => {
      fixture.detectChanges();
      c.incFilterDesc.set('algo');
      c.onIncFilterClear();
      expect(c.incFilterDesc()).toBe('');
    });

    it('onIncFilterClear não altera os filtros da aba Despesas', () => {
      fixture.detectChanges();
      c.expFilterDesc.set('aluguel');
      c.expStatus.set(PaymentStatus.Paid);
      c.onIncFilterClear();
      expect(c.expFilterDesc()).toBe('aluguel');
      expect(c.expStatus()).toBe(PaymentStatus.Paid);
    });
  });

  // ── expFilterFields / incFilterFields — computeds por aba ─────────────────

  describe('expFilterFields computed — 4 campos (sem categoria)', () => {
    it('existe expFilterFields no componente', () => {
      fixture.detectChanges();
      expect(c.expFilterFields).withContext('expFilterFields deve existir').toBeDefined();
    });

    it('expFilterFields possui exatamente 4 entradas', () => {
      fixture.detectChanges();
      expect(c.expFilterFields().length).toBe(4);
    });

    it('expFilterFields contém exatamente as keys description, sourceType, paymentStatus, fortnightType', () => {
      fixture.detectChanges();
      const keys = c.expFilterFields().map((f: any) => f.key).sort();
      expect(keys).toEqual(['description', 'fortnightType', 'paymentStatus', 'sourceType']);
    });

    it('expFilterFields NÃO contém campo de categoria (categoryId/category)', () => {
      fixture.detectChanges();
      const keys = c.expFilterFields().map((f: any) => f.key);
      expect(keys).not.toContain('categoryId');
      expect(keys).not.toContain('category');
    });
  });

  describe('incFilterFields computed — 1 campo apenas (sem quinzena)', () => {
    it('existe incFilterFields no componente', () => {
      fixture.detectChanges();
      expect(c.incFilterFields).withContext('incFilterFields deve existir').toBeDefined();
    });

    it('incFilterFields possui exatamente 1 entrada', () => {
      fixture.detectChanges();
      expect(c.incFilterFields().length).toBe(1);
    });

    it('incFilterFields contém apenas a key "description"', () => {
      fixture.detectChanges();
      const keys = c.incFilterFields().map((f: any) => f.key);
      expect(keys).toEqual(['description']);
    });

    it('incFilterFields NÃO contém campo "fortnightType"', () => {
      fixture.detectChanges();
      const keys = c.incFilterFields().map((f: any) => f.key);
      expect(keys).not.toContain('fortnightType');
    });
  });

  // ── KPIs com filtros ativos na aba Indicadores ────────────────────────────

  describe('aba Indicadores — KPIs refletem dados filtrados', () => {
    beforeEach(() => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      csvSpy.incomes.and.returnValue([INCOME_1]);
      csvSpy.summary.and.returnValue(SUMMARY);
    });

    it('kpiTotalPaid recalcula com expFilterDesc ativo', () => {
      fixture.detectChanges();
      // Sem filtro: EXPENSE_1 (Paid 1500) + EXPENSE_2 (Pending 100) → pago = 1500
      expect(c.kpiTotalPaid()).toBe(1500);

      // Com filtro "Internet" → apenas EXPENSE_2 (Pending) → pago = 0
      c.expFilterDesc.set('Internet');
      expect(c.kpiTotalPaid()).toBe(0);
    });

    it('kpiTotalOwed recalcula com expFilterDesc ativo', () => {
      fixture.detectChanges();
      // Sem filtro: EXPENSE_2 Pending (100) → owed = 100
      expect(c.kpiTotalOwed()).toBe(100);

      // Com filtro "Aluguel" → apenas EXPENSE_1 (Paid) → owed = 0
      c.expFilterDesc.set('Aluguel');
      expect(c.kpiTotalOwed()).toBe(0);
    });

    it('kpiBalance deriva de filteredIncomes - filteredExpenses quando expFilterDesc está ativo', () => {
      fixture.detectChanges();
      c.expFilterDesc.set('Aluguel');
      // filteredExpenses = [EXPENSE_1(1500)], filteredIncomes = [] (nenhum "Aluguel" em receitas)
      const expTotal = c.filteredExpenses().reduce((s: number, e: ExpenseResponse) => s + e.amount, 0);
      const incTotal = c.filteredIncomes().reduce((s: number, i: IncomeResponse) => s + i.amount, 0);
      expect(c.kpiBalance()).toBe(incTotal - expTotal);
    });

    it('setar expFilterDesc não altera kpiTotalIncome (independência entre KPIs de despesa e receita)', () => {
      fixture.detectChanges();
      const incomeBefore = c.kpiTotalIncome();
      c.expFilterDesc.set('Aluguel');
      expect(c.kpiTotalIncome()).toBe(incomeBefore);
    });

    it('setar incFilterDesc não altera kpiTotalPaid, kpiTotalOwed nem kpiTotalExpense', () => {
      fixture.detectChanges();
      const paidBefore    = c.kpiTotalPaid();
      const owedBefore    = c.kpiTotalOwed();
      const expenseBefore = c.kpiTotalExpense();

      c.incFilterDesc.set('Salário');

      expect(c.kpiTotalPaid()).toBe(paidBefore);
      expect(c.kpiTotalOwed()).toBe(owedBefore);
      expect(c.kpiTotalExpense()).toBe(expenseBefore);
    });

    it('kpiBalance reflete alterações independentes de expFilterDesc e incFilterDesc', () => {
      fixture.detectChanges();
      // Baseline: totalIncome (5000) - totalExpense (1600) = 3400
      expect(c.kpiBalance()).toBe(5000 - 1600);

      // Filtra despesas por "Aluguel" → totalExpense passa a 1500, income inalterado
      c.expFilterDesc.set('Aluguel');
      expect(c.kpiBalance()).toBe(5000 - 1500);

      // Adicionalmente filtra receitas por termo que não bate com "Salário" → income vira 0
      c.incFilterDesc.set('Inexistente');
      expect(c.kpiBalance()).toBe(0 - 1500);
    });
  });

  // ── Estado vazio ───────────────────────────────────────────────────────────

  describe('edge case — sem dados carregados', () => {
    beforeEach(() => {
      csvSpy.expenses.and.returnValue([]);
      csvSpy.incomes.and.returnValue([]);
      csvSpy.summary.and.returnValue(null);
    });

    it('exibe estado vazio na aba Despesas quando não há despesas', () => {
      fixture.detectChanges();
      c.activeTab.set('expenses');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      const hasEmpty = text.includes('Nenhum') || text.includes('vazio') || text.includes('dados');
      expect(hasEmpty).withContext('deve exibir mensagem de estado vazio na aba Despesas').toBeTrue();
    });

    it('exibe estado vazio na aba Receitas quando não há receitas', () => {
      fixture.detectChanges();
      c.activeTab.set('incomes');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      const hasEmpty = text.includes('Nenhum') || text.includes('vazio') || text.includes('dados');
      expect(hasEmpty).withContext('deve exibir mensagem de estado vazio na aba Receitas').toBeTrue();
    });

    it('kpiTotalPaid retorna 0 sem dados', () => {
      fixture.detectChanges();
      expect(c.kpiTotalPaid()).toBe(0);
    });

    it('kpiTotalOwed retorna 0 sem dados', () => {
      fixture.detectChanges();
      expect(c.kpiTotalOwed()).toBe(0);
    });

    it('kpiBalance retorna 0 sem dados', () => {
      fixture.detectChanges();
      expect(c.kpiBalance()).toBe(0);
    });
  });

  // ── Edge case — filtro sem resultados ──────────────────────────────────────

  describe('edge case — filtro aplicado sem resultados', () => {
    beforeEach(() => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      csvSpy.incomes.and.returnValue([INCOME_1]);
      csvSpy.summary.and.returnValue(SUMMARY);
    });

    it('filteredExpenses retorna lista vazia quando expFilterDesc não tem correspondência', () => {
      fixture.detectChanges();
      c.expFilterDesc.set('ITEM_QUE_NAO_EXISTE_XYZABC');
      expect(c.filteredExpenses().length).toBe(0);
    });

    it('filteredIncomes retorna lista vazia quando incFilterDesc não tem correspondência', () => {
      fixture.detectChanges();
      c.incFilterDesc.set('ITEM_QUE_NAO_EXISTE_XYZABC');
      expect(c.filteredIncomes().length).toBe(0);
    });

    it('aba Despesas exibe mensagem quando expFilterDesc não retorna resultados', () => {
      fixture.detectChanges();
      c.expFilterDesc.set('ITEM_QUE_NAO_EXISTE_XYZABC');
      c.activeTab.set('expenses');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      const hasEmpty = text.includes('Nenhum') || text.includes('resultado') || text.includes('encontrado') || text.includes('dados');
      expect(hasEmpty).withContext('deve exibir mensagem quando filtro não tem resultados na aba Despesas').toBeTrue();
    });
  });

  // ── expActiveFilterCount — contador de filtros ativos da aba Despesas ─────

  describe('expActiveFilterCount — contador para FilterButtonComponent (Despesas)', () => {
    it('existe expActiveFilterCount no componente', () => {
      fixture.detectChanges();
      expect(c.expActiveFilterCount).withContext('expActiveFilterCount deve existir').toBeDefined();
    });

    it('expActiveFilterCount é 0 sem filtros', () => {
      fixture.detectChanges();
      expect(c.expActiveFilterCount()).toBe(0);
    });

    it('expActiveFilterCount é 1 com expFilterDesc preenchido', () => {
      fixture.detectChanges();
      c.expFilterDesc.set('algo');
      expect(c.expActiveFilterCount()).toBe(1);
    });

    it('expActiveFilterCount é 1 com expFortnight preenchido', () => {
      fixture.detectChanges();
      c.expFortnight.set(FortnightType.First);
      expect(c.expActiveFilterCount()).toBe(1);
    });

    it('expActiveFilterCount é 2 com status e fonte preenchidos (incremento parcial)', () => {
      fixture.detectChanges();
      c.expStatus.set(PaymentStatus.Paid);
      c.expSourceType.set(SourceType.Personal);
      expect(c.expActiveFilterCount()).toBe(2);
    });

    it('expActiveFilterCount é 4 com os 4 filtros preenchidos', () => {
      fixture.detectChanges();
      c.expFilterDesc.set('algo');
      c.expFortnight.set(FortnightType.First);
      c.expStatus.set(PaymentStatus.Paid);
      c.expSourceType.set(SourceType.Personal);
      expect(c.expActiveFilterCount()).toBe(4);
    });
  });

  // ── incActiveFilterCount — contador de filtros ativos da aba Receitas ─────

  describe('incActiveFilterCount — contador para FilterButtonComponent (Receitas)', () => {
    it('existe incActiveFilterCount no componente', () => {
      fixture.detectChanges();
      expect(c.incActiveFilterCount).withContext('incActiveFilterCount deve existir').toBeDefined();
    });

    it('incActiveFilterCount é 0 sem filtro', () => {
      fixture.detectChanges();
      expect(c.incActiveFilterCount()).toBe(0);
    });

    it('incActiveFilterCount é 1 com incFilterDesc preenchido', () => {
      fixture.detectChanges();
      c.incFilterDesc.set('algo');
      expect(c.incActiveFilterCount()).toBe(1);
    });
  });

  // ── expFilterOpen / incFilterOpen — sinais independentes de modal ─────────

  describe('expFilterOpen e incFilterOpen — controle independente dos modais de filtro', () => {
    it('existe sinal expFilterOpen no componente', () => {
      fixture.detectChanges();
      expect(c.expFilterOpen).withContext('expFilterOpen deve existir').toBeDefined();
    });

    it('expFilterOpen inicia como false', () => {
      fixture.detectChanges();
      expect(c.expFilterOpen()).toBeFalse();
    });

    it('existe sinal incFilterOpen no componente', () => {
      fixture.detectChanges();
      expect(c.incFilterOpen).withContext('incFilterOpen deve existir').toBeDefined();
    });

    it('incFilterOpen inicia como false', () => {
      fixture.detectChanges();
      expect(c.incFilterOpen()).toBeFalse();
    });

    it('abrir expFilterOpen não abre incFilterOpen', () => {
      fixture.detectChanges();
      c.expFilterOpen.set(true);
      expect(c.incFilterOpen()).toBeFalse();
    });

    it('abrir incFilterOpen não abre expFilterOpen', () => {
      fixture.detectChanges();
      c.incFilterOpen.set(true);
      expect(c.expFilterOpen()).toBeFalse();
    });
  });
});
