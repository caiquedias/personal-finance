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
