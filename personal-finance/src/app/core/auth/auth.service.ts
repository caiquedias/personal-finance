import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
  ConfirmEmailRequest, EnableMfaResponse, ForgotPasswordRequest, GenericMessageResponse,
  LoginRequest, LoginResponse, MfaSetupResponse, MfaVerifyRequest,
  PasswordResetConfirmRequest, RegisterRequest, ResendConfirmationRequest
} from '../models/models';

const TOKEN_KEY = 'pf_token';
const USER_KEY  = 'pf_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http   = inject(HttpClient);
  private readonly router = inject(Router);

  // ── Signals ───────────────────────────────────────────────────────────────
  private readonly _token = signal<string | null>(
    localStorage.getItem(TOKEN_KEY));

  private readonly _user = signal<{ name: string; email: string } | null>(
    JSON.parse(localStorage.getItem(USER_KEY) ?? 'null'));

  // Challenge MFA pendente (somente memória)
  private readonly _mfaChallenge = signal<string | null>(null);
  readonly mfaChallenge = this._mfaChallenge.asReadonly();

  readonly isAuthenticated = computed(() => !!this._token());
  readonly currentUser     = computed(() => this._user());
  readonly token           = computed(() => this._token());

  // ── Computed: roles extraídas do JWT ─────────────────────────────────────
  readonly roles = computed<string[]>(() => {
    const token = this._token();
    if (!token) return [];
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      // ASP.NET Core pode enviar como array ou string única
      const role = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
      return Array.isArray(role) ? role : role ? [role] : [];
    } catch {
      return [];
    }
  });

  readonly isAdmin = computed(() => this.roles().includes('Admin'));

  // ── Métodos ───────────────────────────────────────────────────────────────

  login(request: LoginRequest) {
    return this.http
      .post<LoginResponse>(`${environment.apiUrl}/auth/login`, request)
      .pipe(
        tap(response => {
          if (response.mfaRequired) {
            // Challenge fica só em memória — nunca persistido
            this._mfaChallenge.set(response.mfaToken ?? null);
            return;
          }
          this.persistSession(response);
        })
      );
  }

  verifyMfa(code: string) {
    const challenge = this._mfaChallenge();
    const headers = challenge ? { Authorization: `Bearer ${challenge}` } : undefined;
    return this.http
      .post<LoginResponse>(`${environment.apiUrl}/auth/mfa/verify`, { code } as MfaVerifyRequest, { headers })
      .pipe(tap(response => this.persistSession(response)));
  }

  setupMfa() {
    return this.http.post<MfaSetupResponse>(`${environment.apiUrl}/auth/mfa/setup`, null);
  }

  enableMfa(code: string) {
    return this.http.post<EnableMfaResponse>(
      `${environment.apiUrl}/auth/mfa/enable`, { code } as MfaVerifyRequest);
  }

  clearMfaChallenge(): void {
    this._mfaChallenge.set(null);
  }

  private persistSession(response: LoginResponse): void {
    if (!response.token) return;
    this._token.set(response.token);
    this._user.set({ name: response.name, email: response.email });
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(USER_KEY, JSON.stringify({
      name: response.name, email: response.email
    }));
    this._mfaChallenge.set(null);
  }

  register(request: RegisterRequest) {
    return this.http
      .post<GenericMessageResponse>(`${environment.apiUrl}/auth/register`, request);
  }

  // ── Recuperação de conta (endpoints anônimos; não tocam na sessão) ────────
  forgotPassword(request: ForgotPasswordRequest) {
    return this.http.post<GenericMessageResponse>(
      `${environment.apiUrl}/auth/password/forgot`, request);
  }

  resetPassword(request: PasswordResetConfirmRequest) {
    return this.http.post<GenericMessageResponse>(
      `${environment.apiUrl}/auth/password/reset`, request);
  }

  confirmEmail(request: ConfirmEmailRequest) {
    return this.http.post<GenericMessageResponse>(
      `${environment.apiUrl}/auth/email/confirm`, request);
  }

  resendConfirmation(request: ResendConfirmationRequest) {
    return this.http.post<GenericMessageResponse>(
      `${environment.apiUrl}/auth/email/resend`, request);
  }

  logout(): void {
    this._token.set(null);
    this._user.set(null);
    this._mfaChallenge.set(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.router.navigate(['/login']);
  }
}
