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
    expect(cmp.name).toBe('MfaSetupComponent');
  });

  it('verify não tem rota própria (fica no /login público)', () => {
    expect(routes.some(r => r.path?.includes('verify'))).toBeFalse();
    expect(shell!.children!.some(c => c.path?.includes('verify'))).toBeFalse();
  });
});
