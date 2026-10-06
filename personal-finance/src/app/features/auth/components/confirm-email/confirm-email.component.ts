import { Component, computed, inject, OnDestroy, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../../../core/auth/auth.service';
import { readEmailHint } from '../../utils/read-email-hint';

const RESEND_COOLDOWN_SECONDS = 60;
const RESEND_MESSAGE = 'Se o e-mail estiver cadastrado e pendente de confirmação, enviaremos um novo código.';
const RATE_LIMIT_MESSAGE = 'Muitas tentativas. Aguarde um momento e tente novamente.';

@Component({
  selector: 'app-confirm-email',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './confirm-email.component.html',
  styleUrls: ['../auth-form.css']
})
export class ConfirmEmailComponent implements OnDestroy {
  private readonly auth   = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb     = inject(FormBuilder);

  readonly loading        = signal(false);
  readonly apiError       = signal<string | null>(null);
  readonly resendCooldown = signal(0);
  readonly resendMessage  = signal<string | null>(null);
  readonly canResend      = computed(() => this.resendCooldown() === 0);

  private cooldownTimer: ReturnType<typeof setInterval> | null = null;

  readonly form = this.fb.group({
    email: [readEmailHint(), [Validators.required, Validators.email]],
    code:  ['', [Validators.required, Validators.pattern(/^[0-9]{6}$/)]],
  });

  showError(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  onSubmit(): void {
    if (this.loading()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.apiError.set(null);

    const { email, code } = this.form.getRawValue();

    this.auth.confirmEmail({ email: email!, code: code! }).subscribe({
      // loading permanece true no sucesso: evita novo envio durante a navegação
      next: () => this.router.navigate(['/login']),
      error: (err) => {
        this.loading.set(false);
        this.apiError.set(err?.status === 429
          ? (err.error?.message ?? RATE_LIMIT_MESSAGE)
          : (err?.error?.message ?? 'Código inválido ou expirado.'));
      }
    });
  }

  resend(): void {
    if (!this.canResend()) return;
    const emailCtrl = this.form.get('email')!;
    if (emailCtrl.invalid) {
      emailCtrl.markAsTouched();
      return;
    }

    this.apiError.set(null);

    this.auth.resendConfirmation({ email: emailCtrl.value! }).subscribe({
      next: () => this.afterResend(),
      error: (err) => {
        if (err?.status === 429) {
          this.resendMessage.set(null);
          this.apiError.set(err.error?.message ?? RATE_LIMIT_MESSAGE);
          this.startCooldown();
          return;
        }
        // Anti-enumeração: mesma mensagem do sucesso
        this.afterResend();
      }
    });
  }

  ngOnDestroy(): void {
    this.stopTimer();
  }

  private afterResend(): void {
    this.resendMessage.set(RESEND_MESSAGE);
    this.startCooldown();
  }

  private startCooldown(): void {
    this.stopTimer();
    this.resendCooldown.set(RESEND_COOLDOWN_SECONDS);
    this.cooldownTimer = setInterval(() => {
      const next = this.resendCooldown() - 1;
      this.resendCooldown.set(Math.max(next, 0));
      if (next <= 0) this.stopTimer();
    }, 1000);
  }

  private stopTimer(): void {
    if (this.cooldownTimer !== null) {
      clearInterval(this.cooldownTimer);
      this.cooldownTimer = null;
    }
  }
}
