import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { RouterModule } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { of, throwError } from 'rxjs';
import { DashboardComponent } from './dashboard.component';
import { ApiService } from '../../../../core/services/api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { PeriodResponse, PeriodSummary, ExpensesReport } from '../../../../core/models/models';

const MOCK_PERIODS: PeriodResponse[] = [
  { id: 'p-1', userId: 'u', year: 2024, month: 3, isActive: true },
  { id: 'p-2', userId: 'u', year: 2024, month: 2, isActive: false },
];

const MOCK_SUMMARY: PeriodSummary = {
  periodId: 'p-1',
  userId:   'u',
  year:     2024,
  month:    3,
  totalIncome:          3000,
  totalExpense:         1200,
  totalPaid:            800,
  totalOwed:            400,
  balance:              1800,
  totalFirstFortnight:  600,
  totalSecondFortnight: 600,
};

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let component: DashboardComponent;
  let apiSpy: jasmine.SpyObj<ApiService>;
  let authSpy: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    apiSpy  = jasmine.createSpyObj('ApiService', ['getPeriods', 'getPeriodSummary', 'getExpensesReport']);
    authSpy = jasmine.createSpyObj('AuthService', [], { currentUser: () => ({ name: 'Caique', email: 'c@t.com' }) });

    const emptyReport: ExpensesReport = { year: 2024, month: null, items: [] };
    apiSpy.getPeriods.and.returnValue(of(MOCK_PERIODS));
    apiSpy.getPeriodSummary.and.returnValue(of(MOCK_SUMMARY));
    apiSpy.getExpensesReport.and.returnValue(of(emptyReport));

    await TestBed.configureTestingModule({
      imports: [DashboardComponent, RouterModule.forRoot([])],
      providers: [
        { provide: ApiService,  useValue: apiSpy },
        { provide: AuthService, useValue: authSpy },
      ],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
  });

  it('cria o componente', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnInit()', () => {
    it('carrega períodos e seleciona o primeiro', fakeAsync(() => {
      fixture.detectChanges();
      tick();
      expect(component.periods()).toEqual(MOCK_PERIODS);
      expect(component.selectedPeriodId()).toBe('p-1');
      expect(component.loadingPeriods()).toBeFalse();
    }));

    it('define loadingPeriods=false quando API retorna erro', fakeAsync(() => {
      apiSpy.getPeriods.and.returnValue(throwError(() => new Error('fail')));
      fixture.detectChanges();
      tick();
      expect(component.loadingPeriods()).toBeFalse();
    }));

    it('não seleciona período quando lista está vazia', fakeAsync(() => {
      apiSpy.getPeriods.and.returnValue(of([]));
      fixture.detectChanges();
      tick();
      expect(component.selectedPeriodId()).toBeNull();
    }));
  });

  describe('selectPeriod()', () => {
    beforeEach(fakeAsync(() => {
      fixture.detectChanges();
      tick();
    }));

    it('atualiza selectedPeriodId', fakeAsync(() => {
      component.selectPeriod('p-2');
      tick();
      expect(component.selectedPeriodId()).toBe('p-2');
    }));

    it('busca summary e atualiza signal', fakeAsync(() => {
      component.selectPeriod('p-1');
      tick();
      expect(component.summary()).toEqual(MOCK_SUMMARY);
      expect(component.loadingSummary()).toBeFalse();
    }));

    it('define loadingSummary=false quando summary falha', fakeAsync(() => {
      apiSpy.getPeriodSummary.and.returnValue(throwError(() => new Error('fail')));
      component.selectPeriod('p-1');
      tick();
      expect(component.loadingSummary()).toBeFalse();
    }));
  });

  describe('monthName()', () => {
    it('retorna abreviação de 3 letras', () => {
      expect(component.monthName(1)).toBe('Jan');
      expect(component.monthName(3)).toBe('Mar');
      expect(component.monthName(12)).toBe('Dez');
    });
  });

  describe('computed — headerSubtitle', () => {
    it('retorna "Selecione um período" sem período selecionado', fakeAsync(() => {
      apiSpy.getPeriods.and.returnValue(of([]));
      fixture.detectChanges();
      tick();
      expect(component.headerSubtitle()).toBe('Selecione um período');
    }));

    it('retorna nome do mês e ano quando período selecionado', fakeAsync(() => {
      fixture.detectChanges();
      tick();
      expect(component.headerSubtitle()).toContain('2024');
    }));
  });

  describe('computed — paymentProgress', () => {
    it('retorna 0 quando não há summary', () => {
      expect(component.paymentProgress()).toBe(0);
    });

    it('calcula percentual de pagamento', fakeAsync(() => {
      fixture.detectChanges();
      tick();
      // totalPaid=800, totalExpense=1200 → ~66.67%
      expect(component.paymentProgress()).toBeCloseTo(66.67, 0);
    }));

    it('retorna 0 quando totalExpense é 0', fakeAsync(() => {
      apiSpy.getPeriodSummary.and.returnValue(of({ ...MOCK_SUMMARY, totalExpense: 0 }));
      fixture.detectChanges();
      tick();
      expect(component.paymentProgress()).toBe(0);
    }));
  });
});
