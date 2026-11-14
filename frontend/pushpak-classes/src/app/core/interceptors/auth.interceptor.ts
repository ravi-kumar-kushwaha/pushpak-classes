import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { TimeService } from '../services/time.service';
import { catchError, switchMap, filter, take, throwError, BehaviorSubject, tap } from 'rxjs';

let isRefreshing = false;
const refreshTokenSubject = new BehaviorSubject<string | null>(null);

export const authInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn) => {
  const authService = inject(AuthService);
  const timeService = inject(TimeService);

  // Synchronize server time from HTTP response Date header
  const syncServerTime = (res: any) => {
    if (res && res.headers) {
      const serverDate = res.headers.get('Date');
      if (serverDate) {
        timeService.syncWithServer(serverDate);
      }
    }
  };

  const token = authService.getStoredAccessToken();
  let authReq = req;

  // Add Bearer token to requests targeting API if token is present
  if (token && !req.headers.has('Authorization')) {
    authReq = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`
      }
    });
  }

  return next(authReq).pipe(
    tap({
      next: (event) => syncServerTime(event)
    }),
    catchError((error: HttpErrorResponse) => {
      // Avoid looping if auth endpoints themselves return 401
      const isAuthEndpoint = req.url.includes('/api/auth/login') ||
                             req.url.includes('/api/auth/refresh') ||
                             req.url.includes('/api/auth/signup') ||
                             req.url.includes('/api/auth/forgot-password');

      if (error.status === 401 && !isAuthEndpoint) {
        if (!isRefreshing) {
          isRefreshing = true;
          refreshTokenSubject.next(null);

          return authService.refreshTokenCall().pipe(
            switchMap(authRes => {
              isRefreshing = false;
              refreshTokenSubject.next(authRes.accessToken);
              return next(
                req.clone({
                  setHeaders: {
                    Authorization: `Bearer ${authRes.accessToken}`
                  }
                })
              );
            }),
            catchError(refreshErr => {
              isRefreshing = false;
              authService.logout();
              return throwError(() => refreshErr);
            })
          );
        } else {
          // Wait until token refresh completes and retry
          return refreshTokenSubject.pipe(
            filter(newToken => newToken !== null),
            take(1),
            switchMap(newToken => {
              return next(
                req.clone({
                  setHeaders: {
                    Authorization: `Bearer ${newToken}`
                  }
                })
              );
            })
          );
        }
      }

      return throwError(() => error);
    })
  );
};
