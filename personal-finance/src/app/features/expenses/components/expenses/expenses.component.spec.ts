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
  ExpenseSortColumn, SortDirection,
} from '../../../../core/models/models';

const PERIOD: PeriodResponse = { id: 'p-1', userId: 'u', year: 2024, month: 4, isActive: true };
const CATEGORY: CategoryResponse = { id: 'cat-1', userId: 'u', name: 'Moradia', color: '#abc', icon: null, isGlobal: false, isActive: true };
const EXPENSE: ExpenseResponse = {
  id: 'e-1', periodId: 'p-1', userId: 'u', categoryId: 'cat-1', description: 'Aluguel',
  amount: 1000, dueDate: '2024-04-05T00:00:00', paymentStatus: PaymentStatus.Pending,
  paymentDate: null, sourceType: SourceType.Personal, fortnightType: FortnightType.First,
  notes: null, isActive: true, isRecurring: false, updatedAt: '2024-04-05T10:00:00',
};

describe('ExpensesComponent', () => {
  let fixture: ComponentFixture<ExpensesComponent>;
  let component: ExpensesComponent;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('ApiService', [
      'getPeriods', 'getCategories', 'getExpensesByPeriod',
      'createExpense', 'updateExpense', 'markExpenseAsPaid', 'deleteExpense',
      'saveExpenseOrder', 'batchPayExpenses', 'batchCancelExpenses', 'batchDeleteExpenses'
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
    const mockEvent = (left = 0, top = 0, width = 28, height = 28) =>
      ({ currentTarget: { getBoundingClientRect: () => ({ left, top, width, height }) } } as unknown as MouseEvent);

    it('atualiza paymentStatus para Paid na lista', fakeAsync(() => {
      component.expenses.set([EXPENSE]);
      apiSpy.markExpenseAsPaid.and.returnValue(of(undefined));

      component.markAsPaid(mockEvent(), EXPENSE);
      tick();

      const updated = component.expenses().find(e => e.id === 'e-1');
      expect(updated?.paymentStatus).toBe(PaymentStatus.Paid);
    }));

    it('adiciona burst ao signal bursts', () => {
      apiSpy.markExpenseAsPaid.and.returnValue(of(undefined));
      component.markAsPaid(mockEvent(100, 200, 28, 28), EXPENSE);
      expect(component.bursts().length).toBe(1);
      expect(component.bursts()[0].origin.x).toBe(114);
      expect(component.bursts()[0].origin.y).toBe(214);
    });
  });

  describe('removeBurst()', () => {
    it('remove burst do signal pelo id', () => {
      component.bursts.set([{ id: 1, origin: { x: 100, y: 200 } }]);
      component.removeBurst(1);
      expect(component.bursts().length).toBe(0);
    });
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

  describe('drag and drop', () => {
    const EXPENSE_B: ExpenseResponse = { ...EXPENSE, id: 'e-2', description: 'Internet' };

    beforeEach(() => {
      component.expenses.set([EXPENSE, EXPENSE_B]);
    });

    it('onDrop reordena a lista de despesas', () => {
      component.onDragStart(0);
      component.onDrop(1);
      expect(component.expenses()[0].id).toBe('e-2');
      expect(component.expenses()[1].id).toBe('e-1');
    });

    it('onDrop popula pendingOrders', () => {
      component.onDragStart(0);
      component.onDrop(1);
      expect(component.pendingOrders().length).toBe(2);
      expect(component.hasPendingOrder()).toBeTrue();
    });

    it('onDrop não altera lista quando origem = destino', () => {
      component.onDragStart(0);
      component.onDrop(0);
      expect(component.expenses()[0].id).toBe('e-1');
      expect(component.pendingOrders().length).toBe(0);
    });

    it('saveOrder chama saveExpenseOrder e limpa pendingOrders', fakeAsync(() => {
      apiSpy.saveExpenseOrder.and.returnValue(of(undefined));
      component.onDragStart(0);
      component.onDrop(1);
      component.saveOrder();
      tick();
      expect(apiSpy.saveExpenseOrder).toHaveBeenCalled();
      expect(component.pendingOrders().length).toBe(0);
      expect(component.hasPendingOrder()).toBeFalse();
    }));

    it('saveOrder não faz nada quando pendingOrders está vazio', () => {
      component.saveOrder();
      expect(apiSpy.saveExpenseOrder).not.toHaveBeenCalled();
    });
  });

  describe('seleção em lote', () => {
    const EXPENSE_B: ExpenseResponse = { ...EXPENSE, id: 'e-2', description: 'Internet' };

    beforeEach(() => {
      component.expenses.set([EXPENSE, EXPENSE_B]);
    });

    it('toggleSelect adiciona e remove id da seleção', () => {
      component.toggleSelect('e-1');
      expect(component.selectedExpenseIds()).toContain('e-1');
      component.toggleSelect('e-1');
      expect(component.selectedExpenseIds()).not.toContain('e-1');
    });

    it('toggleSelectAll seleciona todos os itens exibidos', () => {
      component.toggleSelectAll();
      expect(component.selectedExpenseIds().length).toBe(2);
      expect(component.allSelected()).toBeTrue();
    });

    it('toggleSelectAll desmarca todos quando todos estão selecionados', () => {
      component.selectedExpenseIds.set(['e-1', 'e-2']);
      component.toggleSelectAll();
      expect(component.selectedExpenseIds().length).toBe(0);
    });

    it('isSelected retorna true apenas para ids selecionados', () => {
      component.selectedExpenseIds.set(['e-1']);
      expect(component.isSelected('e-1')).toBeTrue();
      expect(component.isSelected('e-2')).toBeFalse();
    });

    it('batchPay marca itens como Paid e limpa seleção', fakeAsync(() => {
      apiSpy.batchPayExpenses.and.returnValue(of(undefined));
      component.selectedExpenseIds.set(['e-1', 'e-2']);
      component.batchPay();
      tick();
      expect(apiSpy.batchPayExpenses).toHaveBeenCalledWith(['e-1', 'e-2']);
      expect(component.expenses().every(e => e.paymentStatus === PaymentStatus.Paid)).toBeTrue();
      expect(component.selectedExpenseIds().length).toBe(0);
    }));

    it('batchCancel marca itens como Cancelled e limpa seleção', fakeAsync(() => {
      apiSpy.batchCancelExpenses.and.returnValue(of(undefined));
      component.selectedExpenseIds.set(['e-1']);
      component.batchCancel();
      tick();
      expect(apiSpy.batchCancelExpenses).toHaveBeenCalledWith(['e-1']);
      expect(component.expenses().find(e => e.id === 'e-1')?.paymentStatus).toBe(PaymentStatus.Cancelled);
      expect(component.selectedExpenseIds().length).toBe(0);
    }));

    it('batchDelete remove itens da lista e limpa seleção', fakeAsync(() => {
      spyOn(window, 'confirm').and.returnValue(true);
      apiSpy.batchDeleteExpenses.and.returnValue(of(undefined));
      component.selectedExpenseIds.set(['e-1', 'e-2']);
      component.batchDelete();
      tick();
      expect(apiSpy.batchDeleteExpenses).toHaveBeenCalledWith(['e-1', 'e-2']);
      expect(component.expenses().length).toBe(0);
      expect(component.selectedExpenseIds().length).toBe(0);
    }));

    it('batchDelete não executa quando usuário cancela confirm', () => {
      spyOn(window, 'confirm').and.returnValue(false);
      component.selectedExpenseIds.set(['e-1']);
      component.batchDelete();
      expect(apiSpy.batchDeleteExpenses).not.toHaveBeenCalled();
    });
  });

  describe('filtros — onApplyFilters()', () => {
    beforeEach(() => { (component as any).selectedPeriodId = 'p-1'; });

    it('aplica description e dispara loadPage', fakeAsync(() => {
      component.onApplyFilters({ description: 'aluguel', categoryId: '', paymentStatus: '', fortnightType: '', sourceType: '' });
      tick();
      expect(component.filterDescription()).toBe('aluguel');
      expect(apiSpy.getExpensesByPeriod).toHaveBeenCalled();
    }));

    it('aplica paymentStatus numérico', fakeAsync(() => {
      component.onApplyFilters({ description: '', categoryId: '', paymentStatus: PaymentStatus.Paid, fortnightType: '', sourceType: '' });
      tick();
      expect(component.filterStatus()).toBe(PaymentStatus.Paid);
    }));

    it('converte string vazia em null para campos enum', fakeAsync(() => {
      component.onApplyFilters({ description: '', categoryId: '', paymentStatus: '', fortnightType: '', sourceType: '' });
      tick();
      expect(component.filterStatus()).toBeNull();
      expect(component.filterFortnightType()).toBeNull();
      expect(component.filterSourceType()).toBeNull();
    }));

    it('reseta currentPage para 1', fakeAsync(() => {
      component.currentPage.set(3);
      component.onApplyFilters({ description: '', categoryId: '', paymentStatus: '', fortnightType: '', sourceType: '' });
      tick();
      expect(component.currentPage()).toBe(1);
    }));
  });

  describe('filtros — onClearFilters()', () => {
    beforeEach(() => { (component as any).selectedPeriodId = 'p-1'; });

    it('limpa todos os filtros e dispara loadPage', fakeAsync(() => {
      component.filterDescription.set('aluguel');
      component.filterCategoryId.set('cat-1');
      component.filterStatus.set(PaymentStatus.Paid);
      component.onClearFilters();
      tick();
      expect(component.filterDescription()).toBe('');
      expect(component.filterCategoryId()).toBe('');
      expect(component.filterStatus()).toBeNull();
      expect(apiSpy.getExpensesByPeriod).toHaveBeenCalled();
    }));
  });

  describe('activeFilterCount', () => {
    it('retorna 0 quando nenhum filtro ativo', () => {
      expect(component.activeFilterCount()).toBe(0);
    });

    it('conta cada filtro ativo individualmente', () => {
      component.filterDescription.set('a');
      component.filterCategoryId.set('cat-1');
      component.filterStatus.set(PaymentStatus.Paid);
      expect(component.activeFilterCount()).toBe(3);
    });
  });

  describe('ordenação server-side (popup de filtro)', () => {
    it('filterFields inclui sectionHeader "Ordenar por:"', () => {
      const fields = component.filterFields();
      const header = fields.find(f => f.type === 'sectionHeader' && f.key === 'sortHeader');
      expect(header).toBeTruthy();
      expect(header!.label).toBe('Ordenar por:');
    });

    it('filterFields inclui campo sortColumn após o sectionHeader', () => {
      const fields = component.filterFields();
      const headerIdx = fields.findIndex(f => f.key === 'sortHeader');
      const sortColIdx = fields.findIndex(f => f.key === 'sortColumn');
      expect(sortColIdx).toBeGreaterThan(headerIdx);
    });

    it('filterFields inclui campo sortDirection após sortColumn', () => {
      const fields = component.filterFields();
      const sortColIdx = fields.findIndex(f => f.key === 'sortColumn');
      const sortDirIdx = fields.findIndex(f => f.key === 'sortDirection');
      expect(sortDirIdx).toBeGreaterThan(sortColIdx);
    });

    it('sortColumn tem todas as 9 colunas disponíveis como opções', () => {
      const fields = component.filterFields();
      const sortColField = fields.find(f => f.key === 'sortColumn');
      expect(sortColField?.options?.length).toBeGreaterThanOrEqual(9);
    });

    it('sortColumn inclui opção "Data Última Atualização"', () => {
      const fields = component.filterFields();
      const sortColField = fields.find(f => f.key === 'sortColumn');
      const labels = sortColField?.options?.map(o => o.label) ?? [];
      expect(labels).toContain('Data Última Atualização');
    });

    it('sortDirection tem opções Crescente e Decrescente', () => {
      const fields = component.filterFields();
      const sortDirField = fields.find(f => f.key === 'sortDirection');
      const labels = sortDirField?.options?.map(o => o.label) ?? [];
      expect(labels).toContain('Crescente');
      expect(labels).toContain('Decrescente');
    });

    it('onApplyFilters define filterSortColumn e filterSortDirection', () => {
      component.onApplyFilters({ sortColumn: String(ExpenseSortColumn.Amount), sortDirection: String(SortDirection.Descending) });
      expect(component.filterSortColumn()).toBe(ExpenseSortColumn.Amount);
      expect(component.filterSortDirection()).toBe(SortDirection.Descending);
    });

    it('onApplyFilters define null quando sortColumn não informado', () => {
      component.filterSortColumn.set(ExpenseSortColumn.Amount);
      component.onApplyFilters({ sortColumn: '', sortDirection: '' });
      expect(component.filterSortColumn()).toBeNull();
      expect(component.filterSortDirection()).toBeNull();
    });

    it('onClearFilters reseta filterSortColumn e filterSortDirection para null', () => {
      component.filterSortColumn.set(ExpenseSortColumn.DueDate);
      component.filterSortDirection.set(SortDirection.Ascending);
      component.onClearFilters();
      expect(component.filterSortColumn()).toBeNull();
      expect(component.filterSortDirection()).toBeNull();
    });

    it('loadPage passa sortColumn e sortDirection ao getExpensesByPeriod quando definidos', fakeAsync(() => {
      component.filterSortColumn.set(ExpenseSortColumn.Amount);
      component.filterSortDirection.set(SortDirection.Ascending);
      component.selectedPeriodId = 'p-1';
      (component as any)['loadPage']();
      tick();
      expect(apiSpy.getExpensesByPeriod).toHaveBeenCalledWith('p-1', jasmine.objectContaining({
        sortColumn: ExpenseSortColumn.Amount,
        sortDirection: SortDirection.Ascending,
      }));
    }));

    it('loadPage não passa sortColumn quando não definido', fakeAsync(() => {
      component.filterSortColumn.set(null);
      component.filterSortDirection.set(null);
      component.selectedPeriodId = 'p-1';
      (component as any)['loadPage']();
      tick();
      const callArgs = apiSpy.getExpensesByPeriod.calls.mostRecent().args[1] as any;
      expect(callArgs.sortColumn).toBeUndefined();
      expect(callArgs.sortDirection).toBeUndefined();
    }));

    it('sort não incrementa activeFilterCount', () => {
      component.filterSortColumn.set(ExpenseSortColumn.Description);
      component.filterSortDirection.set(SortDirection.Ascending);
      expect(component.activeFilterCount()).toBe(0);
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

  describe('updatedAt — modal de edição', () => {
    it('editingUpdatedAt é definido ao abrir modal em modo edit', () => {
      component.openEditModal(EXPENSE);
      expect(component.editingUpdatedAt()).toBe('2024-04-05T10:00:00');
    });

    it('editingUpdatedAt é null ao abrir modal em modo create', () => {
      component.openCreateModal();
      expect(component.editingUpdatedAt()).toBeNull();
    });

    it('editingUpdatedAt é null após fechar modal', () => {
      component.openEditModal(EXPENSE);
      component.closeModal();
      expect(component.editingUpdatedAt()).toBeNull();
    });
  });

  describe('ordenação client-side por updatedAt', () => {
    const EXPENSE_B: ExpenseResponse = { ...EXPENSE, id: 'e-2', updatedAt: '2024-04-06T10:00:00' };

    beforeEach(() => {
      component.expenses.set([EXPENSE_B, EXPENSE]);
    });

    it('displayedExpenses ordena por updatedAt asc quando sortCol = updatedAt', () => {
      component.sortCol.set('updatedAt');
      component.sortDir.set('asc');
      const result = component.displayedExpenses();
      expect(result[0].id).toBe('e-1');
      expect(result[1].id).toBe('e-2');
    });

    it('displayedExpenses ordena por updatedAt desc quando sortCol = updatedAt', () => {
      component.sortCol.set('updatedAt');
      component.sortDir.set('desc');
      const result = component.displayedExpenses();
      expect(result[0].id).toBe('e-2');
      expect(result[1].id).toBe('e-1');
    });
  });
});
