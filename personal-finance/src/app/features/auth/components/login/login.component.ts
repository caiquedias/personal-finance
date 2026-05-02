import { Component, ElementRef, inject, OnDestroy, signal, ViewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { AuthService } from '../../../../core/auth/auth.service';

interface Coin {
  id: number;
  x: number;
  phase: 'flying' | 'slow' | 'medium' | 'fast';
}

const MAX_COINS = 10;

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnDestroy {
  private readonly auth   = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb     = inject(FormBuilder);

  @ViewChild('blockEl') private blockEl!: ElementRef<HTMLDivElement>;

  readonly loading      = signal(false);
  readonly apiError     = signal<string | null>(null);
  readonly showPassword = signal(false);
  readonly coins        = signal<Coin[]>([]);

  private clickCount    = 0;
  private coinId        = 0;
  private readonly timers: ReturnType<typeof setTimeout>[] = [];

  readonly form = this.fb.group({
    email:    ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  onBlockClick(): void {
    const el = this.blockEl.nativeElement;
    el.classList.remove('block-bounce');
    void el.offsetWidth;
    el.classList.add('block-bounce');

    if (this.clickCount >= MAX_COINS) return;
    this.clickCount++;

    const id = this.coinId++;
    const x  = Math.floor(Math.random() * 161) - 80;

    this.coins.update(c => [...c, { id, x, phase: 'flying' }]);

    const setPhase = (phase: Coin['phase'], delay: number) => {
      const t = setTimeout(() => {
        this.coins.update(c => c.map(coin => coin.id === id ? { ...coin, phase } : coin));
      }, delay);
      this.timers.push(t);
    };

    setPhase('slow',   1500);
    setPhase('medium', 4500);
    setPhase('fast',   7500);

    const t = setTimeout(() => {
      this.coins.update(c => c.filter(coin => coin.id !== id));
    }, 11500);
    this.timers.push(t);
  }

  ngOnDestroy(): void {
    this.timers.forEach(t => clearTimeout(t));
  }

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
