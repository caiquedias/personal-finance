import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

// Casa somente a API própria, com checagem de fronteira (evita host que só compartilha prefixo)
const isApiUrl = (url: string): boolean =>
  url === environment.apiUrl || url.startsWith(environment.apiUrl + '/');

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isApiUrl(req.url)) return next(req);

  const auth  = inject(AuthService);
  const token = auth.token();

  // Não sobrescreve Authorization explícito (ex.: verify com challenge MFA)
  const request = token && !req.headers.has('Authorization')
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      // Token expirado ou inválido na API própria — faz logout automático
      if (error.status === 401) auth.logout();
      return throwError(() => error);
    })
  );
};
