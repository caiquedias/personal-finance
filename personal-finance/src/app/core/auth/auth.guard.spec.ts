import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, RouterStateSnapshot, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { authGuard, adminGuard } from './auth.guard';

describe('Guards de autenticação', () => {
  let authSpy: jasmine.SpyObj<AuthService>;
  let routerSpy: jasmine.SpyObj<Router>;

  const dummyRoute = {} as ActivatedRouteSnapshot;
  const dummyState = {} as RouterStateSnapshot;

  beforeEach(() => {
    authSpy   = jasmine.createSpyObj('AuthService', [], {
      isAuthenticated: jasmine.createSpy().and.returnValue(false),
      isAdmin:         jasmine.createSpy().and.returnValue(false),
    });
    routerSpy = jasmine.createSpyObj('Router', ['navigate']);

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authSpy },
        { provide: Router,      useValue: routerSpy },
      ],
    });
  });

  afterEach(() => TestBed.resetTestingModule());

  describe('authGuard', () => {
    it('retorna true quando autenticado', () => {
      (authSpy.isAuthenticated as jasmine.Spy).and.returnValue(true);

      const result = TestBed.runInInjectionContext(() =>
        authGuard(dummyRoute, dummyState)
      );

      expect(result).toBeTrue();
      expect(routerSpy.navigate).not.toHaveBeenCalled();
    });

    it('retorna false e redireciona para /login quando não autenticado', () => {
      (authSpy.isAuthenticated as jasmine.Spy).and.returnValue(false);

      const result = TestBed.runInInjectionContext(() =>
        authGuard(dummyRoute, dummyState)
      );

      expect(result).toBeFalse();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/login']);
    });
  });

  describe('adminGuard', () => {
    it('retorna true quando autenticado e admin', () => {
      (authSpy.isAuthenticated as jasmine.Spy).and.returnValue(true);
      (authSpy.isAdmin         as jasmine.Spy).and.returnValue(true);

      const result = TestBed.runInInjectionContext(() =>
        adminGuard(dummyRoute, dummyState)
      );

      expect(result).toBeTrue();
      expect(routerSpy.navigate).not.toHaveBeenCalled();
    });

    it('retorna false e redireciona para / quando autenticado mas não admin', () => {
      (authSpy.isAuthenticated as jasmine.Spy).and.returnValue(true);
      (authSpy.isAdmin         as jasmine.Spy).and.returnValue(false);

      const result = TestBed.runInInjectionContext(() =>
        adminGuard(dummyRoute, dummyState)
      );

      expect(result).toBeFalse();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/']);
    });

    it('retorna false e redireciona para / quando não autenticado', () => {
      (authSpy.isAuthenticated as jasmine.Spy).and.returnValue(false);
      (authSpy.isAdmin         as jasmine.Spy).and.returnValue(false);

      const result = TestBed.runInInjectionContext(() =>
        adminGuard(dummyRoute, dummyState)
      );

      expect(result).toBeFalse();
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/']);
    });
  });
});
