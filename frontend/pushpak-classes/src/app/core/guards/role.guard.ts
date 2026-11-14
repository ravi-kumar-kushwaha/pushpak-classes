import { inject } from '@angular/core';
import { CanActivateFn, Router, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { UserRole } from '../models/user.models';

export const roleGuard = (allowedRoles: UserRole[]): CanActivateFn => {
  return (route: ActivatedRouteSnapshot, state: RouterStateSnapshot) => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (!authService.isAuthenticated()) {
      router.navigate(['/auth/login'], { queryParams: { returnUrl: state.url } });
      return false;
    }

    const user = authService.currentUser();
    if (user && allowedRoles.includes(user.role)) {
      return true;
    }

    // Role not authorized, redirect to their home
    if (user?.role === 'Admin') {
      router.navigate(['/admin/dashboard']);
    } else {
      router.navigate(['/user/dashboard']);
    }
    return false;
  };
};
