import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject }                               from '@angular/core';
import { catchError, throwError }               from 'rxjs';
import { AuthService }                          from './auth.service';

/**
 * 1. Додає "Authorization: Bearer <token>" до всіх API-запитів.
 * 2. При отриманні 401 — автоматично викидає користувача (logout).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth  = inject(AuthService);
  const token = auth.getToken();

  const isAuthEndpoint =
    req.url.includes('/auth/login') ||
    req.url.includes('/auth/register');

  // Клонуємо запит з токеном (якщо є і не auth-endpoint)
  const authReq = (token && !isAuthEndpoint)
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authReq).pipe(
    catchError((err: HttpErrorResponse) => {
      // 401 Unauthorized → токен прострочений або невалідний → logout
      if (err.status === 401 && !isAuthEndpoint) {
        auth.logout();
      }
      return throwError(() => err);
    })
  );
};
