import { TestBed, fakeAsync, tick, flush } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { NO_ERRORS_SCHEMA, WritableSignal } from '@angular/core';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of, throwError } from 'rxjs';
import { AdminUsersComponent } from './admin-users.component';
import { ApiService } from '../../../../core/services/api.service';
import { AdminUserResponse } from '../../../../core/models/models';

// Usuário com os flags de MFA expostos pelo backend (#490)
type MfaUser = AdminUserResponse & { mfaEnabled: boolean; mfaSetupPending: boolean };

// API do componente a ser criada pelo Green — tipada aqui para o Red não quebrar a compilação
type MfaModalApi = {
  showResetMfaModal: WritableSignal<boolean>;
  openResetMfa(user: AdminUserResponse): void;
  onConfirmResetMfa(): void;
};

const USER: MfaUser = {
  id:        'u-1',
  name:      'João Silva',
  email:     'joao@exemplo.com',
  roles:     ['User'],
  isActive:  true,
  isDeleted: false,
  createdAt: '2024-01-10T00:00:00',
  mfaEnabled:      false,
  mfaSetupPending: false,
};

const PAGE_RESULT = { items: [USER], totalCount: 1, pageNumber: 1, pageSize: 20 };

describe('AdminUsersComponent', () => {
  let fixture: ComponentFixture<AdminUsersComponent>;
  let component: AdminUsersComponent;
  let apiSpy: jasmine.SpyObj<ApiService>;
  const sut = (): MfaModalApi => component as unknown as MfaModalApi;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('ApiService', [
      'getAdminUsers', 'createAdminUser', 'updateAdminUser',
      'toggleUserActive', 'assignRole', 'removeRole', 'resetUserPassword', 'resetUserMfa',
    ]);
    apiSpy.getAdminUsers.and.returnValue(of(PAGE_RESULT));

    await TestBed.configureTestingModule({
      imports: [AdminUsersComponent],
      providers: [{ provide: ApiService, useValue: apiSpy }, provideNoopAnimations()],
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

  describe('botão Resetar MFA condicional', () => {
    const MFA_ON:      MfaUser = { ...USER, id: 'u-on',      mfaEnabled: true,  mfaSetupPending: false };
    const MFA_PENDING: MfaUser = { ...USER, id: 'u-pending', mfaEnabled: false, mfaSetupPending: true  };
    const MFA_OFF:     MfaUser = { ...USER, id: 'u-off',     mfaEnabled: false, mfaSetupPending: false };

    // Evita bloquear o browser headless caso o handler ainda use window.confirm
    beforeEach(() => { spyOn(window, 'confirm').and.returnValue(false); });

    function render(users: MfaUser[]): void {
      apiSpy.getAdminUsers.and.returnValue(of({ items: users, totalCount: users.length, pageNumber: 1, pageSize: 20 }));
      fixture.detectChanges();
      tick();
      fixture.detectChanges();
    }

    const mfaButtons = (): HTMLButtonElement[] =>
      Array.from(fixture.nativeElement.querySelectorAll('button[title="Resetar MFA"]'));

    it('exibe o botão quando mfaEnabled=true', fakeAsync(() => {
      render([MFA_ON]);
      expect(mfaButtons().length).toBe(1);
    }));

    it('exibe o botão quando há setup pendente (mfaSetupPending=true)', fakeAsync(() => {
      render([MFA_PENDING]);
      expect(mfaButtons().length).toBe(1);
    }));

    it('oculta o botão quando mfaEnabled=false e mfaSetupPending=false', fakeAsync(() => {
      render([MFA_OFF]);
      expect(mfaButtons().length).toBe(0);
    }));

    it('exibe o botão apenas nas linhas aplicáveis', fakeAsync(() => {
      render([MFA_ON, MFA_OFF, MFA_PENDING]);
      expect(mfaButtons().length).toBe(2);
    }));

    it('clicar no botão abre o modal sem chamar a API', fakeAsync(() => {
      render([MFA_ON]);
      mfaButtons()[0].click();
      fixture.detectChanges();
      expect(sut().showResetMfaModal()).toBeTrue();
      expect(fixture.nativeElement.querySelector('.modal-center')).not.toBeNull();
      expect(apiSpy.resetUserMfa).not.toHaveBeenCalled();
    }));
  });

  describe('modal de confirmação de reset de MFA', () => {
    const MFA_ON: MfaUser = { ...USER, mfaEnabled: true, mfaSetupPending: false };

    beforeEach(fakeAsync(() => {
      apiSpy.getAdminUsers.and.returnValue(of({ items: [MFA_ON], totalCount: 1, pageNumber: 1, pageSize: 20 }));
      fixture.detectChanges(); tick();
      spyOn(window, 'confirm');
    }));

    it('openResetMfa abre o modal, seleciona o usuário e não chama a API', () => {
      sut().openResetMfa(MFA_ON);
      expect(sut().showResetMfaModal()).toBeTrue();
      expect(component.selectedUser()?.id).toBe(MFA_ON.id);
      expect(apiSpy.resetUserMfa).not.toHaveBeenCalled();
    });

    it('openResetMfa limpa resetError de uma ação anterior', () => {
      component.resetError.set('erro antigo');
      sut().openResetMfa(MFA_ON);
      expect(component.resetError()).toBeNull();
    });

    it('nunca usa window.confirm', fakeAsync(() => {
      apiSpy.resetUserMfa.and.returnValue(of(void 0));
      sut().openResetMfa(MFA_ON);
      sut().onConfirmResetMfa();
      flush();
      expect(window.confirm).not.toHaveBeenCalled();
      expect(apiSpy.resetUserMfa).toHaveBeenCalled();
    }));

    it('fechar/cancelar o modal não chama resetUserMfa', () => {
      sut().openResetMfa(MFA_ON);
      sut().showResetMfaModal.set(false);
      expect(apiSpy.resetUserMfa).not.toHaveBeenCalled();
      expect(sut().showResetMfaModal()).toBeFalse();
    });

    it('confirmar chama resetUserMfa com o id do usuário selecionado', fakeAsync(() => {
      apiSpy.resetUserMfa.and.returnValue(of(void 0));
      sut().openResetMfa(MFA_ON);
      sut().onConfirmResetMfa();
      expect(apiSpy.resetUserMfa).toHaveBeenCalledOnceWith(MFA_ON.id);
      flush();
    }));

    it('sucesso: fecha o modal, exibe actionMessage e libera loadingAction', fakeAsync(() => {
      apiSpy.resetUserMfa.and.returnValue(of(void 0));
      sut().openResetMfa(MFA_ON);
      sut().onConfirmResetMfa();
      tick();
      expect(sut().showResetMfaModal()).toBeFalse();
      expect(component.actionMessage()).toContain(MFA_ON.name);
      expect(component.actionError()).toBeNull();
      expect(component.loadingAction()).toBeFalse();
      flush();
    }));

    it('sucesso: atualiza o usuário local com mfaEnabled=false e mfaSetupPending=false', fakeAsync(() => {
      apiSpy.resetUserMfa.and.returnValue(of(void 0));
      sut().openResetMfa(MFA_ON);
      sut().onConfirmResetMfa();
      tick();
      const updated = component.users()[0] as MfaUser;
      expect(updated.mfaEnabled).toBeFalse();
      expect(updated.mfaSetupPending).toBeFalse();
      flush();
    }));

    it('sucesso: o botão Resetar MFA some da linha após o reset', fakeAsync(() => {
      apiSpy.resetUserMfa.and.returnValue(of(void 0));
      fixture.detectChanges();
      sut().openResetMfa(MFA_ON);
      sut().onConfirmResetMfa();
      tick();
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelectorAll('button[title="Resetar MFA"]').length).toBe(0);
      flush();
    }));

    it('erro: mantém o modal aberto e exibe resetError (mensagem do backend)', fakeAsync(() => {
      apiSpy.resetUserMfa.and.returnValue(throwError(() => ({ error: { message: 'O MFA não está ativo.' } })));
      sut().openResetMfa(MFA_ON);
      sut().onConfirmResetMfa();
      tick();
      fixture.detectChanges();
      expect(sut().showResetMfaModal()).toBeTrue();
      expect(component.resetError()).toBe('O MFA não está ativo.');
      expect(fixture.nativeElement.querySelector('.modal-center .form-error')?.textContent).toContain('O MFA não está ativo.');
      expect(component.actionMessage()).toBeNull();
      expect(component.loadingAction()).toBeFalse();
    }));

    it('erro sem mensagem: usa texto padrão e não altera o usuário local', fakeAsync(() => {
      apiSpy.resetUserMfa.and.returnValue(throwError(() => ({})));
      sut().openResetMfa(MFA_ON);
      sut().onConfirmResetMfa();
      tick();
      expect(component.resetError()).toBe('Erro ao resetar MFA.');
      expect((component.users()[0] as MfaUser).mfaEnabled).toBeTrue();
    }));

    it('botão confirmar do modal fica desabilitado com loadingAction=true e dispara a API ao clicar', fakeAsync(() => {
      apiSpy.resetUserMfa.and.returnValue(of(void 0));
      fixture.detectChanges();
      sut().openResetMfa(MFA_ON);
      fixture.detectChanges();
      const confirmBtn = (): HTMLButtonElement =>
        fixture.nativeElement.querySelector('.modal-center .modal-footer button.btn-primary');

      component.loadingAction.set(true);
      fixture.detectChanges();
      expect(confirmBtn().disabled).toBeTrue();

      component.loadingAction.set(false);
      fixture.detectChanges();
      confirmBtn().click();
      expect(apiSpy.resetUserMfa).toHaveBeenCalledOnceWith(MFA_ON.id);
      flush();
    }));
  });

  describe('auto-dismiss do banner de reset de MFA', () => {
    const MFA_ON: MfaUser = { ...USER, mfaEnabled: true, mfaSetupPending: false };

    beforeEach(fakeAsync(() => {
      apiSpy.getAdminUsers.and.returnValue(of({ items: [MFA_ON], totalCount: 1, pageNumber: 1, pageSize: 20 }));
      fixture.detectChanges(); tick();
    }));

    it('a mensagem de sucesso permanece antes de 5000ms e some após 5000ms', fakeAsync(() => {
      apiSpy.resetUserMfa.and.returnValue(of(void 0));
      sut().openResetMfa(MFA_ON);
      sut().onConfirmResetMfa();
      tick(4999);
      expect(component.actionMessage()).not.toBeNull();
      tick(1);
      expect(component.actionMessage()).toBeNull();
    }));

    it('o erro do reset não é dispensado automaticamente', fakeAsync(() => {
      apiSpy.resetUserMfa.and.returnValue(throwError(() => ({ error: { message: 'falhou' } })));
      sut().openResetMfa(MFA_ON);
      sut().onConfirmResetMfa();
      tick(10000);
      expect(component.resetError()).toBe('falhou');
      expect(sut().showResetMfaModal()).toBeTrue();
    }));

    it('timer anterior é cancelado ao iniciar nova ação (não apaga a nova mensagem)', fakeAsync(() => {
      apiSpy.resetUserMfa.and.returnValue(of(void 0));
      sut().openResetMfa(MFA_ON);
      sut().onConfirmResetMfa();
      tick(3000);

      sut().openResetMfa(MFA_ON);
      sut().onConfirmResetMfa();
      tick(4000); // 7000ms desde o primeiro sucesso, 4000ms desde o segundo
      expect(component.actionMessage()).not.toBeNull();
      tick(1000);
      expect(component.actionMessage()).toBeNull();
    }));
  });

  describe('formatDate()', () => {
    it('retorna data no formato pt-BR', () => {
      const result = component.formatDate('2024-01-10T00:00:00');
      expect(result).toContain('2024');
    });
  });
});
