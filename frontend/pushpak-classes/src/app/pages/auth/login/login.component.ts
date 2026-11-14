import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule, RouterLink, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, RouterLink],
  template: `
    <div class="card shadow-lg border-0 rounded-4">
      <div class="card-body p-4 p-md-5">
        <div class="text-center mb-4">
          <div class="d-inline-flex align-items-center justify-content-center bg-primary-subtle text-primary rounded-circle mb-3" style="width: 56px; height: 56px;">
            <i class="bi bi-box-arrow-in-right fs-3"></i>
          </div>
          <h3 class="fw-bold text-dark">Welcome Back</h3>
          <p class="text-muted small">Sign in to your PushpakClasses account</p>
        </div>

        @if (errorMessage()) {
          <div class="alert alert-danger d-flex align-items-center gap-2 py-2" role="alert">
            <i class="bi bi-exclamation-octagon-fill"></i>
            <div>{{ errorMessage() }}</div>
          </div>
        }

        <form [formGroup]="loginForm" (ngSubmit)="onSubmit()">
          <!-- Email Input -->
          <div class="mb-3">
            <label for="email" class="form-label small fw-semibold">Email address</label>
            <div class="input-group">
              <span class="input-group-text bg-light border-end-0">
                <i class="bi bi-envelope text-muted"></i>
              </span>
              <input 
                type="email" 
                id="email" 
                formControlName="email" 
                class="form-control border-start-0 ps-0" 
                [class.is-invalid]="f['email'].touched && f['email'].invalid"
                placeholder="name@example.com" 
                autocomplete="username" />
            </div>
            @if (f['email'].touched && f['email'].invalid) {
              <div class="text-danger small mt-1">
                @if (f['email'].errors?.['required']) { Email is required. }
                @if (f['email'].errors?.['email']) { Please enter a valid email address. }
              </div>
            }
          </div>

          <!-- Password Input -->
          <div class="mb-3">
            <div class="d-flex justify-content-between align-items-center mb-1">
              <label for="password" class="form-label small fw-semibold mb-0">Password</label>
              <a routerLink="/auth/forgot-password" class="text-decoration-none small text-primary">Forgot password?</a>
            </div>
            <div class="input-group">
              <span class="input-group-text bg-light border-end-0">
                <i class="bi bi-lock text-muted"></i>
              </span>
              <input 
                [type]="showPassword ? 'text' : 'password'" 
                id="password" 
                formControlName="password" 
                class="form-control border-start-0 border-end-0 ps-0" 
                [class.is-invalid]="f['password'].touched && f['password'].invalid"
                placeholder="••••••••" 
                autocomplete="current-password" />
              <button 
                type="button" 
                class="btn btn-light border border-start-0" 
                (click)="showPassword = !showPassword">
                <i class="bi" [ngClass]="showPassword ? 'bi-eye-slash' : 'bi-eye'"></i>
              </button>
            </div>
            @if (f['password'].touched && f['password'].invalid) {
              <div class="text-danger small mt-1">Password is required.</div>
            }
          </div>

          <!-- Remember Me -->
          <div class="form-check mb-4">
            <input class="form-check-input" type="checkbox" id="rememberMe" formControlName="rememberMe" />
            <label class="form-check-label small text-muted" for="rememberMe">
              Remember me on this device
            </label>
          </div>

          <!-- Submit Button -->
          <button 
            type="submit" 
            class="btn btn-primary w-100 py-2 fw-semibold d-flex align-items-center justify-content-center gap-2" 
            [disabled]="loginForm.invalid || isSubmitting()">
            @if (isSubmitting()) {
              <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
              <span>Signing in...</span>
            } @else {
              <span>Sign In</span>
              <i class="bi bi-arrow-right"></i>
            }
          </button>
        </form>

        <div class="text-center mt-4 pt-2 border-top">
          <p class="text-muted small mb-0">
            Don't have an account? 
            <a routerLink="/auth/signup" class="text-primary fw-semibold text-decoration-none">Create account</a>
          </p>
        </div>
      </div>
    </div>
  `
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private route = inject(ActivatedRoute);

  loginForm: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
    rememberMe: [false]
  });

  isSubmitting = signal<boolean>(false);
  showPassword = false;
  errorMessage = signal<string | null>(null);

  get f() {
    return this.loginForm.controls;
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    const { email, password } = this.loginForm.value;
    const returnUrl = this.route.snapshot.queryParams['returnUrl'];

    this.authService.login({ email, password }).subscribe({
      next: (res) => {
        this.isSubmitting.set(false);
        this.toastService.showSuccess(`Welcome back, ${res.user.fullName}!`, 'Login Successful');
        this.authService.redirectByRole(returnUrl);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        if (err.status === 423) {
          this.errorMessage.set('Account is temporarily locked for 15 minutes due to multiple failed login attempts.');
        } else if (err.status === 401) {
          this.errorMessage.set('Invalid email or password.');
        } else {
          this.errorMessage.set(err.error?.message || 'Login failed. Please check your credentials.');
        }
      }
    });
  }
}
