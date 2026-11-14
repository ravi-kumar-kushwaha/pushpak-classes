import { TestBed } from '@angular/core/testing';
import { Router, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { authGuard } from './auth.guard';
import { roleGuard } from './role.guard';
import { AuthService } from '../services/auth.service';
import { signal } from '@angular/core';
import { UserDto } from '../models/user.models';

describe('Route Guards', () => {
  let routerSpy: { navigate: ReturnType<typeof vi.fn> };
  let mockAuthService: {
    isAuthenticated: ReturnType<typeof vi.fn>;
    currentUser: ReturnType<typeof vi.fn>;
  };

  const dummyRoute = {} as ActivatedRouteSnapshot;
  const dummyState = { url: '/user/dashboard' } as RouterStateSnapshot;

  beforeEach(() => {
    routerSpy = { navigate: vi.fn() };
    mockAuthService = {
      isAuthenticated: vi.fn(),
      currentUser: vi.fn()
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: routerSpy },
        { provide: AuthService, useValue: mockAuthService }
      ]
    });
  });

  describe('authGuard', () => {
    it('should allow access if authenticated', () => {
      mockAuthService.isAuthenticated.mockReturnValue(true);

      const result = TestBed.runInInjectionContext(() => authGuard(dummyRoute, dummyState));
      expect(result).toBe(true);
      expect(routerSpy.navigate).not.toHaveBeenCalled();
    });

    it('should block access and redirect to login if not authenticated', () => {
      mockAuthService.isAuthenticated.mockReturnValue(false);

      const result = TestBed.runInInjectionContext(() => authGuard(dummyRoute, dummyState));
      expect(result).toBe(false);
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/auth/login'], {
        queryParams: { returnUrl: '/user/dashboard' }
      });
    });
  });

  describe('roleGuard', () => {
    it('should allow access if user has required role', () => {
      mockAuthService.isAuthenticated.mockReturnValue(true);
      mockAuthService.currentUser.mockReturnValue({ role: 'Admin' } as UserDto);

      const guard = roleGuard(['Admin']);
      const result = TestBed.runInInjectionContext(() => guard(dummyRoute, dummyState));
      expect(result).toBe(true);
    });

    it('should block and redirect if user does not have required role', () => {
      mockAuthService.isAuthenticated.mockReturnValue(true);
      mockAuthService.currentUser.mockReturnValue({ role: 'User' } as UserDto);

      const guard = roleGuard(['Admin']);
      const result = TestBed.runInInjectionContext(() => guard(dummyRoute, dummyState));
      expect(result).toBe(false);
      expect(routerSpy.navigate).toHaveBeenCalledWith(['/user/dashboard']);
    });
  });
});
