import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators
} from '@angular/forms';
import { AuthService } from '../../../../core/auth/auth.service';
import { readEmailHint } from '../../utils/read-email-hint';

/** Confirmação deve ser igual à nova senha. */
function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const password = group.get('newPassword')?.value;
  const confirm  = group.get('confirmPassword')?.value;
  return password === confirm ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.component.html',
  styleUrls: ['../auth-form.css']
})
export class ResetPasswordComponent {
  private readonly auth   = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb     = inject(FormBuilder);

  readonly loading  = signal(false);
  readonly apiError = signal<string | null>(null);

  readonly form = this.fb.group({
    email:           [readEmailHint(), [Validators.required, Validators.email]],
    code:            ['', [Validators.required, Validators.pattern(/^[0-9]{6}$/)]],
    newPassword:     ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
    confirmPassword: ['', [Validators.required]],
  }, { validators: passwordsMatch });

  showError(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl?.touched);
  }

  showMismatch(): boolean {
    return this.form.hasError('passwordMismatch') && !!this.form.get('confirmPassword')?.touched;
  }

  onSubmit(): void {
    if (this.loading()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.apiError.set(null);

    const { email, code, newPassword } = this.form.getRawValue();

    this.auth.resetPassword({ email: email!, code: code!, newPassword: newPassword! }).subscribe({
      // loading permanece true no sucesso: evita novo envio durante a navegação
      next: () => this.router.navigate(['/login'], { state: { notice: 'Senha redefinida com sucesso. Entre com a nova senha.' } }),
      error: (err) => {
        this.loading.set(false);
        this.apiError.set(err?.status === 429
          ? (err.error?.message ?? 'Muitas tentativas. Aguarde um momento e tente novamente.')
          : (err?.error?.message ?? 'Código inválido ou expirado.'));
      }
    });
  }
}
