import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { RouterModule } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { of, throwError } from 'rxjs';
import { PeriodsComponent } from './periods.component';
import { ApiService } from '../../../core/services/api.service';
import { PeriodResponse } from '../../../core/models/models';

const MOCK_PERIODS: PeriodResponse[] = [
  { id: 'p-1', userId: 'u', year: 2024, month: 3,  isActive: true  },
  { id: 'p-2', userId: 'u', year: 2024, month: 1,  isActive: false },
  { id: 'p-3', userId: 'u', year: 2023, month: 12, isActive: true  },
];

describe('PeriodsComponent', () => {
  let fixture: ComponentFixture<PeriodsComponent>;
  let component: PeriodsComponent;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('ApiService', [
      'getPeriods', 'createPeriod', 'togglePeriodActive', 'deletePeriod'
    ]);
    apiSpy.getPeriods.and.returnValue(of(MOCK_PERIODS));

    await TestBed.configureTestingModule({
      imports: [PeriodsComponent, RouterModule.forRoot([])],
      providers: [{ provide: ApiService, useValue: apiSpy }],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(PeriodsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('cria o componente', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnInit / loadPeriods()', () => {
    it('carrega períodos na inicialização', fakeAsync(() => {
      tick();
      expect(component.periods()).toEqual(MOCK_PERIODS);
      expect(component.loadingList()).toBeFalse();
    }));

    it('define loadingList=false quando API falha', fakeAsync(() => {
      apiSpy.getPeriods.and.returnValue(throwError(() => new Error()));
      component.loadPeriods();
      tick();
      expect(component.loadingList()).toBeFalse();
    }));
  });

  describe('filtros — filteredPeriods', () => {
    beforeEach(fakeAsync(() => { tick(); }));

    it('filtra por ano', () => {
      component.setYear('2024');
      expect(component.filteredPeriods().every(p => p.year === 2024)).toBeTrue();
    });

    it('filtra por status ativo', () => {
      component.selectedYear.set(null);
      component.setStatus('active');
      expect(component.filteredPeriods().every(p => p.isActive)).toBeTrue();
    });

    it('filtra por status inativo', () => {
      component.selectedYear.set(null);
      component.setStatus('inactive');
      expect(component.filteredPeriods().every(p => !p.isActive)).toBeTrue();
    });

    it('filtra por mês (from / to)', () => {
      component.selectedYear.set(null);
      component.setMonthFrom('2');
      component.setMonthTo('3');
      expect(component.filteredPeriods().every(p => p.month >= 2 && p.month <= 3)).toBeTrue();
    });

    it('clearFilters() reseta todos os filtros', () => {
      component.setYear('2023');
      component.setStatus('active');
      component.clearFilters();
      expect(component.filterStatus()).toBe('all');
      expect(component.currentPage()).toBe(1);
    });

    it('setYear reset a página para 1', () => {
      component.currentPage.set(3);
      component.setYear('2024');
      expect(component.currentPage()).toBe(1);
    });
  });

  describe('paginação', () => {
    it('totalPages é 1 quando há menos períodos que pageSize', fakeAsync(() => {
      tick();
      component.selectedYear.set(null);
      expect(component.totalPages()).toBe(1);
    }));

    it('pageNumbers retorna array de 1 a totalPages', fakeAsync(() => {
      tick();
      component.selectedYear.set(null);
      const pages = component.pageNumbers();
      expect(pages).toEqual([1]);
    }));
  });

  describe('onSubmit()', () => {
    beforeEach(fakeAsync(() => { tick(); }));

    it('não submete quando form inválido', () => {
      component.form.get('year')!.setValue(null);
      component.onSubmit();
      expect(apiSpy.createPeriod).not.toHaveBeenCalled();
    });

    it('cria período e adiciona à lista', fakeAsync(() => {
      const newPeriod: PeriodResponse = { id: 'p-new', userId: 'u', year: 2025, month: 1, isActive: true };
      apiSpy.createPeriod.and.returnValue(of(newPeriod));

      component.form.setValue({ year: 2025, month: 1 });
      component.onSubmit();
      tick();

      expect(component.periods()[0]).toEqual(newPeriod);
      expect(component.showForm()).toBeFalse();
    }));

    it('define apiError quando createPeriod falha', fakeAsync(() => {
      apiSpy.createPeriod.and.returnValue(
        throwError(() => ({ error: { message: 'Período duplicado.' } }))
      );
      component.form.setValue({ year: 2025, month: 1 });
      component.onSubmit();
      tick();
      expect(component.apiError()).toBe('Período duplicado.');
    }));
  });

  describe('cancelForm()', () => {
    it('fecha o form e limpa o erro', () => {
      component.showForm.set(true);
      component.apiError.set('erro');
      component.cancelForm();
      expect(component.showForm()).toBeFalse();
      expect(component.apiError()).toBeNull();
    });
  });

  describe('toggleActive()', () => {
    beforeEach(fakeAsync(() => { tick(); }));

    it('inverte isActive do período na lista', fakeAsync(() => {
      apiSpy.togglePeriodActive.and.returnValue(of(undefined));
      const period = component.periods()[0]; // p-1, isActive=true
      component.toggleActive(period);
      tick();
      expect(component.periods().find(p => p.id === 'p-1')!.isActive).toBeFalse();
    }));

    it('define actionError quando falha', fakeAsync(() => {
      apiSpy.togglePeriodActive.and.returnValue(
        throwError(() => ({ error: { message: 'Erro toggle.' } }))
      );
      component.toggleActive(component.periods()[0]);
      tick();
      expect(component.actionError()).toBe('Erro toggle.');
    }));
  });

  describe('deletePeriod()', () => {
    beforeEach(fakeAsync(() => {
      tick();
      spyOn(window, 'confirm').and.returnValue(true);
      apiSpy.deletePeriod.and.returnValue(of(undefined));
    }));

    it('remove período da lista após confirmação', fakeAsync(() => {
      const period = component.periods()[0];
      component.deletePeriod(period);
      tick();
      expect(component.periods().find(p => p.id === 'p-1')).toBeUndefined();
    }));

    it('não deleta quando confirmação é cancelada', () => {
      (window.confirm as jasmine.Spy).and.returnValue(false);
      component.deletePeriod(component.periods()[0]);
      expect(apiSpy.deletePeriod).not.toHaveBeenCalled();
    });
  });

  describe('monthName()', () => {
    it('retorna nome completo do mês', () => {
      expect(component.monthName(1)).toBe('Janeiro');
      expect(component.monthName(12)).toBe('Dezembro');
    });
  });
});
