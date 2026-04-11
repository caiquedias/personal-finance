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
});
