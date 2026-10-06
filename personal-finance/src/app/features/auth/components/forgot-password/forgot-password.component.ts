import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../../../core/auth/auth.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.component.html',
  styleUrls: ['../auth-form.css']
})
export class ForgotPasswordComponent {
  private readonly auth   = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb     = inject(FormBuilder);

  readonly loading  = signal(false);
  readonly apiError = signal<string | null>(null);

  readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
  });

  showError(): boolean {
    const ctrl = this.form.get('email');
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  getEmailError(): string {
    const ctrl = this.form.get('email');
    if (ctrl?.hasError('required')) return 'E-mail é obrigatório';
    if (ctrl?.hasError('email'))    return 'E-mail inválido';
    return '';
  }

  onSubmit(): void {
    if (this.loading()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.apiError.set(null);

    const email = this.form.getRawValue().email!;

    this.auth.forgotPassword({ email }).subscribe({
      next: () => this.goToReset(email),
      error: (err) => {
        if (err?.status === 429) {
          this.loading.set(false);
          this.apiError.set(err.error?.message ?? 'Muitas tentativas. Aguarde um momento e tente novamente.');
          return;
        }
        // Anti-enumeração: qualquer outro erro se comporta como sucesso
        this.goToReset(email);
      }
    });
  }

  private goToReset(email: string): void {
    this.loading.set(false);
    this.router.navigate(['/reset-password'], { state: { email } });
  }
}
