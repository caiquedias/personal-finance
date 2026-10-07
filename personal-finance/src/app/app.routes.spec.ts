import { authGuard } from './core/auth/auth.guard';
import { routes } from './app.routes';

describe('app routes — MFA', () => {
  const shell = routes.find(r => r.path === '' && r.children);

  it('rota account/security existe dentro do shell protegido por authGuard', () => {
    expect(shell?.canActivate).toContain(authGuard);
    expect(shell?.children?.some(c => c.path === 'account/security')).toBeTrue();
  });

  it('account/security carrega MfaSetupComponent via lazy load', async () => {
    const child = shell!.children!.find(c => c.path === 'account/security');
    expect(child).toBeDefined();
    const cmp: any = await (child!.loadComponent as () => Promise<any>)();
    // esbuild pode sufixar o nome da classe (ex.: MfaSetupComponent2) ao desambiguar no bundle de teste
    expect(cmp.name).toMatch(/^MfaSetupComponent\d*$/);
  });

  it('verify não tem rota própria (fica no /login público)', () => {
    expect(routes.some(r => r.path?.includes('verify'))).toBeFalse();
    expect(shell!.children!.some(c => c.path?.includes('verify'))).toBeFalse();
  });
});

describe('app routes — recuperação de conta (#404)', () => {
  const shell = routes.find(r => r.path === '' && r.children);
  const publicPaths = ['forgot-password', 'reset-password', 'confirm-email'] as const;
  const expectedComponents: Record<string, string> = {
    'forgot-password': 'ForgotPasswordComponent',
    'reset-password':  'ResetPasswordComponent',
    'confirm-email':   'ConfirmEmailComponent',
  };

  publicPaths.forEach(path => {
    describe(`rota /${path}`, () => {
      it('existe como rota pública de nível raiz, fora do shell protegido', () => {
        const route = routes.find(r => r.path === path);
        expect(route).toBeDefined();
        expect(shell!.children!.some(c => c.path === path)).toBeFalse();
      });

      it('não tem authGuard nem adminGuard (usuário deslogado precisa acessar)', () => {
        const route = routes.find(r => r.path === path);
        expect(route?.canActivate ?? []).toEqual([]);
        expect(route?.canMatch ?? []).toEqual([]);
      });

      it('carrega o componente certo via lazy load', async () => {
        const route = routes.find(r => r.path === path);
        expect(route?.loadComponent).toBeDefined();
        const cmp: any = await (route!.loadComponent as () => Promise<any>)();
        expect(cmp.name).toMatch(new RegExp(`^${expectedComponents[path]}\\d*$`));
      });
    });
  });

  it('nenhuma rota de nível raiz contém "verify" no path', () => {
    expect(routes.some(r => r.path?.includes('verify'))).toBeFalse();
  });

  it('o fallback ** continua sendo a última rota', () => {
    expect(routes[routes.length - 1].path).toBe('**');
  });

  it('as rotas públicas vêm antes do fallback **', () => {
    const fallbackIndex = routes.findIndex(r => r.path === '**');
    publicPaths.forEach(path => {
      const index = routes.findIndex(r => r.path === path);
      expect(index).toBeGreaterThanOrEqual(0);
      expect(index).toBeLessThan(fallbackIndex);
    });
  });
});
