import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { LoginRequest, LoginResponse, RegisterRequest, UserResponse } from '../models/models';

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
          this._token.set(response.token);
          this._user.set({ name: response.name, email: response.email });
          localStorage.setItem(TOKEN_KEY, response.token);
          localStorage.setItem(USER_KEY, JSON.stringify({
            name: response.name, email: response.email
          }));
        })
      );
  }

  register(request: RegisterRequest) {
    return this.http
      .post<UserResponse>(`${environment.apiUrl}/auth/register`, request);
  }

  logout(): void {
    this._token.set(null);
    this._user.set(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.router.navigate(['/login']);
  }
}
