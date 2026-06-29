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

  // ── filteredExpenses — computed signal ─────────────────────────────────────

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

    it('filtra despesas por descrição ao aplicar filtro', () => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      fixture.detectChanges();
      c.filterDesc.set('Aluguel');
      expect(c.filteredExpenses().length).toBe(1);
      expect(c.filteredExpenses()[0].description).toBe('Aluguel');
    });

    it('filtra despesas por quinzena ao aplicar filtro', () => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      fixture.detectChanges();
      // EXPENSE_1 é First, EXPENSE_2 é Second
      c.filterFortnight.set(FortnightType.First);
      expect(c.filteredExpenses().length).toBe(1);
      expect(c.filteredExpenses()[0].id).toBe('e-1');
    });

    it('retorna lista vazia quando filtro não tem correspondência', () => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      fixture.detectChanges();
      c.filterDesc.set('XYZ não existe');
      expect(c.filteredExpenses().length).toBe(0);
    });
  });

  // ── filteredIncomes — computed signal ──────────────────────────────────────

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

    it('filtra receitas por descrição ao aplicar filtro', () => {
      const INCOME_2: IncomeResponse = { ...INCOME_1, id: 'i-2', description: 'Freelance' };
      csvSpy.incomes.and.returnValue([INCOME_1, INCOME_2]);
      fixture.detectChanges();
      c.filterDesc.set('Freela');
      expect(c.filteredIncomes().length).toBe(1);
      expect(c.filteredIncomes()[0].description).toBe('Freelance');
    });
  });

  // ── FilterModalComponent e FilterButtonComponent ───────────────────────────

  describe('imports — FilterModalComponent e FilterButtonComponent', () => {
    it('FilterModalComponent está nos imports do PurgeDetailComponent', () => {
      const rawDeps: any = (PurgeDetailComponent as any).ɵcmp?.dependencies;
      const deps: any[] = Array.isArray(rawDeps) ? rawDeps
        : (typeof rawDeps === 'function' ? rawDeps() : []);
      const hasFilterModal = deps.some((dep: any) => {
        const sel: string = dep?.ɵcmp?.selectors?.[0]?.[1] ?? dep?.ɵdir?.selectors?.[0]?.[1] ?? '';
        return sel === 'app-filter-modal';
      });
      expect(hasFilterModal).withContext('FilterModalComponent deve estar nos imports do PurgeDetailComponent').toBeTrue();
    });

    it('FilterButtonComponent está nos imports do PurgeDetailComponent', () => {
      const rawDeps: any = (PurgeDetailComponent as any).ɵcmp?.dependencies;
      const deps: any[] = Array.isArray(rawDeps) ? rawDeps
        : (typeof rawDeps === 'function' ? rawDeps() : []);
      const hasFilterButton = deps.some((dep: any) => {
        const sel: string = dep?.ɵcmp?.selectors?.[0]?.[1] ?? dep?.ɵdir?.selectors?.[0]?.[1] ?? '';
        return sel === 'app-filter-button';
      });
      expect(hasFilterButton).withContext('FilterButtonComponent deve estar nos imports do PurgeDetailComponent').toBeTrue();
    });
  });

  // ── Filtros compartilhados — filterDesc e filterFortnight ─────────────────

  describe('filtros compartilhados — filterDesc e filterFortnight', () => {
    it('existe sinal filterDesc no componente', () => {
      fixture.detectChanges();
      expect(c.filterDesc).withContext('filterDesc deve existir no componente').toBeDefined();
    });

    it('filterDesc inicia como string vazia', () => {
      fixture.detectChanges();
      expect(c.filterDesc()).toBe('');
    });

    it('existe sinal filterFortnight no componente', () => {
      fixture.detectChanges();
      expect(c.filterFortnight).withContext('filterFortnight deve existir no componente').toBeDefined();
    });

    it('filterFortnight inicia como null', () => {
      fixture.detectChanges();
      expect(c.filterFortnight()).toBeNull();
    });

    it('filtros aplicam-se simultaneamente em despesas e receitas', () => {
      const INCOME_2: IncomeResponse = { ...INCOME_1, id: 'i-2', description: 'Freelance' };
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      csvSpy.incomes.and.returnValue([INCOME_1, INCOME_2]);
      fixture.detectChanges();

      c.filterDesc.set('Al');

      // Filtra despesas por "Al" → apenas EXPENSE_1 (Aluguel)
      expect(c.filteredExpenses().length).toBe(1);
      // O mesmo filterDesc é compartilhado — resultado deve ser <= total
      const expFiltered = c.filteredExpenses().length;
      const incFiltered = c.filteredIncomes().length;
      expect(expFiltered).toBeLessThanOrEqual(2);
      expect(incFiltered).toBeLessThanOrEqual(2);
    });
  });

  // ── onFilterApply() — método de aplicação de filtros ──────────────────────

  describe('onFilterApply() — aplica filtros às 3 abas', () => {
    it('existe método onFilterApply no componente', () => {
      fixture.detectChanges();
      const hasMeth = typeof c.onFilterApply === 'function';
      expect(hasMeth).withContext('onFilterApply deve ser um método do componente').toBeTrue();
    });

    it('onFilterApply define filterDesc a partir da chave "description"', () => {
      fixture.detectChanges();
      c.onFilterApply({ description: 'aluguel' });
      expect(c.filterDesc()).toBe('aluguel');
    });

    it('onFilterApply define filterFortnight a partir da chave "fortnightType"', () => {
      fixture.detectChanges();
      c.onFilterApply({ fortnightType: `${FortnightType.Second}` });
      expect(c.filterFortnight()).toBe(FortnightType.Second);
    });

    it('onFilterApply com fortnightType vazio limpa o filtro', () => {
      fixture.detectChanges();
      c.filterFortnight.set(FortnightType.First);
      c.onFilterApply({ fortnightType: '' });
      expect(c.filterFortnight()).toBeNull();
    });
  });

  // ── onFilterClear() — limpa todos os filtros ───────────────────────────────

  describe('onFilterClear() — limpa filtros', () => {
    it('existe método onFilterClear no componente', () => {
      fixture.detectChanges();
      const hasMeth = typeof c.onFilterClear === 'function';
      expect(hasMeth).withContext('onFilterClear deve ser um método do componente').toBeTrue();
    });

    it('onFilterClear limpa filterDesc', () => {
      fixture.detectChanges();
      c.filterDesc.set('algo');
      c.onFilterClear();
      expect(c.filterDesc()).toBe('');
    });

    it('onFilterClear limpa filterFortnight', () => {
      fixture.detectChanges();
      c.filterFortnight.set(FortnightType.First);
      c.onFilterClear();
      expect(c.filterFortnight()).toBeNull();
    });
  });

  // ── filterFields computed ──────────────────────────────────────────────────

  describe('filterFields computed', () => {
    it('existe filterFields no componente', () => {
      fixture.detectChanges();
      expect(c.filterFields).withContext('filterFields deve existir').toBeDefined();
    });

    it('filterFields contém campo "description"', () => {
      fixture.detectChanges();
      const fields = c.filterFields();
      const hasDesc = fields.some((f: any) => f.key === 'description');
      expect(hasDesc).withContext('filterFields deve conter campo "description"').toBeTrue();
    });

    it('filterFields contém campo "fortnightType"', () => {
      fixture.detectChanges();
      const fields = c.filterFields();
      const hasFortnight = fields.some((f: any) => f.key === 'fortnightType');
      expect(hasFortnight).withContext('filterFields deve conter campo "fortnightType"').toBeTrue();
    });
  });

  // ── KPIs com filtros ativos na aba Indicadores ────────────────────────────

  describe('aba Indicadores — KPIs refletem dados filtrados', () => {
    beforeEach(() => {
      csvSpy.expenses.and.returnValue([EXPENSE_1, EXPENSE_2]);
      csvSpy.incomes.and.returnValue([INCOME_1]);
      csvSpy.summary.and.returnValue(SUMMARY);
    });

    it('kpiTotalPaid recalcula com filtro de descrição ativo', () => {
      fixture.detectChanges();
      // Sem filtro: EXPENSE_1 (Paid 1500) + EXPENSE_2 (Pending 100) → pago = 1500
      expect(c.kpiTotalPaid()).toBe(1500);

      // Com filtro "Internet" → apenas EXPENSE_2 (Pending) → pago = 0
      c.filterDesc.set('Internet');
      expect(c.kpiTotalPaid()).toBe(0);
    });

    it('kpiTotalOwed recalcula com filtro ativo', () => {
      fixture.detectChanges();
      // Sem filtro: EXPENSE_2 Pending (100) → owed = 100
      expect(c.kpiTotalOwed()).toBe(100);

      // Com filtro "Aluguel" → apenas EXPENSE_1 (Paid) → owed = 0
      c.filterDesc.set('Aluguel');
      expect(c.kpiTotalOwed()).toBe(0);
    });

    it('kpiBalance deriva de filteredIncomes - filteredExpenses quando há filtro', () => {
      fixture.detectChanges();
      c.filterDesc.set('Aluguel');
      // filteredExpenses = [EXPENSE_1(1500)], filteredIncomes = [] (nenhum "Aluguel" em receitas)
      const expTotal = c.filteredExpenses().reduce((s: number, e: ExpenseResponse) => s + e.amount, 0);
      const incTotal = c.filteredIncomes().reduce((s: number, i: IncomeResponse) => s + i.amount, 0);
      expect(c.kpiBalance()).toBe(incTotal - expTotal);
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

    it('filteredExpenses retorna lista vazia quando filtro não tem correspondência', () => {
      fixture.detectChanges();
      c.filterDesc.set('ITEM_QUE_NAO_EXISTE_XYZABC');
      expect(c.filteredExpenses().length).toBe(0);
    });

    it('filteredIncomes retorna lista vazia quando filtro não tem correspondência', () => {
      fixture.detectChanges();
      c.filterDesc.set('ITEM_QUE_NAO_EXISTE_XYZABC');
      expect(c.filteredIncomes().length).toBe(0);
    });

    it('aba Despesas exibe mensagem quando filtro não retorna resultados', () => {
      fixture.detectChanges();
      c.filterDesc.set('ITEM_QUE_NAO_EXISTE_XYZABC');
      c.activeTab.set('expenses');
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent as string;
      const hasEmpty = text.includes('Nenhum') || text.includes('resultado') || text.includes('encontrado') || text.includes('dados');
      expect(hasEmpty).withContext('deve exibir mensagem quando filtro não tem resultados na aba Despesas').toBeTrue();
    });
  });

  // ── activeFilterCount — contador de filtros ativos ────────────────────────

  describe('activeFilterCount — contador para FilterButtonComponent', () => {
    it('existe activeFilterCount no componente', () => {
      fixture.detectChanges();
      expect(c.activeFilterCount).withContext('activeFilterCount deve existir').toBeDefined();
    });

    it('activeFilterCount é 0 sem filtros', () => {
      fixture.detectChanges();
      expect(c.activeFilterCount()).toBe(0);
    });

    it('activeFilterCount é 1 com filterDesc preenchido', () => {
      fixture.detectChanges();
      c.filterDesc.set('algo');
      expect(c.activeFilterCount()).toBe(1);
    });

    it('activeFilterCount é 1 com filterFortnight preenchido', () => {
      fixture.detectChanges();
      c.filterFortnight.set(FortnightType.First);
      expect(c.activeFilterCount()).toBe(1);
    });

    it('activeFilterCount é 2 com ambos os filtros preenchidos', () => {
      fixture.detectChanges();
      c.filterDesc.set('algo');
      c.filterFortnight.set(FortnightType.First);
      expect(c.activeFilterCount()).toBe(2);
    });
  });

  // ── filterOpen — sinal para controle do modal de filtros ──────────────────

  describe('filterOpen — sinal para abrir/fechar modal de filtros', () => {
    it('existe sinal filterOpen no componente', () => {
      fixture.detectChanges();
      expect(c.filterOpen).withContext('filterOpen deve existir').toBeDefined();
    });

    it('filterOpen inicia como false', () => {
      fixture.detectChanges();
      expect(c.filterOpen()).toBeFalse();
    });
  });
});
