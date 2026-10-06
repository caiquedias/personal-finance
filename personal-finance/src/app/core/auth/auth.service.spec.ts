import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';

const TOKEN_KEY = 'pf_token';
const USER_KEY  = 'pf_user';

// Cria um JWT falso com o payload especificado
function makeJwt(payload: object): string {
  const encoded = btoa(JSON.stringify(payload));
  return `header.${encoded}.signature`;
}

const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let routerSpy: jasmine.SpyObj<Router>;

  function setup(): void {
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        AuthService,
        { provide: Router, useValue: routerSpy },
      ],
    });
    service  = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  }

  afterEach(() => {
    httpMock?.verify();
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  describe('inicialização sem dados no localStorage', () => {
    beforeEach(() => {
      localStorage.clear();
      setup();
    });

    it('isAuthenticated é false', () => {
      expect(service.isAuthenticated()).toBeFalse();
    });

    it('currentUser é null', () => {
      expect(service.currentUser()).toBeNull();
    });

    it('token é null', () => {
      expect(service.token()).toBeNull();
    });

    it('roles é array vazio', () => {
      expect(service.roles()).toEqual([]);
    });

    it('isAdmin é false', () => {
      expect(service.isAdmin()).toBeFalse();
    });
  });

  describe('inicialização com token no localStorage', () => {
    const adminJwt = makeJwt({ [ROLE_CLAIM]: 'Admin' });

    beforeEach(() => {
      localStorage.setItem(TOKEN_KEY, adminJwt);
      localStorage.setItem(USER_KEY, JSON.stringify({ name: 'Caique', email: 'c@test.com' }));
      setup();
    });

    it('isAuthenticated é true', () => {
      expect(service.isAuthenticated()).toBeTrue();
    });

    it('currentUser retorna dados do localStorage', () => {
      expect(service.currentUser()).toEqual({ name: 'Caique', email: 'c@test.com' });
    });
  });

  describe('roles — JWT parsing', () => {
    it('role como string simples', () => {
      localStorage.setItem(TOKEN_KEY, makeJwt({ [ROLE_CLAIM]: 'Admin' }));
      setup();
      expect(service.roles()).toEqual(['Admin']);
      expect(service.isAdmin()).toBeTrue();
    });

    it('role como array', () => {
      localStorage.setItem(TOKEN_KEY, makeJwt({ [ROLE_CLAIM]: ['Admin', 'User'] }));
      setup();
      expect(service.roles()).toContain('Admin');
      expect(service.roles()).toContain('User');
    });

    it('role ausente no payload retorna array vazio', () => {
      localStorage.setItem(TOKEN_KEY, makeJwt({ sub: '123' }));
      setup();
      expect(service.roles()).toEqual([]);
      expect(service.isAdmin()).toBeFalse();
    });

    it('JWT inválido (não decodificável) retorna array vazio', () => {
      localStorage.setItem(TOKEN_KEY, 'not.a.jwt');
      setup();
      expect(service.roles()).toEqual([]);
    });
  });

  describe('login()', () => {
    beforeEach(() => {
      localStorage.clear();
      setup();
    });

    it('atualiza signals e localStorage após resposta bem-sucedida', () => {
      const jwt = makeJwt({ [ROLE_CLAIM]: 'User' });
      let emitted = false;

      service.login({ email: 'a@b.com', password: '123' }).subscribe(() => {
        emitted = true;
        expect(service.isAuthenticated()).toBeTrue();
        expect(service.currentUser()).toEqual({ name: 'Teste', email: 'a@b.com' });
        expect(localStorage.getItem(TOKEN_KEY)).toBe(jwt);
      });

      const req = httpMock.expectOne(`https://localhost:51841/api/v1/auth/login`);
      expect(req.request.method).toBe('POST');
      req.flush({ token: jwt, name: 'Teste', email: 'a@b.com' });

      expect(emitted).toBeTrue();
    });
  });

  describe('logout()', () => {
    beforeEach(() => {
      localStorage.setItem(TOKEN_KEY, makeJwt({ [ROLE_CLAIM]: 'User' }));
      localStorage.setItem(USER_KEY, JSON.stringify({ name: 'X', email: 'x@x.com' }));
      setup();
    });

    it('limpa signals', () => {
      service.logout();
      expect(service.isAuthenticated()).toBeFalse();
      expect(service.currentUser()).toBeNull();
    });

    it('remove itens do localStorage', () => {
      service.logout();
      expect(localStorage.getItem(TOKEN_KEY)).toBeNull();
      expect(localStorage.getItem(USER_KEY)).toBeNull();
    });

    it('navega para /login', () => {
      service.logout();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
    });
  });

  describe('register()', () => {
    beforeEach(() => {
      localStorage.clear();
      setup();
    });

    it('faz POST para /auth/register', () => {
      service.register({ name: 'N', email: 'e@e.com', password: 'p' }).subscribe();
      const req = httpMock.expectOne(`https://localhost:51841/api/v1/auth/register`);
      expect(req.request.method).toBe('POST');
      req.flush({ id: '1', name: 'N', email: 'e@e.com', isActive: true });
    });
  });

  // Nota Red: métodos MFA ainda não existem em AuthService — acessados via `any`
  describe('login() com MFA', () => {
    const URL = 'https://localhost:51841/api/v1/auth';

    beforeEach(() => {
      localStorage.clear();
      setup();
    });

    it('mfaRequired=true: não persiste token/usuário, não autentica e guarda challenge em memória', () => {
      service.login({ email: 'a@b.com', password: '123' }).subscribe();
      httpMock.expectOne(`${URL}/login`).flush(
        { token: null, name: 'Teste', email: 'a@b.com', mfaRequired: true, mfaToken: 'chal' });

      expect(service.isAuthenticated()).toBeFalse();
      expect(service.currentUser()).toBeNull();
      expect(localStorage.getItem(TOKEN_KEY)).toBeNull();
      expect(localStorage.getItem(USER_KEY)).toBeNull();
      expect((service as any).mfaChallenge()).toBe('chal');
      // challenge nunca vai para o localStorage
      expect(Object.keys(localStorage).some(k => localStorage.getItem(k) === 'chal')).toBeFalse();
    });

    it('sem MFA: token persistido e nenhum challenge pendente', () => {
      const jwt = makeJwt({ [ROLE_CLAIM]: 'User' });
      service.login({ email: 'a@b.com', password: '123' }).subscribe();
      httpMock.expectOne(`${URL}/login`).flush(
        { token: jwt, name: 'Teste', email: 'a@b.com', mfaRequired: false, mfaToken: null });

      expect(localStorage.getItem(TOKEN_KEY)).toBe(jwt);
      expect((service as any).mfaChallenge()).toBeNull();
    });

    it('verifyMfa: POST /auth/mfa/verify com Bearer do challenge e body {code}', () => {
      service.login({ email: 'a@b.com', password: '123' }).subscribe();
      httpMock.expectOne(`${URL}/login`).flush(
        { token: null, name: 'Teste', email: 'a@b.com', mfaRequired: true, mfaToken: 'chal' });

      (service as any).verifyMfa('123456').subscribe();

      const req = httpMock.expectOne(`${URL}/mfa/verify`);
      expect(req.request.method).toBe('POST');
      expect(req.request.headers.get('Authorization')).toBe('Bearer chal');
      expect(req.request.body).toEqual({ code: '123456' });
      req.flush({ token: 'x', name: 'Teste', email: 'a@b.com', mfaRequired: false, mfaToken: null });
    });

    it('verifyMfa 200: persiste token completo + usuário e limpa challenge', () => {
      const jwt = makeJwt({ [ROLE_CLAIM]: 'User' });
      service.login({ email: 'a@b.com', password: '123' }).subscribe();
      httpMock.expectOne(`${URL}/login`).flush(
        { token: null, name: 'Teste', email: 'a@b.com', mfaRequired: true, mfaToken: 'chal' });

      (service as any).verifyMfa('123456').subscribe();
      httpMock.expectOne(`${URL}/mfa/verify`).flush(
        { token: jwt, name: 'Teste', email: 'a@b.com', mfaRequired: false, mfaToken: null });

      expect(service.isAuthenticated()).toBeTrue();
      expect(localStorage.getItem(TOKEN_KEY)).toBe(jwt);
      expect(service.currentUser()).toEqual({ name: 'Teste', email: 'a@b.com' });
      expect((service as any).mfaChallenge()).toBeNull();
    });

    it('verifyMfa 400: mantém challenge para nova tentativa e não autentica', () => {
      service.login({ email: 'a@b.com', password: '123' }).subscribe();
      httpMock.expectOne(`${URL}/login`).flush(
        { token: null, name: 'Teste', email: 'a@b.com', mfaRequired: true, mfaToken: 'chal' });

      (service as any).verifyMfa('000000').subscribe({ error: () => {} });
      httpMock.expectOne(`${URL}/mfa/verify`).flush(
        { message: 'Código inválido.' }, { status: 400, statusText: 'Bad Request' });

      expect((service as any).mfaChallenge()).toBe('chal');
      expect(service.isAuthenticated()).toBeFalse();
    });

    it('clearMfaChallenge: zera o challenge', () => {
      service.login({ email: 'a@b.com', password: '123' }).subscribe();
      httpMock.expectOne(`${URL}/login`).flush(
        { token: null, name: 'Teste', email: 'a@b.com', mfaRequired: true, mfaToken: 'chal' });

      (service as any).clearMfaChallenge();

      expect((service as any).mfaChallenge()).toBeNull();
    });

    it('logout: limpa challenge pendente', () => {
      service.login({ email: 'a@b.com', password: '123' }).subscribe();
      httpMock.expectOne(`${URL}/login`).flush(
        { token: null, name: 'Teste', email: 'a@b.com', mfaRequired: true, mfaToken: 'chal' });

      service.logout();

      expect((service as any).mfaChallenge()).toBeNull();
    });

    it('setupMfa: POST /auth/mfa/setup sem body e retorna secret/otpAuthUri', () => {
      let result: any;
      (service as any).setupMfa().subscribe((r: any) => result = r);

      const req = httpMock.expectOne(`${URL}/mfa/setup`);
      expect(req.request.method).toBe('POST');
      req.flush({ secret: 'ABC', otpAuthUri: 'otpauth://totp/x?secret=ABC' });

      expect(result).toEqual({ secret: 'ABC', otpAuthUri: 'otpauth://totp/x?secret=ABC' });
    });

    it('enableMfa: POST /auth/mfa/enable com {code} e retorna recoveryCodes', () => {
      let result: any;
      (service as any).enableMfa('123456').subscribe((r: any) => result = r);

      const req = httpMock.expectOne(`${URL}/mfa/enable`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ code: '123456' });
      req.flush({ recoveryCodes: ['A', 'B'] });

      expect(result).toEqual({ recoveryCodes: ['A', 'B'] });
    });
  });

  // ── Recuperação de conta (#404) — endpoints anônimos, sem tocar na sessão ──────────
  describe('recuperação de conta', () => {
    const BASE = 'https://localhost:51841/api/v1/auth';

    beforeEach(() => {
      localStorage.clear();
      setup();
    });

    function expectSessionUntouched(): void {
      expect(service.isAuthenticated()).toBeFalse();
      expect(service.currentUser()).toBeNull();
      expect(localStorage.getItem(TOKEN_KEY)).toBeNull();
      expect(localStorage.getItem(USER_KEY)).toBeNull();
      expect(routerSpy.navigate).not.toHaveBeenCalled();
    }

    it('forgotPassword: POST /auth/password/forgot com {email} e devolve a resposta genérica', () => {
      let result: any;
      (service as any).forgotPassword({ email: 'a@b.com' }).subscribe((r: any) => result = r);

      const req = httpMock.expectOne(`${BASE}/password/forgot`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ email: 'a@b.com' });
      req.flush({ message: 'Se o e-mail existir, enviamos um código.' });

      expect(result).toEqual({ message: 'Se o e-mail existir, enviamos um código.' });
      expectSessionUntouched();
    });

    it('resetPassword: POST /auth/password/reset com {email, code, newPassword}', () => {
      let emitted = false;
      (service as any)
        .resetPassword({ email: 'a@b.com', code: '123456', newPassword: 'NovaSenha@456' })
        .subscribe(() => emitted = true);

      const req = httpMock.expectOne(`${BASE}/password/reset`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ email: 'a@b.com', code: '123456', newPassword: 'NovaSenha@456' });
      req.flush({ message: 'Senha redefinida.' });

      expect(emitted).toBeTrue();
      expectSessionUntouched();
    });

    it('resetPassword: 400 propaga o erro sem criar sessão nem navegar', () => {
      let status = 0;
      (service as any)
        .resetPassword({ email: 'a@b.com', code: '000000', newPassword: 'NovaSenha@456' })
        .subscribe({ error: (e: any) => status = e.status });

      httpMock.expectOne(`${BASE}/password/reset`)
        .flush({ message: 'Código inválido ou expirado.' }, { status: 400, statusText: 'Bad Request' });

      expect(status).toBe(400);
      expectSessionUntouched();
    });

    it('confirmEmail: POST /auth/email/confirm com {email, code}', () => {
      let emitted = false;
      (service as any).confirmEmail({ email: 'a@b.com', code: '654321' }).subscribe(() => emitted = true);

      const req = httpMock.expectOne(`${BASE}/email/confirm`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ email: 'a@b.com', code: '654321' });
      req.flush({ message: 'E-mail confirmado.' });

      expect(emitted).toBeTrue();
      expectSessionUntouched();
    });

    it('resendConfirmation: POST /auth/email/resend com {email}', () => {
      let emitted = false;
      (service as any).resendConfirmation({ email: 'a@b.com' }).subscribe(() => emitted = true);

      const req = httpMock.expectOne(`${BASE}/email/resend`);
      expect(req.request.method).toBe('POST');
      expect(req.request.body).toEqual({ email: 'a@b.com' });
      req.flush({ message: 'ok' });

      expect(emitted).toBeTrue();
      expectSessionUntouched();
    });

    it('não envia Authorization manual nos endpoints de recuperação (são anônimos)', () => {
      (service as any).forgotPassword({ email: 'a@b.com' }).subscribe();

      const req = httpMock.expectOne(`${BASE}/password/forgot`);
      expect(req.request.headers.has('Authorization')).toBeFalse();
      req.flush({});
    });
  });
});
