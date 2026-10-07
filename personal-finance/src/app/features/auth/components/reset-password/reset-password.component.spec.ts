import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { ResetPasswordComponent } from './reset-password.component';

describe('ResetPasswordComponent', () => {
  let fixture: ComponentFixture<ResetPasswordComponent>;
  let component: ResetPasswordComponent;
  let authSpy: any; // any: resetPassword ainda não existe no AuthService (Red)
  let router: Router;

  const VALID = { code: '123456', newPassword: 'NovaSenha@456', confirmPassword: 'NovaSenha@456' };

  /** Cria o componente depois de preparar fragmento/state (o e-mail é lido na inicialização). */
  async function create(opts: { hash?: string; state?: object | null } = {}): Promise<void> {
    history.replaceState(opts.state ?? null, '', location.pathname + location.search + (opts.hash ?? ''));

    authSpy = jasmine.createSpyObj('AuthService', ['resetPassword']);
    await TestBed.configureTestingModule({
      imports: [ResetPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authSpy },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate');

    fixture   = TestBed.createComponent(ResetPasswordComponent);
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

    it('sem fragmento nem state: e-mail vazio e formulário inválido até informar', async () => {
      await create();

      expect(emailValue()).toBe('');
      component.form.patchValue(VALID);
      expect(component.form.valid).toBeFalse();
      component.form.patchValue({ email: 'ana@x.com' });
      expect(component.form.valid).toBeTrue();
    });

    it('limpa o fragmento da URL com history.replaceState após lê-lo (e-mail não fica no histórico)', async () => {
      const replaceSpy = spyOn(history, 'replaceState').and.callThrough();

      await create({ hash: '#email=ana%40x.com' });

      expect(replaceSpy).toHaveBeenCalled();
      const urls = replaceSpy.calls.allArgs().map(a => String(a[2] ?? ''));
      expect(urls.some(u => !u.includes('#'))).toBeTrue();
      expect(location.hash).toBe('');
      expect(emailValue()).toBe('ana@x.com');
    });
  });

  describe('validação', () => {
    beforeEach(async () => {
      await create({ state: { email: 'ana@x.com' } });
    });

    it('formulário completo e consistente é válido', () => {
      component.form.patchValue(VALID);

      expect(component.form.valid).toBeTrue();
    });

    ['', '12345', '1234567', 'abcdef', '12 456'].forEach(code => {
      it(`código "${code}" (não é exatamente 6 dígitos) é inválido`, () => {
        component.form.patchValue({ ...VALID, code });

        expect(component.form.valid).toBeFalse();
      });
    });

    it('senha com menos de 8 caracteres é inválida', () => {
      component.form.patchValue({ ...VALID, newPassword: 'curta12', confirmPassword: 'curta12' });

      expect(component.form.valid).toBeFalse();
    });

    it('senha com exatamente 8 caracteres é válida', () => {
      component.form.patchValue({ ...VALID, newPassword: '12345678', confirmPassword: '12345678' });

      expect(component.form.valid).toBeTrue();
    });

    it('confirmação divergente invalida o formulário', () => {
      component.form.patchValue({ ...VALID, confirmPassword: 'OutraSenha@789' });

      expect(component.form.valid).toBeFalse();
    });

    it('submit inválido não chama o serviço e marca os campos como tocados', () => {
      component.form.patchValue({ ...VALID, confirmPassword: 'diferente' });

      component.onSubmit();

      expect(authSpy.resetPassword).not.toHaveBeenCalled();
      expect(component.form.get('code')!.touched).toBeTrue();
      expect(router.navigate).not.toHaveBeenCalled();
    });
  });

  describe('envio', () => {
    beforeEach(async () => {
      await create({ state: { email: 'ana@x.com' } });
      component.form.patchValue(VALID);
    });

    it('chama resetPassword com {email, code, newPassword} (sem confirmPassword)', () => {
      authSpy.resetPassword.and.returnValue(of({ message: 'ok' }));

      component.onSubmit();

      expect(authSpy.resetPassword).toHaveBeenCalledOnceWith({
        email: 'ana@x.com', code: '123456', newPassword: 'NovaSenha@456'
      });
    });

    it('sucesso: navega para /login', () => {
      authSpy.resetPassword.and.returnValue(of({ message: 'ok' }));

      component.onSubmit();

      expect((router.navigate as jasmine.Spy).calls.mostRecent().args[0]).toEqual(['/login']);
    });

    it('sucesso: navega para /login com state.notice (banner de sucesso)', () => {
      authSpy.resetPassword.and.returnValue(of({ message: 'ok' }));

      component.onSubmit();

      const args = (router.navigate as jasmine.Spy).calls.mostRecent().args;
      expect(args[0]).toEqual(['/login']);
      expect(typeof args[1]?.state?.notice).toBe('string');
      expect(args[1].state.notice.length).toBeGreaterThan(0);
    });

    it('400: mostra a mensagem da API, não navega e mantém os dados para nova tentativa', () => {
      authSpy.resetPassword.and.returnValue(
        throwError(() => ({ status: 400, error: { message: 'Código inválido ou expirado.' } })));

      component.onSubmit();

      expect(component.apiError()).toBe('Código inválido ou expirado.');
      expect(router.navigate).not.toHaveBeenCalled();
      expect(component.loading()).toBeFalse();
      expect(component.form.getRawValue()).toEqual(jasmine.objectContaining(VALID));
    });

    it('400 sem mensagem: mostra um texto padrão', () => {
      authSpy.resetPassword.and.returnValue(throwError(() => ({ status: 400 })));

      component.onSubmit();

      expect(component.apiError()).toBeTruthy();
    });

    it('429: mostra o aviso de limite e não navega', () => {
      authSpy.resetPassword.and.returnValue(
        throwError(() => ({ status: 429, error: { message: 'Tente novamente em 20 segundos.' } })));

      component.onSubmit();

      expect(component.apiError()).toBeTruthy();
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('após erro é possível reenviar (nova chamada ao serviço) e o erro anterior é limpo', () => {
      authSpy.resetPassword.and.returnValues(
        throwError(() => ({ status: 400, error: { message: 'Código inválido ou expirado.' } })),
        of({ message: 'ok' }));

      component.onSubmit();
      expect(component.apiError()).toBeTruthy();
      component.onSubmit();

      expect(authSpy.resetPassword).toHaveBeenCalledTimes(2);
      expect(component.apiError()).toBeNull();
    });

    it('bloqueia duplo submit enquanto a requisição está em andamento', () => {
      const pending = new Subject<any>();
      authSpy.resetPassword.and.returnValue(pending);

      component.onSubmit();
      expect(component.loading()).toBeTrue();
      component.onSubmit();

      expect(authSpy.resetPassword).toHaveBeenCalledTimes(1);

      pending.next({ message: 'ok' });
      pending.complete();
    });

    it('depois do sucesso um segundo submit não dispara outra chamada', () => {
      authSpy.resetPassword.and.returnValue(of({ message: 'ok' }));

      component.onSubmit();
      component.onSubmit();

      expect(authSpy.resetPassword).toHaveBeenCalledTimes(1);
    });
  });

  describe('template', () => {
    beforeEach(async () => {
      await create({ state: { email: 'ana@x.com' } });
    });

    it('campo do código: inputmode numeric, maxlength 6 e autocomplete one-time-code', () => {
      const input: HTMLInputElement = fixture.nativeElement.querySelector('#code');

      expect(input.getAttribute('inputmode')).toBe('numeric');
      expect(input.getAttribute('maxlength')).toBe('6');
      expect(input.getAttribute('autocomplete')).toBe('one-time-code');
    });

    it('exibe o erro da API no DOM', () => {
      component.apiError.set('Código inválido ou expirado.');
      fixture.detectChanges();

      expect(fixture.nativeElement.textContent).toContain('Código inválido ou expirado.');
    });

    it('botão de enviar fica desabilitado enquanto carrega', () => {
      authSpy.resetPassword.and.returnValue(new Subject<any>());
      component.form.patchValue(VALID);

      component.onSubmit();
      fixture.detectChanges();

      const button: HTMLButtonElement = fixture.nativeElement.querySelector('button[type="submit"]');
      expect(button.disabled).toBeTrue();
    });

    it('tem link para pedir novo código e link de volta ao login', () => {
      expect(fixture.nativeElement.querySelector('a[href="/forgot-password"]')).not.toBeNull();
      expect(fixture.nativeElement.querySelector('a[href="/login"]')).not.toBeNull();
    });

    it('campos de senha usam type=password', () => {
      const passwordInputs = fixture.nativeElement.querySelectorAll('input[type="password"]');

      expect(passwordInputs.length).toBeGreaterThanOrEqual(2);
    });
  });
});
