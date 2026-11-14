import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { ToastService } from '../services/toast.service';
import { LoadingService } from '../services/loading.service';
import { catchError, finalize, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn) => {
  const toastService = inject(ToastService);
  const loadingService = inject(LoadingService);

  const skipLoader = req.headers.has('X-Skip-Loader') ||
    req.headers.get('X-Skip-Loader') === 'true' ||
    req.url.endsWith('/save') ||
    req.url.endsWith('/tab-switch') ||
    req.url.includes('/server-time');

  let finalReq = req;
  if (req.headers.has('X-Skip-Loader')) {
    finalReq = req.clone({
      headers: req.headers.delete('X-Skip-Loader')
    });
  }

  // Show loading spinner for active requests (except background polling, autosave, tab-switch, server-time)
  if (!skipLoader) {
    loadingService.show();
  }

  return next(finalReq).pipe(
    finalize(() => {
      if (!skipLoader) {
        loadingService.hide();
      }
    }),
    catchError((error: HttpErrorResponse) => {
      let errorMessage = 'An unexpected error occurred. Please try again.';

      if (error.error) {
        if (typeof error.error === 'string') {
          errorMessage = error.error;
        } else if (error.error.message) {
          errorMessage = error.error.message;
        } else if (error.error.errors) {
          // ASP.NET Core ModelState validation errors
          if (Array.isArray(error.error.errors)) {
            errorMessage = error.error.errors.join(' ');
          } else if (typeof error.error.errors === 'object') {
            const errorList: string[] = [];
            for (const key of Object.keys(error.error.errors)) {
              const val = error.error.errors[key];
              if (Array.isArray(val)) {
                errorList.push(...val);
              } else {
                errorList.push(String(val));
              }
            }
            if (errorList.length > 0) {
              errorMessage = errorList.join(' ');
            }
          }
        }
      }

      // Handle specific status codes
      if (error.status === 423) {
        toastService.showError(
          errorMessage || 'Your account has been locked for 15 minutes due to multiple failed login attempts.',
          'Account Locked'
        );
      } else if (error.status === 403) {
        toastService.showError('You do not have permission to perform this action.', 'Access Denied');
      } else if (error.status === 401) {
        // Only toast if it's an explicit login failure, not background refresh token expiration
        if (req.url.includes('/api/auth/login')) {
          toastService.showError(errorMessage || 'Invalid email or password.', 'Login Failed');
        }
      } else if (error.status === 0) {
        toastService.showError('Unable to connect to the PushpakClasses server. Please check your connection.', 'Network Error');
      } else if (error.status >= 400) {
        toastService.showError(errorMessage, `Error (${error.status})`);
      }

      return throwError(() => error);
    })
  );
};

export const loaderInterceptor = errorInterceptor;
