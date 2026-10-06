import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { ForgotPasswordComponent } from './forgot-password.component';

describe('ForgotPasswordComponent', () => {
  let fixture: ComponentFixture<ForgotPasswordComponent>;
  let component: ForgotPasswordComponent;
  let authSpy: any; // any: forgotPassword ainda não existe no AuthService (Red)
  let router: Router;

  beforeEach(async () => {
    authSpy = jasmine.createSpyObj('AuthService', ['forgotPassword']);

    await TestBed.configureTestingModule({
      imports: [ForgotPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authSpy },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate');

    fixture   = TestBed.createComponent(ForgotPasswordComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('cria o componente', () => {
    expect(component).toBeTruthy();
  });

  describe('validação do formulário', () => {
    it('e-mail vazio: não chama o serviço e marca os campos como tocados', () => {
      component.onSubmit();

      expect(authSpy.forgotPassword).not.toHaveBeenCalled();
      expect(component.form.get('email')!.touched).toBeTrue();
      expect(router.navigate).not.toHaveBeenCalled();
    });

    it('e-mail malformado: não chama o serviço', () => {
      component.form.patchValue({ email: 'nao-eh-email' });

      component.onSubmit();

      expect(authSpy.forgotPassword).not.toHaveBeenCalled();
    });
  });

  describe('envio', () => {
    beforeEach(() => component.form.patchValue({ email: 'ana@x.com' }));

    it('chama forgotPassword com apenas {email}', () => {
      authSpy.forgotPassword.and.returnValue(of({ message: 'ok' }));

      component.onSubmit();

      expect(authSpy.forgotPassword).toHaveBeenCalledOnceWith({ email: 'ana@x.com' });
    });

    it('sucesso: navega para /reset-password levando o e-mail no state', () => {
      authSpy.forgotPassword.and.returnValue(of({ message: 'ok' }));

      component.onSubmit();

      expect(router.navigate).toHaveBeenCalledWith(
        ['/reset-password'], jasmine.objectContaining({ state: { email: 'ana@x.com' } }));
    });

    [400, 404, 500].forEach(status => {
      it(`erro ${status}: mesmo comportamento do sucesso (navega com o e-mail) e não mostra erro — anti-enumeração`, () => {
        authSpy.forgotPassword.and.returnValue(
          throwError(() => ({ status, error: { message: 'detalhe do servidor' } })));

        component.onSubmit();

        expect(router.navigate).toHaveBeenCalledWith(
          ['/reset-password'], jasmine.objectContaining({ state: { email: 'ana@x.com' } }));
        expect(component.apiError()).toBeNull();
        expect(component.loading()).toBeFalse();
      });
    });

    it('429: mostra a mensagem de limite, não navega e libera novo envio', () => {
      authSpy.forgotPassword.and.returnValue(
        throwError(() => ({ status: 429, error: { message: 'Tente novamente em 30 segundos.' } })));

      component.onSubmit();

      expect(component.apiError()).toBeTruthy();
      expect(router.navigate).not.toHaveBeenCalled();
      expect(component.loading()).toBeFalse();
    });

    it('429: sem mensagem da API ainda mostra um texto padrão', () => {
      authSpy.forgotPassword.and.returnValue(throwError(() => ({ status: 429 })));

      component.onSubmit();

      expect(component.apiError()).toBeTruthy();
    });

    it('bloqueia duplo submit enquanto a requisição está em andamento', () => {
      const pending = new Subject<any>();
      authSpy.forgotPassword.and.returnValue(pending);

      component.onSubmit();
      expect(component.loading()).toBeTrue();
      component.onSubmit();

      expect(authSpy.forgotPassword).toHaveBeenCalledTimes(1);

      pending.next({ message: 'ok' });
      pending.complete();
    });

    it('limpa o erro anterior ao reenviar', () => {
      authSpy.forgotPassword.and.returnValues(
        throwError(() => ({ status: 429, error: { message: 'x' } })),
        of({ message: 'ok' }));

      component.onSubmit();
      expect(component.apiError()).toBeTruthy();
      component.onSubmit();

      expect(component.apiError()).toBeNull();
      expect(authSpy.forgotPassword).toHaveBeenCalledTimes(2);
    });
  });

  describe('template', () => {
    it('botão de enviar fica desabilitado enquanto carrega', () => {
      authSpy.forgotPassword.and.returnValue(new Subject<any>());
      component.form.patchValue({ email: 'ana@x.com' });

      component.onSubmit();
      fixture.detectChanges();

      const button: HTMLButtonElement = fixture.nativeElement.querySelector('button[type="submit"]');
      expect(button.disabled).toBeTrue();
    });

    it('tem link de volta para o login', () => {
      const link = fixture.nativeElement.querySelector('a[href="/login"]');
      expect(link).not.toBeNull();
    });

    it('exibe o erro de rate limit no DOM', () => {
      component.apiError.set('Tente novamente em 30 segundos.');
      fixture.detectChanges();

      expect(fixture.nativeElement.textContent).toContain('Tente novamente em 30 segundos.');
    });
  });
});
