import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { RouterModule } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { of, throwError } from 'rxjs';
import { ExpensesComponent } from './expenses.component';
import { ApiService } from '../../../../core/services/api.service';
import {
  ExpenseResponse, PeriodResponse, CategoryResponse,
  PaymentStatus, FortnightType, SourceType,
} from '../../../../core/models/models';

const PERIOD: PeriodResponse = { id: 'p-1', userId: 'u', year: 2024, month: 4, isActive: true };
const CATEGORY: CategoryResponse = { id: 'cat-1', userId: 'u', name: 'Moradia', color: '#abc', icon: null, isGlobal: false, isActive: true };
const EXPENSE: ExpenseResponse = {
  id: 'e-1', periodId: 'p-1', userId: 'u', categoryId: 'cat-1', description: 'Aluguel',
  amount: 1000, dueDate: '2024-04-05T00:00:00', paymentStatus: PaymentStatus.Pending,
  paymentDate: null, sourceType: SourceType.Personal, fortnightType: FortnightType.First,
  notes: null, isActive: true,
};

describe('ExpensesComponent', () => {
  let fixture: ComponentFixture<ExpensesComponent>;
  let component: ExpensesComponent;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('ApiService', [
      'getPeriods', 'getCategories', 'getExpensesByPeriod',
      'createExpense', 'updateExpense', 'markExpenseAsPaid', 'deleteExpense'
    ]);
    apiSpy.getPeriods.and.returnValue(of([PERIOD]));
    apiSpy.getCategories.and.returnValue(of([CATEGORY]));
    apiSpy.getExpensesByPeriod.and.returnValue(of({ items: [EXPENSE], totalCount: 1, pageNumber: 1, pageSize: 20 }));

    await TestBed.configureTestingModule({
      imports: [ExpensesComponent, RouterModule.forRoot([])],
      providers: [{ provide: ApiService, useValue: apiSpy }],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(ExpensesComponent);
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

    it('limpa apiError', () => {
      component.apiError.set('erro antigo');
      component.openCreateModal();
      expect(component.apiError()).toBeNull();
    });
  });

  describe('openEditModal()', () => {
    it('abre modal em modo edit com dados da despesa', () => {
      component.openEditModal(EXPENSE);
      expect(component.modalOpen()).toBeTrue();
      expect(component.modalMode()).toBe('edit');
      expect(component.form.get('description')!.value).toBe('Aluguel');
    });
  });

  describe('closeModal()', () => {
    it('fecha modal e limpa estado', () => {
      component.openCreateModal();
      component.closeModal();
      expect(component.modalOpen()).toBeFalse();
      expect(component.apiError()).toBeNull();
    });
  });

  describe('onSubmit() — create', () => {
    beforeEach(() => {
      component.openCreateModal();
      component.form.patchValue({
        periodId: 'p-1', categoryId: 'cat-1', description: 'Teste',
        amount: 100, dueDate: '2024-04-01',
      });
    });

    it('não submete quando form inválido', () => {
      component.form.get('description')!.setValue('');
      component.onSubmit();
      expect(apiSpy.createExpense).not.toHaveBeenCalled();
    });

    it('cria despesa e fecha modal', fakeAsync(() => {
      apiSpy.createExpense.and.returnValue(of({ ...EXPENSE, id: 'e-new' }));
      component.onSubmit();
      tick();
      expect(component.modalOpen()).toBeFalse();
      expect(component.saving()).toBeFalse();
    }));

    it('define apiError quando createExpense falha', fakeAsync(() => {
      apiSpy.createExpense.and.returnValue(
        throwError(() => ({ error: { message: 'Erro ao salvar.' } }))
      );
      component.onSubmit();
      tick();
      expect(component.apiError()).toBe('Erro ao salvar.');
    }));
  });

  describe('onSubmit() — edit', () => {
    beforeEach(() => {
      component.openEditModal(EXPENSE);
    });

    it('atualiza despesa na lista e fecha modal', fakeAsync(() => {
      component.expenses.set([EXPENSE]);
      apiSpy.updateExpense.and.returnValue(of(undefined));

      component.form.patchValue({ description: 'Aluguel Novo', amount: 1100, dueDate: '2024-04-05' });
      component.onSubmit();
      tick();

      const updated = component.expenses().find(e => e.id === 'e-1');
      expect(updated?.description).toBe('Aluguel Novo');
      expect(component.modalOpen()).toBeFalse();
    }));

    it('define apiError quando updateExpense falha', fakeAsync(() => {
      apiSpy.updateExpense.and.returnValue(
        throwError(() => ({ error: { message: 'Erro ao editar.' } }))
      );
      component.onSubmit();
      tick();
      expect(component.apiError()).toBe('Erro ao editar.');
    }));
  });

  describe('markAsPaid()', () => {
    it('atualiza paymentStatus para Paid na lista', fakeAsync(() => {
      component.expenses.set([EXPENSE]);
      apiSpy.markExpenseAsPaid.and.returnValue(of(undefined));

      component.markAsPaid(EXPENSE);
      tick();

      const updated = component.expenses().find(e => e.id === 'e-1');
      expect(updated?.paymentStatus).toBe(PaymentStatus.Paid);
    }));
  });

  describe('deleteExpense()', () => {
    beforeEach(() => {
      spyOn(window, 'confirm').and.returnValue(true);
      apiSpy.deleteExpense.and.returnValue(of(undefined));
      component.expenses.set([EXPENSE]);
    });

    it('remove despesa da lista', fakeAsync(() => {
      component.deleteExpense('e-1');
      tick();
      expect(component.expenses().find(e => e.id === 'e-1')).toBeUndefined();
    }));

    it('não deleta quando usuário cancela', () => {
      (window.confirm as jasmine.Spy).and.returnValue(false);
      component.deleteExpense('e-1');
      expect(apiSpy.deleteExpense).not.toHaveBeenCalled();
    });
  });

  describe('helpers', () => {
    beforeEach(fakeAsync(() => { tick(); }));

    it('categoryName retorna nome da categoria', () => {
      expect(component.categoryName('cat-1')).toBe('Moradia');
    });

    it('categoryName retorna "—" para id desconhecido', () => {
      expect(component.categoryName('unknown')).toBe('—');
    });

    it('categoryColor retorna cor da categoria', () => {
      expect(component.categoryColor('cat-1')).toBe('#abc');
    });

    it('statusBadgeClass retorna classes corretas', () => {
      expect(component.statusBadgeClass(PaymentStatus.Paid)).toBe('badge-success');
      expect(component.statusBadgeClass(PaymentStatus.Pending)).toBe('badge-warning');
      expect(component.statusBadgeClass(PaymentStatus.Cancelled)).toBe('badge-neutral');
      expect(component.statusBadgeClass(PaymentStatus.Partial)).toBe('badge-info');
    });

    it('formatDate retorna data formatada', () => {
      const result = component.formatDate('2024-04-05T00:00:00');
      expect(result).toContain('2024');
    });
  });
});
