import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AuthResponseDto,
  AuthMessageResponseDto,
  LoginRequestDto,
  SignupRequestDto,
  RefreshTokenRequestDto,
  ForgotPasswordRequestDto,
  ResetPasswordRequestDto,
  VerifyEmailRequestDto,
  LogoutRequestDto
} from '../models/auth.models';
import { UserDto } from '../models/user.models';
import { TimeService } from './time.service';

const ACCESS_TOKEN_KEY = 'pushpak_access_token';
const REFRESH_TOKEN_KEY = 'pushpak_refresh_token';
const USER_KEY = 'pushpak_user';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private timeService = inject(TimeService);

  private apiUrl = `${environment.apiUrl}/auth`;

  // Reactive state signals
  currentUser = signal<UserDto | null>(null);
  accessToken = signal<string | null>(null);
  refreshToken = signal<string | null>(null);

  isAuthenticated = computed(() => !!this.currentUser() && !!this.accessToken());
  isAdmin = computed(() => this.currentUser()?.role === 'Admin');
  isUser = computed(() => this.currentUser()?.role === 'User');

  constructor() {
    this.initFromStorage();
  }

  /**
   * Initialize state from localStorage upon application startup.
   */
  private initFromStorage(): void {
    const token = localStorage.getItem(ACCESS_TOKEN_KEY);
    const refresh = localStorage.getItem(REFRESH_TOKEN_KEY);
    const userJson = localStorage.getItem(USER_KEY);

    if (token && refresh && userJson) {
      try {
        const user: UserDto = JSON.parse(userJson);
        this.accessToken.set(token);
        this.refreshToken.set(refresh);
        this.currentUser.set(user);
      } catch {
        this.clearStorage();
      }
    }
  }

  getStoredAccessToken(): string | null {
    return this.accessToken() || localStorage.getItem(ACCESS_TOKEN_KEY);
  }

  getStoredRefreshToken(): string | null {
    return this.refreshToken() || localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  login(request: LoginRequestDto): Observable<AuthResponseDto> {
    return this.http.post<AuthResponseDto>(`${this.apiUrl}/login`, request).pipe(
      tap(response => {
        this.handleAuthSuccess(response);
      })
    );
  }

  signup(request: SignupRequestDto): Observable<AuthMessageResponseDto> {
    return this.http.post<AuthMessageResponseDto>(`${this.apiUrl}/signup`, request);
  }

  verifyEmail(request: VerifyEmailRequestDto): Observable<AuthMessageResponseDto> {
    return this.http.post<AuthMessageResponseDto>(`${this.apiUrl}/verify-email`, request);
  }

  forgotPassword(request: ForgotPasswordRequestDto): Observable<AuthMessageResponseDto> {
    return this.http.post<AuthMessageResponseDto>(`${this.apiUrl}/forgot-password`, request);
  }

  resetPassword(request: ResetPasswordRequestDto): Observable<AuthMessageResponseDto> {
    return this.http.post<AuthMessageResponseDto>(`${this.apiUrl}/reset-password`, request);
  }

  refreshTokenCall(): Observable<AuthResponseDto> {
    const currentAccessToken = this.getStoredAccessToken() || '';
    const currentRefreshToken = this.getStoredRefreshToken() || '';

    const payload: RefreshTokenRequestDto = {
      accessToken: currentAccessToken,
      refreshToken: currentRefreshToken
    };

    return this.http.post<AuthResponseDto>(`${this.apiUrl}/refresh`, payload).pipe(
      tap(response => {
        this.handleAuthSuccess(response);
      }),
      catchError(err => {
        this.logout();
        return throwError(() => err);
      })
    );
  }

  logout(): void {
    const refresh = this.getStoredRefreshToken();
    if (refresh) {
      const payload: LogoutRequestDto = { refreshToken: refresh };
      this.http.post<AuthMessageResponseDto>(`${this.apiUrl}/logout`, payload).subscribe({
        error: () => {
          // Even if logout API fails or is offline, purge local session
        }
      });
    }

    this.clearStorage();
    this.router.navigate(['/auth/login']);
  }

  redirectByRole(returnUrl?: string): void {
    const user = this.currentUser();
    if (!user) {
      this.router.navigate(['/auth/login']);
      return;
    }

    if (returnUrl && returnUrl.length > 1 && !returnUrl.startsWith('/auth')) {
      if (user.role === 'Admin' && returnUrl.startsWith('/admin')) {
        this.router.navigateByUrl(returnUrl);
        return;
      }
      if (user.role === 'User' && returnUrl.startsWith('/user')) {
        this.router.navigateByUrl(returnUrl);
        return;
      }
    }

    if (user.role === 'Admin') {
      this.router.navigate(['/admin/dashboard']);
    } else {
      this.router.navigate(['/user/dashboard']);
    }
  }

  updateCurrentUser(partial: Partial<UserDto>): void {
    const current = this.currentUser();
    if (current) {
      const updated = { ...current, ...partial };
      this.currentUser.set(updated);
      localStorage.setItem(USER_KEY, JSON.stringify(updated));
    }
  }

  private handleAuthSuccess(response: AuthResponseDto): void {
    this.accessToken.set(response.accessToken);
    this.refreshToken.set(response.refreshToken);
    this.currentUser.set(response.user);

    localStorage.setItem(ACCESS_TOKEN_KEY, response.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, response.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
  }

  private clearStorage(): void {
    this.accessToken.set(null);
    this.refreshToken.set(null);
    this.currentUser.set(null);

    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }
}
