import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="login-page">
      <!-- Painel esquerdo — decorativo -->
      <div class="login-aside">
        <div class="login-aside-inner">
          <div class="login-brand">
            <span class="login-brand-mark">₿</span>
            <span class="login-brand-name">Personal Finance</span>
          </div>
          <blockquote class="login-quote">
            "A economia é muito maior se eu não comprar."
          </blockquote>
          <p class="login-quote-author">— MonkeyBomb</p>
        </div>
      </div>

      <!-- Painel direito — formulário -->
      <div class="login-main">
        <div class="login-card">
          <div class="login-header">
            <h1>Entrar</h1>
            <p class="text-muted">Acesse sua conta para continuar</p>
          </div>

          <form [formGroup]="form" (ngSubmit)="onSubmit()" class="login-form">
            <!-- E-mail -->
            <div class="field">
              <label for="email" class="field-label">E-mail</label>
              <input
                id="email"
                type="email"
                formControlName="email"
                class="input"
                [class.error]="showError('email')"
                placeholder="seu@email.com"
                autocomplete="email"
              />
              @if (showError('email')) {
                <span class="field-error">
                  {{ getEmailError() }}
                </span>
              }
            </div>

            <!-- Senha -->
            <div class="field">
              <label for="password" class="field-label">
                Senha
              </label>
              <div class="input-group">
                <input
                  id="password"
                  [type]="showPassword() ? 'text' : 'password'"
                  formControlName="password"
                  class="input"
                  [class.error]="showError('password')"
                  placeholder="••••••••"
                  autocomplete="current-password"
                />
                <button
                  type="button"
                  class="input-suffix"
                  (click)="showPassword.update(v => !v)"
                  tabindex="-1"
                >
                  @if (showPassword()) {
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94"/>
                      <path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19"/>
                      <line x1="1" y1="1" x2="23" y2="23"/>
                    </svg>
                  } @else {
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/>
                      <circle cx="12" cy="12" r="3"/>
                    </svg>
                  }
                </button>
              </div>
              @if (showError('password')) {
                <span class="field-error">Senha é obrigatória</span>
              }
            </div>

            <!-- Erro da API -->
            @if (apiError()) {
              <div class="login-error">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <circle cx="12" cy="12" r="10"/>
                  <line x1="12" y1="8" x2="12" y2="12"/>
                  <line x1="12" y1="16" x2="12.01" y2="16"/>
                </svg>
                {{ apiError() }}
              </div>
            }

            <button
              type="submit"
              class="btn btn-primary btn-lg"
              style="width: 100%"
              [disabled]="loading()"
            >
              @if (loading()) {
                <span class="spinner"></span>
                Entrando...
              } @else {
                Entrar
              }
            </button>
          </form>

          <p class="login-register">
            Não tem conta?
            <a routerLink="/register">Cadastre-se</a>
          </p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-page {
      min-height: 100vh;
      display: grid;
      grid-template-columns: 1fr 1fr;
    }

    @media (max-width: 768px) {
      .login-page { grid-template-columns: 1fr; }
      .login-aside { display: none; }
    }

    /* ── Aside ── */
    .login-aside {
      background: var(--bg3);
      border-right: 1px solid var(--border);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 48px;
    }

    .login-aside-inner {
      max-width: 320px;
    }

    .login-brand {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 48px;
    }

    .login-brand-mark {
      width: 44px;
      height: 44px;
      background: var(--sage2);
      color: #fff;
      border-radius: var(--radius-lg);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 20px;
      font-weight: 700;
    }

    .login-brand-name {
      font-size: 1.125rem;
      font-weight: 600;
      color: var(--ink);
    }

    .login-quote {
      font-size: 1.25rem;
      font-weight: 500;
      color: var(--ink);
      line-height: 1.5;
      margin: 0 0 12px;
      font-style: italic;
      border-left: 3px solid var(--sage);
      padding-left: 16px;
    }

    .login-quote-author {
      color: var(--ink3);
      font-size: 0.875rem;
      padding-left: 19px;
    }

    /* ── Main ── */
    .login-main {
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 48px 24px;
      background: var(--bg);
    }

    .login-card {
      width: 100%;
      max-width: 400px;
    }

    .login-header {
      margin-bottom: 32px;
    }

    .login-header h1 {
      font-size: 1.75rem;
      margin-bottom: 6px;
    }

    /* ── Formulário ── */
    .login-form {
      display: flex;
      flex-direction: column;
      gap: 20px;
    }

    .field {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .field-label {
      font-size: 0.875rem;
      font-weight: 500;
      color: var(--ink2);
    }

    .field-error {
      font-size: 0.8125rem;
      color: var(--color-danger);
    }

    .input-group {
      position: relative;
    }

    .input-group .input {
      padding-right: 40px;
    }

    .input-suffix {
      position: absolute;
      right: 10px;
      top: 50%;
      transform: translateY(-50%);
      background: none;
      border: none;
      color: var(--ink3);
      cursor: pointer;
      padding: 4px;
      display: flex;
      align-items: center;
    }

    .input-suffix:hover { color: var(--ink); }

    .login-error {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 14px;
      background: var(--color-danger-bg);
      color: var(--color-danger);
      border-radius: var(--radius);
      font-size: 0.875rem;
    }

    .login-register {
      margin-top: 20px;
      text-align: center;
      font-size: 0.875rem;
      color: var(--ink3);
    }

    .spinner {
      width: 16px;
      height: 16px;
      border: 2px solid rgba(255,255,255,.3);
      border-top-color: #fff;
      border-radius: 50%;
      animation: spin .7s linear infinite;
      display: inline-block;
    }

    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class LoginComponent {
  private readonly auth   = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb     = inject(FormBuilder);

  readonly loading      = signal(false);
  readonly apiError     = signal<string | null>(null);
  readonly showPassword = signal(false);

  readonly form = this.fb.group({
    email:    ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  showError(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  getEmailError(): string {
    const ctrl = this.form.get('email');
    if (ctrl?.hasError('required')) return 'E-mail é obrigatório';
    if (ctrl?.hasError('email'))    return 'E-mail inválido';
    return '';
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.apiError.set(null);

    const { email, password } = this.form.getRawValue();

    this.auth.login({ email: email!, password: password! }).subscribe({
      next: () => this.router.navigate(['/']),
      error: (err) => {
        this.loading.set(false);
        this.apiError.set(err.error?.message ?? 'Credenciais inválidas.');
      }
    });
  }
}
