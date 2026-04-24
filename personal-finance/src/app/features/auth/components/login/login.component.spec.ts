import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ComponentFixture } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let authSpy: jasmine.SpyObj<AuthService>;
  let router: Router;

  beforeEach(async () => {
    authSpy = jasmine.createSpyObj('AuthService', ['login']);

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

  describe('showPassword signal', () => {
    it('começa como false', () => {
      expect(component.showPassword()).toBeFalse();
    });

    it('toggle atualiza o valor', () => {
      component.showPassword.update(v => !v);
      expect(component.showPassword()).toBeTrue();
    });
  });
});
