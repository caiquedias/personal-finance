import { TestBed, ComponentFixture, fakeAsync, tick } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { AuthService } from '../../../../core/auth/auth.service';
import { QrCodeService } from '../../../../core/services/qr-code.service';
import { MfaSetupComponent } from './mfa-setup.component';

const SETUP = { secret: 'JBSWY3DPEHPK3PXP', otpAuthUri: 'otpauth://totp/PF:a@b.com?secret=JBSWY3DPEHPK3PXP' };

describe('MfaSetupComponent', () => {
  let fixture: ComponentFixture<MfaSetupComponent>;
  let component: MfaSetupComponent;
  let authSpy: any; // any: setupMfa/enableMfa ainda não existem em AuthService (Red)
  let qrSpy: jasmine.SpyObj<QrCodeService>;
  let router: Router;

  async function create(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [MfaSetupComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authSpy },
        { provide: QrCodeService, useValue: qrSpy },
      ],
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate');
    fixture   = TestBed.createComponent(MfaSetupComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  async function createAndSettle(): Promise<void> {
    await create();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  beforeEach(() => {
    authSpy = jasmine.createSpyObj('AuthService', ['setupMfa', 'enableMfa']);
    qrSpy   = jasmine.createSpyObj('QrCodeService', ['toDataUrl']);
    qrSpy.toDataUrl.and.resolveTo('data:image/png;base64,QR');
  });

  afterEach(() => TestBed.resetTestingModule());

  describe('etapa 1 — QR + secret', () => {
    it('chama setupMfa ao iniciar e renderiza QR gerado localmente a partir da otpAuthUri', async () => {
      authSpy.setupMfa.and.returnValue(of(SETUP));
      await createAndSettle();

      expect(authSpy.setupMfa).toHaveBeenCalledTimes(1);
      expect(qrSpy.toDataUrl).toHaveBeenCalledWith(SETUP.otpAuthUri);
      expect(component.step()).toBe('verify');
      const img: HTMLImageElement = fixture.nativeElement.querySelector('[data-testid="mfa-qr"]');
      expect(img.getAttribute('src')).toBe('data:image/png;base64,QR');
    });

    it('exibe o secret em texto', async () => {
      authSpy.setupMfa.and.returnValue(of(SETUP));
      await createAndSettle();

      expect(fixture.nativeElement.querySelector('[data-testid="mfa-secret"]').textContent)
        .toContain(SETUP.secret);
    });

    it('400 "já ativo": estado active com a mensagem da API, sem QR', async () => {
      authSpy.setupMfa.and.returnValue(throwError(() => ({
        status: 400, error: { message: 'O MFA já está ativo para este usuário.' } })));
      await createAndSettle();

      expect(component.step()).toBe('active');
      expect(component.error()).toBe('O MFA já está ativo para este usuário.');
      expect(qrSpy.toDataUrl).not.toHaveBeenCalled();
      expect(fixture.nativeElement.querySelector('[data-testid="mfa-qr"]')).toBeNull();
    });

    it('outro erro no setup: mostra mensagem de erro e permanece na etapa inicial', async () => {
      authSpy.setupMfa.and.returnValue(throwError(() => ({ status: 500, error: {} })));
      await createAndSettle();

      expect(component.step()).toBe('setup');
      expect(component.error()).toBeTruthy();
    });
  });

  describe('etapa 2 — confirmar código', () => {
    beforeEach(async () => {
      authSpy.setupMfa.and.returnValue(of(SETUP));
      await createAndSettle();
    });

    it('submit do formulário chama enableMfa e não dispara o submit nativo (recarga da página)', () => {
      authSpy.enableMfa.and.returnValue(new Subject());
      component.codeControl.setValue('123456');

      const form: HTMLFormElement = fixture.nativeElement.querySelector('form');
      const event = new Event('submit', { cancelable: true });
      form.dispatchEvent(event);

      expect(authSpy.enableMfa).toHaveBeenCalledOnceWith('123456');
      expect(event.defaultPrevented).toBeTrue();
    });

    it('código vazio: não chama enableMfa', () => {
      component.codeControl.setValue('');
      component.confirm();
      expect(authSpy.enableMfa).not.toHaveBeenCalled();
    });

    it('enable OK: avança para recovery codes e expõe os códigos', fakeAsync(() => {
      authSpy.enableMfa.and.returnValue(of({ recoveryCodes: ['AAAA-1111', 'BBBB-2222'] }));
      component.codeControl.setValue('123456');

      component.confirm();
      tick();
      fixture.detectChanges();

      expect(authSpy.enableMfa).toHaveBeenCalledOnceWith('123456');
      expect(component.step()).toBe('recovery');
      expect(component.recoveryCodes()).toEqual(['AAAA-1111', 'BBBB-2222']);
      const text = fixture.nativeElement.textContent;
      expect(text).toContain('AAAA-1111');
      expect(text).toContain('BBBB-2222');
    }));

    it('código errado: mostra erro da API e permanece na etapa para nova tentativa', fakeAsync(() => {
      authSpy.enableMfa.and.returnValue(throwError(() => ({
        status: 400, error: { message: 'Código inválido.' } })));
      component.codeControl.setValue('000000');

      component.confirm();
      tick();

      expect(component.step()).toBe('verify');
      expect(component.error()).toBe('Código inválido.');
      expect(component.loading()).toBeFalse();
      expect(component.recoveryCodes()).toEqual([]);
      expect(component.codeControl.value).toBe('000000');
    }));

    it('duplo submit durante loading chama enableMfa uma vez', () => {
      const pending = new Subject<any>();
      authSpy.enableMfa.and.returnValue(pending);
      component.codeControl.setValue('123456');

      component.confirm();
      component.confirm();

      expect(authSpy.enableMfa).toHaveBeenCalledTimes(1);
      expect(component.loading()).toBeTrue();
      pending.complete();
    });

    it('após sucesso, novo confirm não dispara outra chamada', fakeAsync(() => {
      authSpy.enableMfa.and.returnValue(of({ recoveryCodes: ['A'] }));
      component.codeControl.setValue('123456');

      component.confirm();
      tick();
      component.confirm();

      expect(authSpy.enableMfa).toHaveBeenCalledTimes(1);
    }));
  });

  describe('etapa 3 — recovery codes', () => {
    it('finish navega para /', async () => {
      authSpy.setupMfa.and.returnValue(of(SETUP));
      authSpy.enableMfa.and.returnValue(of({ recoveryCodes: ['A'] }));
      await createAndSettle();
      component.codeControl.setValue('123456');
      component.confirm();

      component.finish();

      expect(router.navigate).toHaveBeenCalledWith(['/']);
    });
  });
});
