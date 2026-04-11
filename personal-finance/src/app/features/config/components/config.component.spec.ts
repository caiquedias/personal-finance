import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { RouterModule } from '@angular/router';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { of, throwError } from 'rxjs';
import { ConfigComponent } from './config.component';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { CategoryResponse, LookupItem } from '../../../core/models/models';

const CATEGORY: CategoryResponse = {
  id: 'cat-1', userId: 'u', name: 'Moradia', color: '#aabbcc', icon: null, isGlobal: false, isActive: true
};

const LOOKUP_ITEM: LookupItem = { id: 1, name: 'Pendente', description: 'Aguardando', isSystemSeed: false };

describe('ConfigComponent', () => {
  let fixture: ComponentFixture<ConfigComponent>;
  let component: ConfigComponent;
  let apiSpy: jasmine.SpyObj<ApiService>;
  let authSpy: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('ApiService', [
      'getCategories', 'getPaymentStatuses', 'getSourceTypes', 'getFortnightTypes',
      'createCategory', 'deleteCategory', 'updateCategory',
      'createPaymentStatus', 'deletePaymentStatus', 'updatePaymentStatus',
      'createSourceType', 'deleteSourceType', 'updateSourceType',
      'createFortnightType', 'deleteFortnightType', 'updateFortnightType',
    ]);
    authSpy = jasmine.createSpyObj('AuthService', [], {
      isAdmin: jasmine.createSpy().and.returnValue(false),
    });

    apiSpy.getCategories.and.returnValue(of([CATEGORY]));
    apiSpy.getPaymentStatuses.and.returnValue(of([LOOKUP_ITEM]));
    apiSpy.getSourceTypes.and.returnValue(of([]));
    apiSpy.getFortnightTypes.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [ConfigComponent, RouterModule.forRoot([])],
      providers: [
        { provide: ApiService,  useValue: apiSpy },
        { provide: AuthService, useValue: authSpy },
      ],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(ConfigComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('cria o componente', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnInit()', () => {
    it('carrega categorias e lookup tables', fakeAsync(() => {
      tick();
      expect(component.categories()).toEqual([CATEGORY]);
      expect(component.paymentStatuses()).toEqual([LOOKUP_ITEM]);
    }));
  });

  describe('modalTitle()', () => {
    it('retorna "Editar Categoria" no modo edit-cat', () => {
      component.modalMode.set('edit-cat');
      expect(component.modalTitle()).toBe('Editar Categoria');
    });

    it('retorna label correto para tab payment', () => {
      component.modalMode.set('edit-lookup');
      component.editingLookupTab = 'payment';
      expect(component.modalTitle()).toBe('Editar Status de Pagamento');
    });

    it('retorna label correto para tab source', () => {
      component.modalMode.set('edit-lookup');
      component.editingLookupTab = 'source';
      expect(component.modalTitle()).toBe('Editar Tipo de Fonte');
    });

    it('retorna label correto para tab fortnight', () => {
      component.modalMode.set('edit-lookup');
      component.editingLookupTab = 'fortnight';
      expect(component.modalTitle()).toBe('Editar Tipo de Quinzena');
    });
  });

  describe('openEditCat()', () => {
    it('abre modal com dados da categoria', () => {
      component.openEditCat(CATEGORY);
      expect(component.modalOpen()).toBeTrue();
      expect(component.modalMode()).toBe('edit-cat');
      expect(component.editCatForm.get('name')!.value).toBe('Moradia');
    });
  });

  describe('onSaveCat()', () => {
    beforeEach(() => {
      component.openEditCat(CATEGORY);
    });

    it('atualiza categoria na lista quando sucesso', fakeAsync(() => {
      apiSpy.updateCategory.and.returnValue(of(undefined));
      component.editCatForm.patchValue({ name: 'Moradia Nova', color: '#fff' });
      component.onSaveCat();
      tick();
      expect(component.categories().find(c => c.id === 'cat-1')?.name).toBe('Moradia Nova');
      expect(component.modalOpen()).toBeFalse();
    }));

    it('define modalError quando updateCategory falha', fakeAsync(() => {
      apiSpy.updateCategory.and.returnValue(
        throwError(() => ({ error: { message: 'Erro ao salvar categoria.' } }))
      );
      component.onSaveCat();
      tick();
      expect(component.modalError()).toBe('Erro ao salvar categoria.');
    }));
  });

  describe('deleteCategory()', () => {
    beforeEach(() => {
      spyOn(window, 'confirm').and.returnValue(true);
      apiSpy.deleteCategory.and.returnValue(of(undefined));
      component.categories.set([CATEGORY]);
    });

    it('remove categoria após confirmação', fakeAsync(() => {
      component.deleteCategory('cat-1', false);
      tick();
      expect(component.categories().find(c => c.id === 'cat-1')).toBeUndefined();
    }));

    it('não deleta quando usuário cancela', () => {
      (window.confirm as jasmine.Spy).and.returnValue(false);
      component.deleteCategory('cat-1', false);
      expect(apiSpy.deleteCategory).not.toHaveBeenCalled();
    });
  });

  describe('openEditLookup()', () => {
    it('abre modal com dados do lookup item (payment)', () => {
      component.openEditLookup(LOOKUP_ITEM, 'payment');
      expect(component.modalOpen()).toBeTrue();
      expect(component.modalMode()).toBe('edit-lookup');
      expect(component.editingLookupTab).toBe('payment');
      expect(component.editingLookupId).toBe(1);
      expect(component.editLookupForm.get('name')!.value).toBe('Pendente');
    });
  });

  describe('onSaveLookup()', () => {
    beforeEach(() => {
      component.paymentStatuses.set([LOOKUP_ITEM]);
      component.openEditLookup(LOOKUP_ITEM, 'payment');
    });

    it('atualiza payment status e fecha modal', fakeAsync(() => {
      const updated: LookupItem = { ...LOOKUP_ITEM, name: 'Pago' };
      apiSpy.updatePaymentStatus.and.returnValue(of(updated));

      component.editLookupForm.patchValue({ name: 'Pago', description: '' });
      component.onSaveLookup();
      tick();

      expect(component.paymentStatuses().find(i => i.id === 1)?.name).toBe('Pago');
      expect(component.modalOpen()).toBeFalse();
    }));

    it('define modalError quando falha', fakeAsync(() => {
      apiSpy.updatePaymentStatus.and.returnValue(
        throwError(() => ({ error: { message: 'Erro ao salvar.' } }))
      );
      component.onSaveLookup();
      tick();
      expect(component.modalError()).toBe('Erro ao salvar.');
    }));
  });

  describe('deleteLookupItem()', () => {
    beforeEach(() => {
      spyOn(window, 'confirm').and.returnValue(true);
      apiSpy.deletePaymentStatus.and.returnValue(of(undefined));
      component.paymentStatuses.set([LOOKUP_ITEM]);
    });

    it('remove payment status após confirmação', fakeAsync(() => {
      component.deleteLookupItem(1, 'payment');
      tick();
      expect(component.paymentStatuses().find(i => i.id === 1)).toBeUndefined();
    }));

    it('não deleta quando usuário cancela', () => {
      (window.confirm as jasmine.Spy).and.returnValue(false);
      component.deleteLookupItem(1, 'payment');
      expect(apiSpy.deletePaymentStatus).not.toHaveBeenCalled();
    });
  });

  describe('closeModal()', () => {
    it('fecha modal e limpa estado de edição', () => {
      component.openEditCat(CATEGORY);
      component.closeModal();
      expect(component.modalOpen()).toBeFalse();
      expect(component.editingLookupId).toBeNull();
      expect(component.editingLookupTab).toBeNull();
    });
  });

  describe('onCreateCategory()', () => {
    it('não cria quando form inválido', () => {
      component.catForm.get('name')!.setValue('');
      component.onCreateCategory();
      expect(apiSpy.createCategory).not.toHaveBeenCalled();
    });

    it('adiciona categoria à lista quando sucesso', fakeAsync(() => {
      const newCat: CategoryResponse = { ...CATEGORY, id: 'cat-new', name: 'Nova' };
      apiSpy.createCategory.and.returnValue(of(newCat));

      component.catForm.setValue({ name: 'Nova', color: '#aabbcc', icon: '' });
      component.onCreateCategory();
      tick();

      expect(component.categories()).toContain(newCat);
      expect(component.showCatForm()).toBeFalse();
    }));
  });
});
