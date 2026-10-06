import { ComponentFixture, TestBed, discardPeriodicTasks, fakeAsync, tick } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { ConfirmEmailComponent } from './confirm-email.component';

describe('ConfirmEmailComponent', () => {
  let fixture: ComponentFixture<ConfirmEmailComponent>;
  let component: ConfirmEmailComponent;
  let authSpy: any; // any: confirmEmail/resendConfirmation ainda não existem no AuthService (Red)
  let router: Router;

  /** Cria o componente depois de preparar fragmento/state (o e-mail é lido na inicialização). */
  async function create(opts: { hash?: string; state?: object | null } = {}): Promise<void> {
    history.replaceState(opts.state ?? null, '', location.pathname + location.search + (opts.hash ?? ''));

    authSpy = jasmine.createSpyObj('AuthService', ['confirmEmail', 'resendConfirmation']);
    await TestBed.configureTestingModule({
      imports: [ConfirmEmailComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authSpy },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate');

    fixture   = TestBed.createComponent(ConfirmEmailComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  const emailValue = () => component.form.get('email')!.value;

  afterEach(() => {
    history.replaceState(null, '', location.pathname + location.search);
  });

  it('cria o componente', async () => {
    await create();
    expect(component).toBeTruthy();
  });

  describe('origem do e-mail', () => {
    it('lê o e-mail do fragmento da URL (decodificado)', async () => {
      await create({ hash: '#email=a%2Bb%40x.com' });

      expect(emailValue()).toBe('a+b@x.com');
    });

    it('lê o e-mail do state da navegação quando não há fragmento', async () => {
      await create({ state: { email: 'state@x.com' } });

      expect(emailValue()).toBe('state@x.com');
    });

    it('sem fragmento nem state: e-mail vazio (usuário informa)', async () => {
      await create();

      expect(emailValue()).toBe('');
    });

    it('limpa o fragmento da URL com history.replaceState após lê-lo', async () => {
      const replaceSpy = spyOn(history, 'replaceState').and.callThrough();

      await create({ hash: '#email=ana%40x.com' });

      expect(replaceSpy).toHaveBeenCalled();
      expect(location.hash).toBe('');
      expect(emailValue()).toBe('ana@x.com');
    });
  });

  describe('confirmação', () => {
    beforeEach(async () => {
      await create({ state: { email: 'ana@x.com' } });
    });

    ['', '12345', '1234567', 'abcdef', '12 456'].forEach(code => {
      it(`código "${code}" (não é exatamente 6 dígitos) é inválido e não chama o serviço`, () => {
        component.form.patchValue({ code });

        component.onSubmit();

        expect(component.form.valid).toBeFalse();
        expect(authSpy.confirmEmail).not.toHaveBeenCalled();
      });
    });

    it('chama confirmEmail com {email, code}', () => {
      authSpy.confirmEmail.and.returnValue(of({ message: 'ok' }));
      component.form.patchValue({ code: '654321' });

      component.onSubmit();

      expect(authSpy.confirmEmail).toHaveBeenCalledOnceWith({ email: 'ana@x.com', code: '654321' });
    });

    it('sucesso: navega para /login', () => {
      authSpy.confirmEmail.and.returnValue(of({ message: 'ok' }));
      component.form.patchValue({ code: '654321' });

      component.onSubmit();

      expect((router.navigate as jasmine.Spy).calls.mostRecent().args[0]).toEqual(['/login']);
    });

    it('400: mostra a mensagem da API, não navega e mantém o código digitado', () => {
      authSpy.confirmEmail.and.returnValue(
        throwError(() => ({ status: 400, error: { message: 'Código inválido ou expirado.' } })));
      component.form.patchValue({ code: '000000' });

      component.onSubmit();

      expect(component.apiError()).toBe('Código inválido ou expirado.');
      expect(router.navigate).not.toHaveBeenCalled();
      expect(component.loading()).toBeFalse();
      expect(component.form.get('code')!.value).toBe('000000');
    });

    it('400 sem mensagem: mostra um texto padrão', () => {
      authSpy.confirmEmail.and.returnValue(throwError(() => ({ status: 400 })));
      component.form.patchValue({ code: '000000' });

      component.onSubmit();

      expect(component.apiError()).toBeTruthy();
    });

    it('bloqueia duplo submit enquanto a requisição está em andamento', () => {
      const pending = new Subject<any>();
      authSpy.confirmEmail.and.returnValue(pending);
      component.form.patchValue({ code: '654321' });

      component.onSubmit();
      component.onSubmit();

      expect(authSpy.confirmEmail).toHaveBeenCalledTimes(1);

      pending.next({ message: 'ok' });
      pending.complete();
    });

    it('depois do sucesso um segundo submit não dispara outra chamada', () => {
      authSpy.confirmEmail.and.returnValue(of({ message: 'ok' }));
      component.form.patchValue({ code: '654321' });

      component.onSubmit();
      component.onSubmit();

      expect(authSpy.confirmEmail).toHaveBeenCalledTimes(1);
    });
  });

  describe('reenvio do código', () => {
    beforeEach(async () => {
      await create({ state: { email: 'ana@x.com' } });
    });

    it('pode reenviar de início (sem cooldown)', () => {
      expect(component.canResend()).toBeTrue();
      expect(component.resendCooldown()).toBe(0);
    });

    it('resend chama resendConfirmation com {email} e inicia o cooldown de 60s', fakeAsync(() => {
      authSpy.resendConfirmation.and.returnValue(of({ message: 'ok' }));

      component.resend();

      expect(authSpy.resendConfirmation).toHaveBeenCalledOnceWith({ email: 'ana@x.com' });
      expect(component.resendCooldown()).toBe(60);
      expect(component.canResend()).toBeFalse();
      discardPeriodicTasks();
    }));

    it('cooldown regressivo: reenviar fica indisponível por 60s e volta a habilitar ao zerar', fakeAsync(() => {
      authSpy.resendConfirmation.and.returnValue(of({ message: 'ok' }));

      component.resend();
      tick(59000);
      expect(component.resendCooldown()).toBe(1);
      expect(component.canResend()).toBeFalse();

      tick(1000);
      expect(component.resendCooldown()).toBe(0);
      expect(component.canResend()).toBeTrue();
      discardPeriodicTasks();
    }));

    it('resend durante o cooldown é ignorado (não dispara outra chamada)', fakeAsync(() => {
      authSpy.resendConfirmation.and.returnValue(of({ message: 'ok' }));

      component.resend();
      tick(10000);
      component.resend();

      expect(authSpy.resendConfirmation).toHaveBeenCalledTimes(1);
      discardPeriodicTasks();
    }));

    it('depois do cooldown é possível reenviar de novo', fakeAsync(() => {
      authSpy.resendConfirmation.and.returnValue(of({ message: 'ok' }));

      component.resend();
      tick(60000);
      component.resend();

      expect(authSpy.resendConfirmation).toHaveBeenCalledTimes(2);
      discardPeriodicTasks();
    }));

    it('mensagem genérica idêntica em sucesso e em erro (não revela se o e-mail existe) e cooldown nos dois casos', fakeAsync(() => {
      authSpy.resendConfirmation.and.returnValue(of({ message: 'ok' }));
      component.resend();
      const onSuccess = component.resendMessage();
      expect(component.resendCooldown()).toBe(60);
      tick(60000);

      authSpy.resendConfirmation.and.returnValue(throwError(() => ({ status: 500 })));
      component.resend();
      const onError = component.resendMessage();

      expect(onSuccess).toBeTruthy();
      expect(onError).toBe(onSuccess);
      expect(component.resendCooldown()).toBe(60);
      discardPeriodicTasks();
    }));

    it('resend sem e-mail válido não chama o serviço', fakeAsync(() => {
      component.form.patchValue({ email: '' });

      component.resend();

      expect(authSpy.resendConfirmation).not.toHaveBeenCalled();
      expect(component.resendCooldown()).toBe(0);
    }));

    it('botão de reenviar fica desabilitado durante o cooldown', fakeAsync(() => {
      authSpy.resendConfirmation.and.returnValue(of({ message: 'ok' }));

      component.resend();
      fixture.detectChanges();

      const button: HTMLButtonElement = fixture.nativeElement.querySelector('[data-testid="resend-button"]');
      expect(button).not.toBeNull();
      expect(button.disabled).toBeTrue();
      discardPeriodicTasks();
    }));
  });

  describe('template', () => {
    beforeEach(async () => {
      await create({ state: { email: 'ana@x.com' } });
    });

    it('exibe o erro da API no DOM', () => {
      component.apiError.set('Código inválido ou expirado.');
      fixture.detectChanges();

      expect(fixture.nativeElement.textContent).toContain('Código inválido ou expirado.');
    });

    it('tem link de volta para o login', () => {
      expect(fixture.nativeElement.querySelector('a[href="/login"]')).not.toBeNull();
    });
  });
});
