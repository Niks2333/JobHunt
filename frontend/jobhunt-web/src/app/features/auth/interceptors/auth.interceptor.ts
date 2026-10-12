import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/')) return next(request);
  if (request.url.startsWith('/api/auth/')) {
    // /me is the only auth endpoint requiring a bearer token.
    if (request.url !== '/api/auth/me')
      return next(
        request.clone({
          withCredentials: true,
          setHeaders: { 'X-JobHunt-CSRF': '1' },
        }),
      );
  }
  const auth = inject(AuthService);
  const router = inject(Router);
  return from(auth.ensureToken()).pipe(
    switchMap((token) =>
      next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })).pipe(
        catchError((error) => {
          if (!(error instanceof HttpErrorResponse) || error.status !== 401)
            return throwError(() => error);
          return from(auth.ensureToken(true, token)).pipe(
            switchMap((replacement) =>
              next(request.clone({ setHeaders: { Authorization: `Bearer ${replacement}` } })),
            ),
          );
        }),
      ),
    ),
    catchError((error) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        auth.clear();
        void router.navigate(['/login'], { queryParams: { returnUrl: router.url, expired: '1' } });
      }
      return throwError(() => error);
    }),
  );
};
