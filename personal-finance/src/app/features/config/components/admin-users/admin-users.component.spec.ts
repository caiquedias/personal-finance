import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { of, throwError } from 'rxjs';
import { AdminUsersComponent } from './admin-users.component';
import { ApiService } from '../../../../core/services/api.service';
import { AdminUserResponse } from '../../../../core/models/models';

const USER: AdminUserResponse = {
  id:        'u-1',
  name:      'João Silva',
  email:     'joao@exemplo.com',
  roles:     ['User'],
  isActive:  true,
  isDeleted: false,
  createdAt: '2024-01-10T00:00:00',
};

const PAGE_RESULT = { items: [USER], totalCount: 1, pageNumber: 1, pageSize: 20 };

describe('AdminUsersComponent', () => {
  let fixture: ComponentFixture<AdminUsersComponent>;
  let component: AdminUsersComponent;
  let apiSpy: jasmine.SpyObj<ApiService>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('ApiService', [
      'getAdminUsers', 'createAdminUser', 'updateAdminUser',
      'toggleUserActive', 'assignRole', 'removeRole', 'resetUserPassword',
    ]);
    apiSpy.getAdminUsers.and.returnValue(of(PAGE_RESULT));

    await TestBed.configureTestingModule({
      imports: [AdminUsersComponent],
      providers: [{ provide: ApiService, useValue: apiSpy }],
      schemas: [NO_ERRORS_SCHEMA],
    }).compileComponents();

    fixture   = TestBed.createComponent(AdminUsersComponent);
    component = fixture.componentInstance;
  });

  it('cria o componente', () => {
    expect(component).toBeTruthy();
  });

  describe('ngOnInit()', () => {
    it('carrega usuários e define loading=false', fakeAsync(() => {
      fixture.detectChanges();
      tick();
      expect(component.users()).toEqual([USER]);
      expect(component.totalCount()).toBe(1);
      expect(component.loading()).toBeFalse();
    }));

    it('define loading=false em caso de erro', fakeAsync(() => {
      apiSpy.getAdminUsers.and.returnValue(throwError(() => new Error()));
      fixture.detectChanges();
      tick();
      expect(component.loading()).toBeFalse();
    }));
  });

  describe('filterFields getter', () => {
    it('retorna 3 campos', () => {
      expect(component.filterFields.length).toBe(3);
    });

    it('reflete nameFilter atual', () => {
      component.nameFilter = 'João';
      const field = component.filterFields.find(f => f.key === 'name')!;
      expect(field.value).toBe('João');
    });
  });

  describe('onFilterApply()', () => {
    beforeEach(fakeAsync(() => { fixture.detectChanges(); tick(); }));

    it('aplica filtros, fecha painel e recarrega', fakeAsync(() => {
      component.filterOpen.set(true);
      component.onFilterApply({ name: 'João', email: 'joao@', status: 'true' });
      tick();
      expect(component.nameFilter).toBe('João');
      expect(component.emailFilter).toBe('joao@');
      expect(component.statusFilter).toBe('true');
      expect(component.filterOpen()).toBeFalse();
      expect(apiSpy.getAdminUsers).toHaveBeenCalled();
    }));

    it('reseta página para 1 ao aplicar', fakeAsync(() => {
      component.currentPage.set(3);
      component.onFilterApply({ name: '', email: '', status: '' });
      tick();
      expect(component.currentPage()).toBe(1);
    }));
  });

  describe('onFilterClear()', () => {
    beforeEach(fakeAsync(() => { fixture.detectChanges(); tick(); }));

    it('limpa filtros e fecha painel', fakeAsync(() => {
      component.nameFilter  = 'x';
      component.emailFilter = 'y';
      component.filterOpen.set(true);
      component.onFilterClear();
      tick();
      expect(component.nameFilter).toBe('');
      expect(component.emailFilter).toBe('');
      expect(component.filterOpen()).toBeFalse();
    }));
  });

  describe('toggleActive()', () => {
    beforeEach(fakeAsync(() => { fixture.detectChanges(); tick(); }));

    it('inverte isActive do usuário na lista', fakeAsync(() => {
      apiSpy.toggleUserActive.and.returnValue(of(void 0));
      component.toggleActive(USER);
      tick();
      expect(component.users()[0].isActive).toBeFalse();
    }));
  });

  describe('formatDate()', () => {
    it('retorna data no formato pt-BR', () => {
      const result = component.formatDate('2024-01-10T00:00:00');
      expect(result).toContain('2024');
    });
  });
});
