import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { of, throwError } from 'rxjs';
import { BatchExpensesModalComponent } from './batch-expenses-modal.component';
import { ApiService } from '../../../../core/services/api.service';
import { CategoryResponse, FortnightType, SourceType } from '../../../../core/models/models';

const CATEGORY: CategoryResponse = {
  id: 'cat-1', userId: 'u', name: 'Moradia', color: '#abc', icon: null, isGlobal: false, isActive: true
};

const ITEM = {
  categoryId: 'cat-1', description: 'Aluguel', amount: 1000,
  dueDate: '2026-10-05', sourceType: SourceType.Personal,
  fortnightType: FortnightType.First, isRecurring: false
};

describe('BatchExpensesModalComponent', () => {
  let fixture: ComponentFixture<BatchExpensesModalComponent>;
  let component: BatchExpensesModalComponent;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('ApiService', ['createExpensesBatch']);

    await TestBed.configureTestingModule({
      imports: [BatchExpensesModalComponent],
      providers: [{ provide: ApiService, useValue: apiSpy }],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture = TestBed.createComponent(BatchExpensesModalComponent);
    component = fixture.componentInstance;

    fixture.componentRef.setInput('periodId', 'p-1');
    fixture.componentRef.setInput('categories', [CATEGORY]);

    fixture.detectChanges();
  });

  it('cria o componente com lista vazia e form definido', () => {
    expect(component).toBeTruthy();
    expect(component.items()).toEqual([]);
    expect(component.newForm).toBeDefined();
  });

  describe('addItem()', () => {
    it('adiciona item válido ao signal e limpa o form', () => {
      component.newForm.patchValue({
        categoryId: 'cat-1', description: 'Aluguel', amount: 1000,
        dueDate: '2026-10-05', sourceType: SourceType.Personal, fortnightType: FortnightType.First
      });
      component.addItem();
      expect(component.items().length).toBe(1);
      expect(component.items()[0].description).toBe('Aluguel');
      expect(component.newForm.get('description')?.value).toBe('');
    });

    it('não adiciona quando form inválido', () => {
      component.addItem();
      expect(component.items().length).toBe(0);
    });
  });

  describe('startEditItem()', () => {
    it('define editingIndex e popula editForm com valores do item', () => {
      component.items.set([{ ...ITEM }]);
      component.startEditItem(0);
      expect(component.editingIndex()).toBe(0);
      expect(component.editForm.get('description')?.value).toBe('Aluguel');
      expect(component.editForm.get('amount')?.value).toBe(1000);
    });
  });

  describe('removeItem()', () => {
    it('remove item do signal e rearranja lista', () => {
      component.items.set([
        { ...ITEM, description: 'A', amount: 100 },
        { ...ITEM, description: 'B', amount: 200 },
      ]);
      component.removeItem(0);
      expect(component.items().length).toBe(1);
      expect(component.items()[0].description).toBe('B');
    });

    it('ajusta editingIndex ao remover item antes do editado', () => {
      component.items.set([
        { ...ITEM, description: 'A' },
        { ...ITEM, description: 'B' },
      ]);
      component.startEditItem(1);
      component.removeItem(0);
      expect(component.editingIndex()).toBe(0);
    });
  });

  describe('canSave()', () => {
    it('retorna false sem itens', () => expect(component.canSave()).toBeFalse());
    it('retorna true com itens', () => {
      component.items.set([{ ...ITEM }]);
      expect(component.canSave()).toBeTrue();
    });
  });

  describe('save()', () => {
    it('chama createExpensesBatch com periodId e itens e emite saved', fakeAsync(() => {
      const savedSpy = jasmine.createSpy('saved');
      component.saved.subscribe(savedSpy);
      component.items.set([{ ...ITEM }]);
      apiSpy.createExpensesBatch.and.returnValue(of([]));

      component.save();
      tick();

      expect(apiSpy.createExpensesBatch).toHaveBeenCalledWith({
        periodId: 'p-1',
        items: [{ ...ITEM }]
      });
      expect(savedSpy).toHaveBeenCalled();
    }));

    it('abre MarioModal com mensagem de erro quando API falha', fakeAsync(() => {
      component.items.set([{ ...ITEM }]);
      apiSpy.createExpensesBatch.and.returnValue(
        throwError(() => ({ error: { message: 'Erro ao salvar lote.' } }))
      );

      component.save();
      tick();

      expect(component.marioOpen()).toBeTrue();
      expect(component.marioContent()).toBe('Erro ao salvar lote.');
    }));
  });
});
