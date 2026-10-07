import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { AuthService } from '../../../../core/auth/auth.service';
import { QrCodeService } from '../../../../core/services/qr-code.service';

type MfaSetupStep = 'setup' | 'verify' | 'recovery' | 'active';

@Component({
  selector: 'app-mfa-setup',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <div class="mfa-page">
      <h1 class="mfa-title">Segurança — verificação em duas etapas</h1>

      @if (error() && step() !== 'verify') {
        <div class="mfa-error">{{ error() }}</div>
      }

      @switch (step()) {
        @case ('setup') {
          <p class="mfa-text">Preparando a configuração...</p>
        }
        @case ('verify') {
          <div class="card mfa-card">
            <p class="mfa-text">
              1. Escaneie o QR code com seu aplicativo autenticador.<br />
              2. Informe abaixo o código de 6 dígitos gerado.
            </p>
            @if (qrDataUrl()) {
              <img class="mfa-qr" data-testid="mfa-qr" [src]="qrDataUrl()" alt="QR code para configurar o MFA" />
            }
            <p class="mfa-text">Ou digite a chave manualmente:</p>
            <code class="mfa-secret" data-testid="mfa-secret">{{ secret() }}</code>

            <form class="mfa-form" (submit)="$event.preventDefault(); confirm()">
              <label for="mfa-setup-code" class="mfa-label">Código</label>
              <input
                id="mfa-setup-code"
                type="text"
                class="input"
                inputmode="numeric"
                autocomplete="one-time-code"
                placeholder="000000"
                [formControl]="codeControl"
              />
              @if (error()) {
                <div class="mfa-error">{{ error() }}</div>
              }
              <button type="submit" class="btn btn-primary" [disabled]="loading()">
                @if (loading()) { Confirmando... } @else { Ativar MFA }
              </button>
            </form>
          </div>
        }
        @case ('recovery') {
          <div class="card mfa-card">
            <p class="mfa-text">
              MFA ativado. Guarde os códigos de recuperação abaixo em local seguro.
              Eles são exibidos <strong>uma única vez</strong>.
            </p>
            <ul class="mfa-codes">
              @for (code of recoveryCodes(); track code) {
                <li><code>{{ code }}</code></li>
              }
            </ul>
            <button type="button" class="btn btn-primary" (click)="finish()">
              Guardei os códigos
            </button>
          </div>
        }
        @case ('active') {
          <div class="card mfa-card">
            <p class="mfa-text">A verificação em duas etapas já está ativa na sua conta.</p>
          </div>
        }
      }
    </div>
  `,
  styles: [`
    .mfa-page { max-width: 480px; display: flex; flex-direction: column; gap: 16px; }
    .mfa-title { font-size: 1.25rem; color: var(--ink); }
    .mfa-card { display: flex; flex-direction: column; gap: 12px; padding: 20px; }
    .mfa-text { color: var(--ink2); font-size: 0.875rem; }
    .mfa-qr { width: 220px; height: 220px; align-self: center; border-radius: var(--radius); }
    .mfa-secret {
      padding: 8px 12px; background: var(--bg2); color: var(--ink);
      border-radius: var(--radius-sm); word-break: break-all;
    }
    .mfa-form { display: flex; flex-direction: column; gap: 8px; }
    .mfa-label { font-size: 0.875rem; color: var(--ink2); }
    .mfa-error {
      padding: 10px 14px; background: var(--color-danger-bg); color: var(--color-danger);
      border-radius: var(--radius); font-size: 0.875rem;
    }
    .mfa-codes {
      list-style: none; padding: 12px; margin: 0; display: grid;
      grid-template-columns: 1fr 1fr; gap: 8px; background: var(--bg2);
      border-radius: var(--radius); color: var(--ink);
    }
  `]
})
export class MfaSetupComponent implements OnInit {
  private readonly auth   = inject(AuthService);
  private readonly qr     = inject(QrCodeService);
  private readonly router = inject(Router);

  readonly step          = signal<MfaSetupStep>('setup');
  readonly error         = signal<string | null>(null);
  readonly loading       = signal(false);
  readonly recoveryCodes = signal<string[]>([]);
  readonly secret        = signal('');
  readonly qrDataUrl     = signal('');

  readonly codeControl = new FormControl('', { nonNullable: true });

  ngOnInit(): void {
    this.auth.setupMfa().subscribe({
      next: async (res) => {
        this.secret.set(res.secret);
        try {
          this.qrDataUrl.set(await this.qr.toDataUrl(res.otpAuthUri));
        } catch {
          this.error.set('Não foi possível gerar o QR code. Use a chave manual.');
        }
        this.step.set('verify');
      },
      error: (err) => {
        this.error.set(err.error?.message ?? 'Não foi possível iniciar a configuração do MFA.');
        if (err.status === 400) this.step.set('active');
      }
    });
  }

  confirm(): void {
    const code = this.codeControl.value.trim();
    if (!code || this.loading() || this.step() !== 'verify') return;

    this.loading.set(true);
    this.error.set(null);

    this.auth.enableMfa(code).subscribe({
      next: (res) => {
        this.recoveryCodes.set(res.recoveryCodes);
        this.loading.set(false);
        this.step.set('recovery');
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.message ?? 'Não foi possível ativar o MFA.');
      }
    });
  }

  finish(): void {
    this.router.navigate(['/']);
  }
}
