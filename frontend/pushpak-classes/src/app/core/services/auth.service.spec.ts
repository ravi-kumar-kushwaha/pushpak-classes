import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { TimeService } from './time.service';
import { AuthResponseDto } from '../models/auth.models';
import { environment } from '../../../environments/environment';

describe('AuthService', () => {
  let service: AuthService;
  let httpTesting: HttpTestingController;
  let routerSpy: { navigate: ReturnType<typeof vi.fn>; navigateByUrl: ReturnType<typeof vi.fn> };

  const mockUser = {
    id: 'user-123',
    fullName: 'Test User',
    email: 'test@pushpakclasses.com',
    role: 'User' as const,
    isActive: true,
    isEmailVerified: true,
    createdAtUtc: new Date().toISOString()
  };

  const mockAdmin = {
    id: 'admin-123',
    fullName: 'Admin User',
    email: 'admin@example.com',
    role: 'Admin' as const,
    isActive: true,
    isEmailVerified: true,
    createdAtUtc: new Date().toISOString()
  };

  const mockAuthResponse: AuthResponseDto = {
    accessToken: 'test-jwt-access-token',
    refreshToken: 'test-jwt-refresh-token',
    accessTokenExpiresAtUtc: new Date(Date.now() + 900000).toISOString(),
    user: mockUser
  };

  beforeEach(() => {
    localStorage.clear();
    routerSpy = {
      navigate: vi.fn(),
      navigateByUrl: vi.fn()
    };

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        TimeService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: routerSpy }
      ]
    });

    service = TestBed.inject(AuthService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    localStorage.clear();
  });

  it('should be created with initial unauthenticated state', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.currentUser()).toBeNull();
  });

  it('should log in and update reactive signals and storage', () => {
    service.login({ email: 'test@pushpakclasses.com', password: 'Password@123' }).subscribe(res => {
      expect(res.accessToken).toBe('test-jwt-access-token');
      expect(service.isAuthenticated()).toBe(true);
      expect(service.isUser()).toBe(true);
      expect(service.isAdmin()).toBe(false);
      expect(service.currentUser()?.email).toBe('test@pushpakclasses.com');
      expect(localStorage.getItem('pushpak_access_token')).toBe('test-jwt-access-token');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/auth/login`);
    expect(req.request.method).toBe('POST');
    req.flush(mockAuthResponse);
  });

  it('should logout and clear signals, storage, and navigate to login', () => {
    // Prime state
    service.currentUser.set(mockUser);
    service.accessToken.set('token');
    service.refreshToken.set('refresh');
    localStorage.setItem('pushpak_access_token', 'token');
    localStorage.setItem('pushpak_refresh_token', 'refresh');

    service.logout();

    const req = httpTesting.expectOne(`${environment.apiUrl}/auth/logout`);
    expect(req.request.method).toBe('POST');
    req.flush({ message: 'Logged out successfully' });

    expect(service.isAuthenticated()).toBe(false);
    expect(service.currentUser()).toBeNull();
    expect(localStorage.getItem('pushpak_access_token')).toBeNull();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/auth/login']);
  });

  it('should redirect by role: Admin to /admin/dashboard and User to /user/dashboard', () => {
    service.currentUser.set(mockAdmin);
    service.redirectByRole();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/admin/dashboard']);

    service.currentUser.set(mockUser);
    service.redirectByRole();
    expect(routerSpy.navigate).toHaveBeenCalledWith(['/user/dashboard']);
  });
});
