import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { RouterModule } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { of, throwError } from 'rxjs';
import { PeriodDetailComponent } from './period-detail.component';
import { ApiService } from '../../../../core/services/api.service';
import {
  PeriodSummary, ExpenseResponse, IncomeResponse, CategoryResponse,
  PaymentStatus, FortnightType, SourceType,
} from '../../../../core/models/models';

const SUMMARY: PeriodSummary = {
  periodId:             'p-1',
  userId:               'u',
  year:                 2024,
  month:                4,
  totalIncome:          3000,
  totalExpense:         1000,
  totalPaid:            600,
  totalOwed:            400,
  balance:              2000,
  totalFirstFortnight:  500,
  totalSecondFortnight: 500,
};

const EXPENSE: ExpenseResponse = {
  id:             'e-1',
  periodId:       'p-1',
  userId:         'u',
  categoryId:     'cat-1',
  description:    'Aluguel',
  amount:         1000,
  dueDate:        '2024-04-05T00:00:00',
  paymentStatus:  PaymentStatus.Paid,
  paymentDate:    '2024-04-05T00:00:00',
  sourceType:     SourceType.Personal,
  fortnightType:  FortnightType.First,
  notes:          null,
  isActive:       true,
  isRecurring:    false,
};

const INCOME: IncomeResponse = {
  id:             'i-1',
  periodId:       'p-1',
  userId:         'u',
  fortnightType:  FortnightType.First,
  description:    'Salário',
  amount:         3000,
  receivedAt:     '2024-04-05T00:00:00',
  notes:          null,
  isActive:       true,
};

const CATEGORY: CategoryResponse = {
  id:       'cat-1',
  userId:   'u',
  name:     'Moradia',
  color:    '#aabbcc',
  icon:     null,
  isGlobal: false,
  isActive: true,
};

describe('PeriodDetailComponent', () => {
  let fixture: ComponentFixture<PeriodDetailComponent>;
  let component: PeriodDetailComponent;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('ApiService', [
      'getPeriodSummary', 'getExpensesByPeriod', 'getIncomesByPeriod', 'getCategories'
    ]);
    apiSpy.getPeriodSummary.and.returnValue(of(SUMMARY));
    apiSpy.getExpensesByPeriod.and.returnValue(of({ items: [EXPENSE], totalCount: 1, pageNumber: 1, pageSize: 20 }));
    apiSpy.getIncomesByPeriod.and.returnValue(of({ items: [INCOME], totalCount: 1, pageNumber: 1, pageSize: 20 }));
    apiSpy.getCategories.and.returnValue(of([CATEGORY]));

    await TestBed.configureTestingModule({
      imports: [PeriodDetailComponent, RouterModule.forRoot([])],
      providers: [{ provide: ApiService, useValue: apiSpy }],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(PeriodDetailComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('id', 'p-1');
  });

  it('cria o componente', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnInit() — Promise.all', () => {
    it('carrega summary, expenses, incomes e categories', fakeAsync(() => {
      fixture.detectChanges();
      tick();
      expect(component.summary()).toEqual(SUMMARY);
      expect(component.expenses()).toEqual([EXPENSE]);
      expect(component.incomes()).toEqual([INCOME]);
      expect(component.categories()).toEqual([CATEGORY]);
      expect(component.loading()).toBeFalse();
    }));

    it('define loading=false quando Promise.all falha', fakeAsync(() => {
      apiSpy.getPeriodSummary.and.returnValue(throwError(() => new Error()));
      fixture.detectChanges();
      tick();
      expect(component.loading()).toBeFalse();
    }));
  });

  describe('headerTitle computed', () => {
    it('retorna "Período" sem summary', () => {
      expect(component.headerTitle()).toBe('Período');
    });

    it('retorna mês e ano com summary carregado', fakeAsync(() => {
      fixture.detectChanges();
      tick();
      expect(component.headerTitle()).toContain('2024');
    }));
  });

  describe('monthAbbr computed', () => {
    it('retorna string vazia sem summary', () => {
      expect(component.monthAbbr()).toBe('');
    });

    it('retorna "Abr" para mês 4', fakeAsync(() => {
      fixture.detectChanges();
      tick();
      expect(component.monthAbbr()).toBe('Abr');
    }));

    it('retorna "Dez" para mês 12', fakeAsync(() => {
      apiSpy.getPeriodSummary.and.returnValue(of({ ...SUMMARY, month: 12 }));
      fixture.detectChanges();
      tick();
      expect(component.monthAbbr()).toBe('Dez');
    }));

    it('retorna "Jan" para mês 1', fakeAsync(() => {
      apiSpy.getPeriodSummary.and.returnValue(of({ ...SUMMARY, month: 1 }));
      fixture.detectChanges();
      tick();
      expect(component.monthAbbr()).toBe('Jan');
    }));
  });

  describe('year computed', () => {
    it('retorna null sem summary', () => {
      expect(component.year()).toBeNull();
    });

    it('retorna o ano do summary', fakeAsync(() => {
      fixture.detectChanges();
      tick();
      expect(component.year()).toBe(2024);
    }));
  });

  describe('openMario()', () => {
    beforeEach(fakeAsync(() => {
      fixture.detectChanges();
      tick();
    }));

    it('target "receitas" — define titulo e conteúdo', () => {
      component.openMario('receitas');
      expect(component.marioTitle()).toBe('RECEITAS DO PERIODO');
      expect(component.marioContent()).toContain('Salário');
      expect(component.marioOpen()).toBeTrue();
    });

    it('target "receitas" — lista vazia', () => {
      apiSpy.getIncomesByPeriod.and.returnValue(of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 1000 }));
      component.openMario('receitas');
      expect(component.marioContent()).toContain('Nenhuma receita');
    });

    it('target "despesas" — define titulo e conteúdo', fakeAsync(() => {
      apiSpy.getExpensesByPeriod.and.returnValue(of({ items: [EXPENSE], totalCount: 1, pageNumber: 1, pageSize: 1000 }));
      component.openMario('despesas');
      tick();
      expect(component.marioTitle()).toBe('DESPESAS DO PERIODO');
      expect(component.marioContent()).toContain('Aluguel');
      expect(component.marioOpen()).toBeTrue();
    }));

    it('target "despesas" — lista vazia', fakeAsync(() => {
      apiSpy.getExpensesByPeriod.and.returnValue(of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 1000 }));
      component.openMario('despesas');
      tick();
      expect(component.marioContent()).toContain('Nenhuma despesa');
    }));

    it('target "pago" — lista despesas pagas', fakeAsync(() => {
      apiSpy.getExpensesByPeriod.and.returnValue(of({ items: [EXPENSE], totalCount: 1, pageNumber: 1, pageSize: 1000 }));
      component.openMario('pago');
      tick();
      expect(component.marioTitle()).toBe('DESPESAS PAGAS');
      expect(component.marioContent()).toContain('Aluguel');
      expect(component.marioOpen()).toBeTrue();
    }));

    it('target "pago" — sem despesas pagas', fakeAsync(() => {
      apiSpy.getExpensesByPeriod.and.returnValue(of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 1000 }));
      component.openMario('pago');
      tick();
      expect(component.marioContent()).toContain('Nenhuma despesa');
    }));

    it('target "apagar" — sem pendentes (tudo pago)', () => {
      apiSpy.getExpensesByPeriod.and.returnValue(of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 1000 }));
      component.openMario('apagar');
      expect(component.marioContent()).toContain('Nenhuma despesa');
    });

    it('target "apagar" — com despesa parcial mostra sufixo', () => {
      apiSpy.getExpensesByPeriod.and.returnValue(of({ items: [{ ...EXPENSE, paymentStatus: PaymentStatus.Partial }], totalCount: 1, pageNumber: 1, pageSize: 1000 }));
      component.openMario('apagar');
      expect(component.marioContent()).toContain('parcial');
    });

    it('target "saldo" — calcula receitas e despesas', () => {
      component.openMario('saldo');
      expect(component.marioTitle()).toBe('CALCULO DO SALDO');
      expect(component.marioContent()).toContain('Receitas');
      expect(component.marioContent()).toContain('Despesas');
      expect(component.marioContent()).toContain('Saldo');
    });

    it('target "saldoAposPagamento" — exibe titulo e calculo correto', () => {
      component.openMario('saldoAposPagamento');
      expect(component.marioTitle()).toBe('SALDO APOS PAGAMENTO');
      expect(component.marioContent()).toContain('Receitas');
      expect(component.marioContent()).toContain('Total de despesas');
      expect(component.marioContent()).toContain('Saldo apos pagamento');
      expect(component.marioOpen()).toBeTrue();
    });
  });

  describe('KPI computeds — sem filtros ativos', () => {
    beforeEach(fakeAsync(() => { fixture.detectChanges(); tick(); }));

    it('kpiIncomeTotal retorna summary.totalIncome', () => {
      expect(component.kpiIncomeTotal()).toBe(SUMMARY.totalIncome);
    });

    it('kpiExpenseTotal retorna summary.totalExpense', () => {
      expect(component.kpiExpenseTotal()).toBe(SUMMARY.totalExpense);
    });

    it('kpiPaid retorna summary.totalPaid', () => {
      expect(component.kpiPaid()).toBe(SUMMARY.totalPaid);
    });

    it('kpiOwed retorna summary.totalOwed', () => {
      expect(component.kpiOwed()).toBe(SUMMARY.totalOwed);
    });

    it('kpiBalance retorna summary.balance', () => {
      expect(component.kpiBalance()).toBe(SUMMARY.balance);
    });

    it('kpiBalanceAfterPayment retorna totalIncome - totalExpense', () => {
      expect(component.kpiBalanceAfterPayment()).toBe(SUMMARY.totalIncome - SUMMARY.totalExpense);
    });

  });

  describe('kpiBalanceAfterPayment — summary negativo', () => {
    it('retorna valor negativo quando despesas superam receitas', fakeAsync(() => {
      apiSpy.getPeriodSummary.and.returnValue(of({ ...SUMMARY, totalIncome: 500, totalExpense: 1500 }));
      fixture.detectChanges();
      tick();
      expect(component.kpiBalanceAfterPayment()).toBe(-1000);
    }));
  });

  describe('KPI computeds — com filtros de despesas ativos', () => {
    beforeEach(fakeAsync(() => { fixture.detectChanges(); tick(); }));

    it('kpiExpenseTotal soma allFilteredExpenses', () => {
      component.expDesc.set('x');
      component.allFilteredExpenses.set([
        { ...EXPENSE, amount: 300 },
        { ...EXPENSE, amount: 200, paymentStatus: PaymentStatus.Pending },
      ]);
      expect(component.kpiExpenseTotal()).toBe(500);
    });

    it('kpiPaid soma somente despesas Paid de allFilteredExpenses', () => {
      component.expDesc.set('x');
      component.allFilteredExpenses.set([
        { ...EXPENSE, amount: 300, paymentStatus: PaymentStatus.Paid },
        { ...EXPENSE, amount: 200, paymentStatus: PaymentStatus.Pending },
      ]);
      expect(component.kpiPaid()).toBe(300);
    });

    it('kpiOwed soma Pending e Partial de allFilteredExpenses', () => {
      component.expDesc.set('x');
      component.allFilteredExpenses.set([
        { ...EXPENSE, amount: 300, paymentStatus: PaymentStatus.Paid },
        { ...EXPENSE, amount: 200, paymentStatus: PaymentStatus.Pending },
        { ...EXPENSE, amount: 100, paymentStatus: PaymentStatus.Partial },
      ]);
      expect(component.kpiOwed()).toBe(300);
    });

    it('kpiBalance deriva de kpiIncomeTotal - kpiExpenseTotal com filtros ativos', () => {
      component.expDesc.set('x');
      component.allFilteredExpenses.set([{ ...EXPENSE, amount: 400 }]);
      expect(component.kpiBalance()).toBe(SUMMARY.totalIncome - 400);
    });
  });

  describe('KPI computeds — com filtros de receitas ativos', () => {
    beforeEach(fakeAsync(() => { fixture.detectChanges(); tick(); }));

    it('kpiIncomeTotal soma allFilteredIncomes', () => {
      component.incDesc.set('sal');
      component.allFilteredIncomes.set([
        { ...INCOME, amount: 1500 },
        { ...INCOME, amount: 500 },
      ]);
      expect(component.kpiIncomeTotal()).toBe(2000);
    });

    it('kpiBalance deriva de kpiIncomeTotal - kpiExpenseTotal com filtros ativos', () => {
      component.incDesc.set('sal');
      component.allFilteredIncomes.set([{ ...INCOME, amount: 2000 }]);
      expect(component.kpiBalance()).toBe(2000 - SUMMARY.totalExpense);
    });
  });

  describe('expFilterFields computed', () => {
    beforeEach(fakeAsync(() => { fixture.detectChanges(); tick(); }));

    it('retorna 5 campos', () => {
      expect(component.expFilterFields().length).toBe(5);
    });

    it('campo description reflete expDesc', () => {
      component.expDesc.set('teste');
      const field = component.expFilterFields().find(f => f.key === 'description')!;
      expect(field.value).toBe('teste');
    });

    it('campo categoryId inclui categorias carregadas', () => {
      const field = component.expFilterFields().find(f => f.key === 'categoryId')!;
      expect(field.options?.some(o => o.label === 'Moradia')).toBeTrue();
    });
  });

  describe('incFilterFields computed', () => {
    it('retorna 2 campos', () => {
      expect(component.incFilterFields().length).toBe(2);
    });

    it('campo description reflete incDesc', () => {
      component.incDesc.set('sal');
      const field = component.incFilterFields().find(f => f.key === 'description')!;
      expect(field.value).toBe('sal');
    });
  });

  describe('onExpFilterApply()', () => {
    beforeEach(fakeAsync(() => { fixture.detectChanges(); tick(); }));

    it('aplica filtros, fecha painel e recarrega despesas', fakeAsync(() => {
      component.expFilterOpen.set(true);
      component.onExpFilterApply({ description: 'aluguel', categoryId: 'cat-1', paymentStatus: '1', fortnightType: '1', sourceType: '1' });
      tick();
      expect(component.expDesc()).toBe('aluguel');
      expect(component.expCategoryId()).toBe('cat-1');
      expect(component.expStatus()).toBe(1);
      expect(component.expFortnight()).toBe(1);
      expect(component.expSourceType()).toBe(1);
      expect(component.expFilterOpen()).toBeFalse();
      expect(apiSpy.getExpensesByPeriod).toHaveBeenCalled();
    }));

    it('valores vazios limpam filtros de enum', fakeAsync(() => {
      component.expStatus.set(1 as any);
      component.onExpFilterApply({ description: '', categoryId: '', paymentStatus: '', fortnightType: '', sourceType: '' });
      tick();
      expect(component.expStatus()).toBeNull();
      expect(component.expFortnight()).toBeNull();
      expect(component.expSourceType()).toBeNull();
    }));
  });

  describe('onExpFilterClear()', () => {
    beforeEach(fakeAsync(() => { fixture.detectChanges(); tick(); }));

    it('limpa filtros e fecha painel', fakeAsync(() => {
      component.expDesc.set('x');
      component.expFilterOpen.set(true);
      component.onExpFilterClear();
      tick();
      expect(component.expDesc()).toBe('');
      expect(component.expFilterOpen()).toBeFalse();
    }));
  });

  describe('onIncFilterApply()', () => {
    beforeEach(fakeAsync(() => { fixture.detectChanges(); tick(); }));

    it('aplica filtros e recarrega receitas', fakeAsync(() => {
      component.onIncFilterApply({ description: 'sal', fortnightType: '2' });
      tick();
      expect(component.incDesc()).toBe('sal');
      expect(component.incFortnight()).toBe(2);
      expect(component.incFilterOpen()).toBeFalse();
      expect(apiSpy.getIncomesByPeriod).toHaveBeenCalled();
    }));
  });

  describe('onIncFilterClear()', () => {
    beforeEach(fakeAsync(() => { fixture.detectChanges(); tick(); }));

    it('limpa filtros e fecha painel', fakeAsync(() => {
      component.incDesc.set('x');
      component.incFilterOpen.set(true);
      component.onIncFilterClear();
      tick();
      expect(component.incDesc()).toBe('');
      expect(component.incFilterOpen()).toBeFalse();
    }));
  });

  describe('helpers', () => {
    beforeEach(fakeAsync(() => { fixture.detectChanges(); tick(); }));

    it('categoryName retorna nome da categoria', () => {
      expect(component.categoryName('cat-1')).toBe('Moradia');
    });

    it('categoryName retorna "—" para id inexistente', () => {
      expect(component.categoryName('unknown')).toBe('—');
    });

    it('categoryColor retorna cor da categoria', () => {
      expect(component.categoryColor('cat-1')).toBe('#aabbcc');
    });

    it('categoryColor retorna cor padrão para id inexistente', () => {
      expect(component.categoryColor('unknown')).toBe('#c8bfaf');
    });

    it('statusBadgeClass retorna classe correta', () => {
      expect(component.statusBadgeClass(PaymentStatus.Paid)).toBe('badge-success');
      expect(component.statusBadgeClass(PaymentStatus.Pending)).toBe('badge-warning');
      expect(component.statusBadgeClass(PaymentStatus.Cancelled)).toBe('badge-neutral');
      expect(component.statusBadgeClass(PaymentStatus.Partial)).toBe('badge-info');
    });

    it('formatDate retorna data no formato pt-BR', () => {
      const result = component.formatDate('2024-04-05T00:00:00');
      expect(result).toContain('2024');
    });
  });
});
