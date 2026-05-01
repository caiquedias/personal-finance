import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { RouterModule } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { of, throwError } from 'rxjs';
import { IncomesComponent } from './incomes.component';
import { ApiService } from '../../../../core/services/api.service';
import { IncomeResponse, PeriodResponse, FortnightType } from '../../../../core/models/models';

const PERIOD: PeriodResponse = { id: 'p-1', userId: 'u', year: 2024, month: 4, isActive: true };

const INCOME: IncomeResponse = {
  id:            'i-1',
  periodId:      'p-1',
  userId:        'u',
  fortnightType: FortnightType.First,
  description:   'Salário',
  amount:        3000,
  receivedAt:    '2024-04-05T00:00:00',
  notes:         null,
  isActive:      true,
};


describe('IncomesComponent', () => {
  let fixture: ComponentFixture<IncomesComponent>;
  let component: IncomesComponent;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('ApiService', [
      'getPeriods', 'getIncomesByPeriod', 'createIncome', 'deleteIncome'
    ]);
    apiSpy.getPeriods.and.returnValue(of([PERIOD]));
    apiSpy.getIncomesByPeriod.and.returnValue(of({ items: [INCOME], totalCount: 1, pageNumber: 1, pageSize: 20 }));

    await TestBed.configureTestingModule({
      imports: [IncomesComponent, RouterModule.forRoot([])],
      providers: [{ provide: ApiService, useValue: apiSpy }],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(IncomesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('cria o componente', () => {
    expect(component).toBeTruthy();
  });

  describe('openCreateModal()', () => {
    it('abre modal em modo create', () => {
      component.openCreateModal();
      expect(component.modalOpen()).toBeTrue();
      expect(component.modalMode()).toBe('create');
    });
  });

  describe('openEditModal()', () => {
    it('abre modal em modo edit com dados da receita', () => {
      component.openEditModal(INCOME);
      expect(component.modalOpen()).toBeTrue();
      expect(component.modalMode()).toBe('edit');
      expect(component.form.get('description')!.value).toBe('Salário');
      expect(component.form.get('amount')!.value).toBe(3000);
    });
  });

  describe('closeModal()', () => {
    it('fecha o modal e limpa o erro', () => {
      component.openCreateModal();
      component.apiError.set('erro');
      component.closeModal();
      expect(component.modalOpen()).toBeFalse();
      expect(component.apiError()).toBeNull();
    });
  });

  describe('onSubmit() — create', () => {
    beforeEach(() => {
      component.openCreateModal();
      component.form.patchValue({
        periodId: 'p-1', description: 'Salário', amount: 3000, receivedAt: '2024-04-05'
      });
    });

    it('não submete quando form inválido', () => {
      component.form.get('description')!.setValue('');
      component.onSubmit();
      expect(apiSpy.createIncome).not.toHaveBeenCalled();
    });

    it('cria receita e fecha modal quando período já selecionado', fakeAsync(() => {
      // Simula período selecionado
      (component as any).selectedPeriodId = 'p-1';
      apiSpy.createIncome.and.returnValue(of(INCOME));

      component.onSubmit();
      tick();

      expect(component.modalOpen()).toBeFalse();
      expect(component.saving()).toBeFalse();
    }));

    it('define apiError quando createIncome falha', fakeAsync(() => {
      apiSpy.createIncome.and.returnValue(
        throwError(() => ({ error: { message: 'Erro ao criar receita.' } }))
      );
      component.onSubmit();
      tick();
      expect(component.apiError()).toBe('Erro ao criar receita.');
    }));
  });

  describe('onSubmit() — edit (delete + create)', () => {
    beforeEach(() => {
      component.incomes.set([INCOME]);
      component.openEditModal(INCOME);
      component.form.patchValue({
        periodId: 'p-1', description: 'Salário Atualizado', amount: 3500, receivedAt: '2024-04-05'
      });
    });

    it('faz delete e create, fecha modal e recarrega a lista', fakeAsync(() => {
      const updated: IncomeResponse = { ...INCOME, description: 'Salário Atualizado', amount: 3500 };
      (component as any).selectedPeriodId = 'p-1';
      apiSpy.deleteIncome.and.returnValue(of(undefined));
      apiSpy.createIncome.and.returnValue(of(updated));
      apiSpy.getIncomesByPeriod.and.returnValue(of({ items: [updated], totalCount: 1, pageNumber: 1, pageSize: 20 }));

      component.onSubmit();
      tick();

      const found = component.incomes().find(i => i.id === 'i-1');
      expect(found?.description).toBe('Salário Atualizado');
      expect(component.modalOpen()).toBeFalse();
    }));

    it('define apiError quando deleteIncome falha', fakeAsync(() => {
      apiSpy.deleteIncome.and.returnValue(
        throwError(() => ({ error: { message: 'Erro ao atualizar receita.' } }))
      );
      component.onSubmit();
      tick();
      expect(component.apiError()).toBe('Erro ao atualizar receita.');
    }));
  });

  describe('deleteIncome()', () => {
    beforeEach(() => {
      spyOn(window, 'confirm').and.returnValue(true);
      apiSpy.deleteIncome.and.returnValue(of(undefined));
      component.incomes.set([INCOME]);
    });

    it('remove receita da lista', fakeAsync(() => {
      (component as any).selectedPeriodId = 'p-1';
      apiSpy.getIncomesByPeriod.and.returnValue(of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 }));
      component.deleteIncome('i-1');
      tick();
      expect(component.incomes().find(i => i.id === 'i-1')).toBeUndefined();
    }));

    it('não deleta quando usuário cancela', () => {
      (window.confirm as jasmine.Spy).and.returnValue(false);
      component.deleteIncome('i-1');
      expect(apiSpy.deleteIncome).not.toHaveBeenCalled();
    });
  });

  describe('filtros — onApplyFilters()', () => {
    beforeEach(() => { (component as any).selectedPeriodId = 'p-1'; });

    it('aplica description e dispara loadPage', fakeAsync(() => {
      component.onApplyFilters({ description: 'salário', fortnightType: '' });
      tick();
      expect(component.filterDescription()).toBe('salário');
      expect(apiSpy.getIncomesByPeriod).toHaveBeenCalled();
    }));

    it('aplica fortnightType numérico', fakeAsync(() => {
      component.onApplyFilters({ description: '', fortnightType: FortnightType.Second });
      tick();
      expect(component.filterFortnightType()).toBe(FortnightType.Second);
    }));

    it('converte string vazia em null para fortnightType', fakeAsync(() => {
      component.onApplyFilters({ description: '', fortnightType: '' });
      tick();
      expect(component.filterFortnightType()).toBeNull();
    }));

    it('reseta currentPage para 1', fakeAsync(() => {
      component.currentPage.set(5);
      component.onApplyFilters({ description: '', fortnightType: '' });
      tick();
      expect(component.currentPage()).toBe(1);
    }));
  });

  describe('filtros — onClearFilters()', () => {
    beforeEach(() => { (component as any).selectedPeriodId = 'p-1'; });

    it('limpa todos os filtros e dispara loadPage', fakeAsync(() => {
      component.filterDescription.set('salário');
      component.filterFortnightType.set(FortnightType.First);
      component.onClearFilters();
      tick();
      expect(component.filterDescription()).toBe('');
      expect(component.filterFortnightType()).toBeNull();
      expect(apiSpy.getIncomesByPeriod).toHaveBeenCalled();
    }));
  });

  describe('activeFilterCount', () => {
    it('retorna 0 quando nenhum filtro ativo', () => {
      expect(component.activeFilterCount()).toBe(0);
    });

    it('conta description e fortnightType quando ativos', () => {
      component.filterDescription.set('s');
      component.filterFortnightType.set(FortnightType.First);
      expect(component.activeFilterCount()).toBe(2);
    });
  });

  describe('formatDate()', () => {
    it('retorna data formatada em pt-BR', () => {
      const result = component.formatDate('2024-04-05T00:00:00');
      expect(result).toContain('2024');
    });
  });
});
