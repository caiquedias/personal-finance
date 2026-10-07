import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { LoginComponent } from './login.component';

const QUOTES_STORAGE_KEY = 'login_quotes_usage';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let authSpy: any; // any: verifyMfa/clearMfaChallenge ainda não existem no AuthService (Red)
  let router: Router;

  beforeEach(async () => {
    authSpy = jasmine.createSpyObj('AuthService', ['login', 'verifyMfa', 'clearMfaChallenge']);

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authSpy },
      ],
    }).compileComponents();

    router    = TestBed.inject(Router);
    spyOn(router, 'navigate');

    fixture   = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('cria o componente', () => {
    expect(component).toBeTruthy();
  });

  describe('showError()', () => {
    it('retorna false para campo válido e não tocado', () => {
      expect(component.showError('email')).toBeFalse();
    });

    it('retorna true quando campo inválido e tocado', () => {
      const ctrl = component.form.get('email')!;
      ctrl.markAsTouched();
      expect(component.showError('email')).toBeTrue();
    });

    it('retorna false para campo válido mesmo que tocado', () => {
      const ctrl = component.form.get('email')!;
      ctrl.setValue('valid@email.com');
      ctrl.markAsTouched();
      expect(component.showError('email')).toBeFalse();
    });
  });

  describe('getEmailError()', () => {
    it('retorna mensagem de "obrigatório" quando email vazio', () => {
      component.form.get('email')!.setValue('');
      component.form.get('email')!.markAsTouched();
      expect(component.getEmailError()).toBe('E-mail é obrigatório');
    });

    it('retorna mensagem de "inválido" quando email malformado', () => {
      component.form.get('email')!.setValue('nao-eh-email');
      component.form.get('email')!.markAsTouched();
      expect(component.getEmailError()).toBe('E-mail inválido');
    });

    it('retorna string vazia quando email é válido', () => {
      component.form.get('email')!.setValue('ok@test.com');
      expect(component.getEmailError()).toBe('');
    });
  });

  describe('onSubmit()', () => {
    it('marca todos os campos como tocados e não faz login quando form inválido', () => {
      component.onSubmit();
      expect(authSpy.login).not.toHaveBeenCalled();
      expect(component.form.get('email')!.touched).toBeTrue();
      expect(component.form.get('password')!.touched).toBeTrue();
    });

    it('chama login e navega para / quando credenciais válidas', fakeAsync(() => {
      authSpy.login.and.returnValue(of({
        token: 'tok', name: 'N', email: 'a@b.com'
      }));

      component.form.setValue({ email: 'a@b.com', password: '123456' });
      component.onSubmit();
      tick();

      expect(authSpy.login).toHaveBeenCalledWith({ email: 'a@b.com', password: '123456' });
      expect(router.navigate).toHaveBeenCalledWith(['/']);
    }));

    it('define loading=true durante a chamada', fakeAsync(() => {
      authSpy.login.and.returnValue(of({ token: 'tok', name: 'N', email: 'a@b.com' }));

      component.form.setValue({ email: 'a@b.com', password: '123' });
      component.onSubmit();

      expect(component.loading()).toBeTrue();
      tick();
    }));

    it('define apiError e loading=false quando login retorna erro', fakeAsync(() => {
      authSpy.login.and.returnValue(
        throwError(() => ({ error: { message: 'Credenciais inválidas.' } }))
      );

      component.form.setValue({ email: 'a@b.com', password: 'wrong' });
      component.onSubmit();
      tick();

      expect(component.loading()).toBeFalse();
      expect(component.apiError()).toBe('Credenciais inválidas.');
    }));

    it('usa mensagem fallback quando erro não tem error.message', fakeAsync(() => {
      authSpy.login.and.returnValue(throwError(() => ({})));

      component.form.setValue({ email: 'a@b.com', password: 'wrong' });
      component.onSubmit();
      tick();

      expect(component.apiError()).toBe('Credenciais inválidas.');
    }));
  });

  describe('pickQuote()', () => {
    beforeEach(() => sessionStorage.removeItem(QUOTES_STORAGE_KEY));
    afterEach(() => sessionStorage.removeItem(QUOTES_STORAGE_KEY));

    it('retorna uma frase não vazia', () => {
      expect(component.pickQuote().length).toBeGreaterThan(0);
    });

    it('a mesma frase não aparece mais de 3 vezes consecutivas', () => {
      const counts: Record<string, number> = {};
      for (let i = 0; i < 60; i++) {
        const q = component.pickQuote();
        counts[q] = (counts[q] ?? 0) + 1;
        expect(counts[q]).toBeLessThanOrEqual(3);
      }
    });

    it('reseta o ciclo quando todas as frases atingem 3 repetições', () => {
      // Preenche storage com uso = 3 para todas as 50 frases
      const usage: Record<number, number> = {};
      for (let i = 0; i < 50; i++) usage[i] = 3;
      sessionStorage.setItem(QUOTES_STORAGE_KEY, JSON.stringify(usage));

      // Após reset, deve retornar uma frase e atualizar o storage com contagem 1
      const q = component.pickQuote();
      expect(q.length).toBeGreaterThan(0);

      const stored: Record<number, number> = JSON.parse(sessionStorage.getItem(QUOTES_STORAGE_KEY)!);
      const total = Object.values(stored).reduce((s, v) => s + v, 0);
      expect(total).toBe(1);
    });

    it('persiste contagem no sessionStorage', () => {
      component.pickQuote();
      const stored = JSON.parse(sessionStorage.getItem(QUOTES_STORAGE_KEY)!);
      const total = Object.values(stored as Record<string, number>).reduce((s, v) => s + v, 0);
      expect(total).toBe(1);
    });
  });

  describe('seção de cadastro', () => {
    it('não deve existir elemento com routerLink="/register"', () => {
      const el = fixture.nativeElement.querySelector('[routerLink="/register"]');
      expect(el).toBeNull();
    });

    it('não deve exibir o texto "Cadastre-se"', () => {
      const text: string = fixture.nativeElement.textContent;
      expect(text).not.toContain('Cadastre-se');
    });

    it('não deve existir elemento com a classe login-register', () => {
      const el = fixture.nativeElement.querySelector('.login-register');
      expect(el).toBeNull();
    });
  });

  describe('links de recuperação de conta (#404)', () => {
    it('exibe o link "Esqueci minha senha" apontando para /forgot-password', () => {
      const link: HTMLAnchorElement | null = fixture.nativeElement.querySelector('a[href="/forgot-password"]');

      expect(link).not.toBeNull();
      expect(link!.textContent).toContain('Esqueci minha senha');
    });

    it('exibe o link "Confirmar e-mail" apontando para /confirm-email', () => {
      const link: HTMLAnchorElement | null = fixture.nativeElement.querySelector('a[href="/confirm-email"]');

      expect(link).not.toBeNull();
      expect(link!.textContent).toContain('Confirmar e-mail');
    });

    it('os links são estáticos: continuam visíveis mesmo com erro da API (sem comportamento condicional)', () => {
      component.apiError.set('Credenciais inválidas.');
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('a[href="/forgot-password"]')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('a[href="/confirm-email"]')).not.toBeNull();
    });

    it('não reintroduz cadastro: sem link /register nem classe login-register', () => {
      expect(fixture.nativeElement.querySelector('a[href="/register"]')).toBeNull();
      expect(fixture.nativeElement.querySelector('.login-register')).toBeNull();
    });
  });

  describe('links de recuperação no passo MFA (#405)', () => {
    it('ocultos quando mfaStep() é true', () => {
      component.mfaStep.set(true);
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('a[href="/forgot-password"]')).toBeNull();
      expect(fixture.nativeElement.querySelector('a[href="/confirm-email"]')).toBeNull();
    });

    it('voltam a aparecer quando mfaStep() volta a false', () => {
      component.mfaStep.set(true);
      fixture.detectChanges();
      component.mfaStep.set(false);
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('a[href="/forgot-password"]')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('a[href="/confirm-email"]')).not.toBeNull();
    });
  });

  describe('CSS morto', () => {
    it('estilos do componente não contêm mais .login-register', () => {
      const css = Array.from(document.head.querySelectorAll('style'))
        .map(s => s.textContent ?? '').join('\n');

      expect(css).not.toContain('login-register');
    });
  });

  describe('banner de sucesso (history.state.notice, #405)', () => {
    afterEach(() => history.replaceState(null, '', location.pathname + location.search));

    function createWithState(state: object | null): void {
      history.replaceState(state, '', location.pathname + location.search);
      fixture   = TestBed.createComponent(LoginComponent);
      component = fixture.componentInstance;
      fixture.detectChanges();
    }

    const banner = () => fixture.nativeElement.querySelector('[data-testid="login-notice"]');

    it('com state.notice: exibe o banner com a mensagem', () => {
      createWithState({ notice: 'Senha redefinida com sucesso.' });

      expect(banner()).not.toBeNull();
      expect(banner().textContent).toContain('Senha redefinida com sucesso.');
    });

    it('sem notice: não exibe banner', () => {
      createWithState(null);

      expect(banner()).toBeNull();
    });

    it('state sem notice (ex.: só email): não exibe banner', () => {
      createWithState({ email: 'a@x.com' });

      expect(banner()).toBeNull();
    });

    it('notice não-string é ignorado', () => {
      createWithState({ notice: 42 });

      expect(banner()).toBeNull();
    });

    it('state com email e notice: banner aparece sem conflito', () => {
      createWithState({ email: 'a@x.com', notice: 'E-mail confirmado.' });

      expect(banner().textContent).toContain('E-mail confirmado.');
    });
  });

  describe('showPassword signal', () => {
    it('começa como false', () => {
      expect(component.showPassword()).toBeFalse();
    });

    it('toggle atualiza o valor', () => {
      component.showPassword.update(v => !v);
      expect(component.showPassword()).toBeTrue();
    });
  });

  describe('MFA — 2º passo', () => {
    const mfaResponse = { token: null, name: 'N', email: 'a@b.com', mfaRequired: true, mfaToken: 'chal' };

    function submitCredentials(): void {
      authSpy.login.and.returnValue(of(mfaResponse));
      component.form.setValue({ email: 'a@b.com', password: '123456' });
      component.onSubmit();
    }

    function submitCode(code = '123456'): void {
      component.mfaForm.setValue({ code });
      component.onVerify();
    }

    it('mfaRequired: exibe 2º passo e não navega', fakeAsync(() => {
      submitCredentials();
      tick();
      fixture.detectChanges();

      expect(component.mfaStep()).toBeTrue();
      expect(router.navigate).not.toHaveBeenCalled();
      expect(fixture.nativeElement.querySelector('[data-testid="mfa-code"]')).not.toBeNull();
      expect(component.loading()).toBeFalse();
    }));

    it('sem MFA: não entra no 2º passo', fakeAsync(() => {
      authSpy.login.and.returnValue(of({ token: 'tok', name: 'N', email: 'a@b.com' }));
      component.form.setValue({ email: 'a@b.com', password: '123456' });
      component.onSubmit();
      tick();
      expect(component.mfaStep()).toBeFalse();
    }));

    it('verify OK: chama verifyMfa com o código e navega para /', fakeAsync(() => {
      submitCredentials();
      tick();
      authSpy.verifyMfa.and.returnValue(of({ token: 'full', name: 'N', email: 'a@b.com', mfaRequired: false, mfaToken: null }));

      submitCode('654321');
      tick();

      expect(authSpy.verifyMfa).toHaveBeenCalledOnceWith('654321');
      expect(router.navigate).toHaveBeenCalledWith(['/']);
    }));

    it('código vazio: não chama verifyMfa', fakeAsync(() => {
      submitCredentials();
      tick();
      submitCode('');
      expect(authSpy.verifyMfa).not.toHaveBeenCalled();
    }));

    it('400: mostra mensagem da API e mantém o passo', fakeAsync(() => {
      submitCredentials();
      tick();
      authSpy.verifyMfa.and.returnValue(throwError(() => ({ status: 400, error: { message: 'Código inválido.' } })));

      submitCode();
      tick();

      expect(component.apiError()).toBe('Código inválido.');
      expect(component.mfaStep()).toBeTrue();
      expect(component.loading()).toBeFalse();
      expect(router.navigate).not.toHaveBeenCalled();
    }));

    it('429: mostra mensagem da API', fakeAsync(() => {
      submitCredentials();
      tick();
      authSpy.verifyMfa.and.returnValue(throwError(() => ({ status: 429, error: { message: 'Muitas tentativas. Tente novamente em 60s.' } })));

      submitCode();
      tick();

      expect(component.apiError()).toBe('Muitas tentativas. Tente novamente em 60s.');
      expect(component.mfaStep()).toBeTrue();
    }));

    it('401 (challenge expirado): volta ao passo de credenciais e limpa challenge', fakeAsync(() => {
      submitCredentials();
      tick();
      authSpy.verifyMfa.and.returnValue(throwError(() => ({ status: 401, error: {} })));

      submitCode();
      tick();

      expect(component.mfaStep()).toBeFalse();
      expect(authSpy.clearMfaChallenge).toHaveBeenCalled();
      expect(component.apiError()).toBeTruthy();
    }));

    it('submit bloqueado durante loading (duplo submit chama verifyMfa uma vez)', fakeAsync(() => {
      submitCredentials();
      tick();
      const pending = new Subject<any>();
      authSpy.verifyMfa.and.returnValue(pending);

      submitCode();
      submitCode();

      expect(authSpy.verifyMfa).toHaveBeenCalledTimes(1);
      expect(component.loading()).toBeTrue();
      pending.complete();
    }));

    it('alternar recovery code muda o modo e permite código de recuperação', fakeAsync(() => {
      submitCredentials();
      tick();
      expect(component.useRecoveryCode()).toBeFalse();

      component.toggleRecoveryCode();
      expect(component.useRecoveryCode()).toBeTrue();

      authSpy.verifyMfa.and.returnValue(of({ token: 'full', name: 'N', email: 'a@b.com' }));
      submitCode('ABCD-EFGH');
      tick();
      expect(authSpy.verifyMfa).toHaveBeenCalledOnceWith('ABCD-EFGH');
    }));

    it('voltar: limpa challenge, sai do 2º passo e erro anterior', fakeAsync(() => {
      submitCredentials();
      tick();

      component.backToCredentials();

      expect(authSpy.clearMfaChallenge).toHaveBeenCalled();
      expect(component.mfaStep()).toBeFalse();
      expect(component.apiError()).toBeNull();
    }));
  });
});
