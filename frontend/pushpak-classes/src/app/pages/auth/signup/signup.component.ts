import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { RouterModule, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

// Custom password validator enforcing backend policy
function passwordPolicyValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (!value) return null;

  const hasUpperCase = /[A-Z]/.test(value);
  const hasLowerCase = /[a-z]/.test(value);
  const hasNumeric = /[0-9]/.test(value);
  const hasSpecial = /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(value);
  const isLengthValid = value.length >= 8;

  const valid = hasUpperCase && hasLowerCase && hasNumeric && hasSpecial && isLengthValid;
  return !valid ? { passwordPolicy: true } : null;
}

// Custom validator to confirm passwords match
function passwordMatchValidator(group: AbstractControl): ValidationErrors | null {
  const password = group.get('password')?.value;
  const confirmPassword = group.get('confirmPassword')?.value;
  return password === confirmPassword ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-signup',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, RouterLink],
  template: `
    <div class="card shadow-lg border-0 rounded-4">
      <div class="card-body p-4 p-md-5">
        @if (registrationSuccess()) {
          <div class="text-center py-4">
            <div class="d-inline-flex align-items-center justify-content-center bg-success-subtle text-success rounded-circle mb-3" style="width: 64px; height: 64px;">
              <i class="bi bi-envelope-check-fill fs-2"></i>
            </div>
            <h4 class="fw-bold text-dark">Check Your Email</h4>
            <p class="text-muted small mb-4">
              We have sent a verification link to <strong>{{ registeredEmail() }}</strong>. 
              Please verify your email address to activate your PushpakClasses account.
            </p>
            <a routerLink="/auth/login" class="btn btn-primary px-4 py-2">
              Proceed to Login
            </a>
          </div>
        } @else {
          <div class="text-center mb-4">
            <div class="d-inline-flex align-items-center justify-content-center bg-primary-subtle text-primary rounded-circle mb-3" style="width: 56px; height: 56px;">
              <i class="bi bi-person-plus-fill fs-3"></i>
            </div>
            <h3 class="fw-bold text-dark">Create Account</h3>
            <p class="text-muted small">Join PushpakClasses to take timed tests</p>
          </div>

          @if (errorMessage()) {
            <div class="alert alert-danger d-flex align-items-center gap-2 py-2" role="alert">
              <i class="bi bi-exclamation-octagon-fill"></i>
              <div>{{ errorMessage() }}</div>
            </div>
          }

          <form [formGroup]="signupForm" (ngSubmit)="onSubmit()">
            <!-- Full Name -->
            <div class="mb-3">
              <label for="fullName" class="form-label small fw-semibold">Full Name</label>
              <div class="input-group">
                <span class="input-group-text bg-light border-end-0">
                  <i class="bi bi-person text-muted"></i>
                </span>
                <input 
                  type="text" 
                  id="fullName" 
                  formControlName="fullName" 
                  class="form-control border-start-0 ps-0" 
                  [class.is-invalid]="f['fullName'].touched && f['fullName'].invalid"
                  placeholder="John Doe" />
              </div>
              @if (f['fullName'].touched && f['fullName'].invalid) {
                <div class="text-danger small mt-1">
                  @if (f['fullName'].errors?.['required']) { Full name is required. }
                  @if (f['fullName'].errors?.['minlength']) { Full name must be at least 2 characters. }
                </div>
              }
            </div>

            <!-- Email -->
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
                  placeholder="name@example.com" />
              </div>
              @if (f['email'].touched && f['email'].invalid) {
                <div class="text-danger small mt-1">
                  @if (f['email'].errors?.['required']) { Email is required. }
                  @if (f['email'].errors?.['email']) { Please enter a valid email address. }
                </div>
              }
            </div>

            <!-- Phone Number (Optional) -->
            <div class="mb-3">
              <label for="phoneNumber" class="form-label small fw-semibold">Phone Number <span class="text-muted fw-normal">(Optional)</span></label>
              <div class="input-group">
                <span class="input-group-text bg-light border-end-0">
                  <i class="bi bi-telephone text-muted"></i>
                </span>
                <input 
                  type="tel" 
                  id="phoneNumber" 
                  formControlName="phoneNumber" 
                  class="form-control border-start-0 ps-0" 
                  placeholder="+91 9876543210" />
              </div>
            </div>

            <!-- Password -->
            <div class="mb-3">
              <label for="password" class="form-label small fw-semibold">Password</label>
              <div class="input-group">
                <span class="input-group-text bg-light border-end-0">
                  <i class="bi bi-lock text-muted"></i>
                </span>
                <input 
                  [type]="showPassword() ? 'text' : 'password'" 
                  id="password" 
                  formControlName="password" 
                  class="form-control border-start-0 border-end-0 ps-0" 
                  [class.is-invalid]="f['password'].touched && f['password'].invalid"
                  placeholder="Min 8 chars with upper, lower, digit & special" />
                <button 
                  type="button" 
                  class="btn btn-light border border-start-0" 
                  (click)="showPassword.set(!showPassword())">
                  <i class="bi" [ngClass]="showPassword() ? 'bi-eye-slash' : 'bi-eye'"></i>
                </button>
              </div>
              @if (f['password'].touched && f['password'].invalid) {
                <div class="text-danger small mt-1">
                  @if (f['password'].errors?.['required']) { Password is required. }
                  @if (f['password'].errors?.['passwordPolicy']) { Password must be at least 8 characters and contain uppercase, lowercase, digit, and special character. }
                </div>
              }
            </div>

            <!-- Confirm Password -->
            <div class="mb-4">
              <label for="confirmPassword" class="form-label small fw-semibold">Confirm Password</label>
              <div class="input-group">
                <span class="input-group-text bg-light border-end-0">
                  <i class="bi bi-shield-lock text-muted"></i>
                </span>
                <input 
                  [type]="showPassword() ? 'text' : 'password'" 
                  id="confirmPassword" 
                  formControlName="confirmPassword" 
                  class="form-control border-start-0 ps-0" 
                  [class.is-invalid]="(f['confirmPassword'].touched && f['confirmPassword'].invalid) || (signupForm.errors?.['passwordMismatch'] && f['confirmPassword'].touched)"
                  placeholder="Re-enter your password" />
              </div>
              @if (f['confirmPassword'].touched && (f['confirmPassword'].invalid || signupForm.errors?.['passwordMismatch'])) {
                <div class="text-danger small mt-1">
                  @if (f['confirmPassword'].errors?.['required']) { Confirm password is required. }
                  @else if (signupForm.errors?.['passwordMismatch']) { Passwords do not match. }
                </div>
              }
            </div>

            <!-- Submit Button -->
            <button 
              type="submit" 
              class="btn btn-primary w-100 py-2 fw-semibold d-flex align-items-center justify-content-center gap-2" 
              [disabled]="signupForm.invalid || isSubmitting()">
              @if (isSubmitting()) {
                <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
                <span>Creating account...</span>
              } @else {
                <span>Register</span>
                <i class="bi bi-arrow-right"></i>
              }
            </button>
          </form>

          <div class="text-center mt-4 pt-2 border-top">
            <p class="text-muted small mb-0">
              Already have an account? 
              <a routerLink="/auth/login" class="text-primary fw-semibold text-decoration-none">Sign In</a>
            </p>
          </div>
        }
      </div>
    </div>
  `
})
export class SignupComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);

  signupForm: FormGroup = this.fb.group({
    fullName: ['', [Validators.required, Validators.minLength(2)]],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: [''],
    password: ['', [Validators.required, passwordPolicyValidator]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: passwordMatchValidator });

  isSubmitting = signal(false);
  showPassword = signal(false);
  errorMessage = signal<string | null>(null);
  registrationSuccess = signal(false);
  registeredEmail = signal('');

  get f() {
    return this.signupForm.controls;
  }

  onSubmit(): void {
    if (this.signupForm.invalid) {
      this.signupForm.markAllAsTouched();
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    const { fullName, email, phoneNumber, password, confirmPassword } = this.signupForm.value;

    this.authService.signup({
      fullName,
      email,
      phoneNumber: phoneNumber ? phoneNumber.trim() : undefined,
      password,
      confirmPassword: confirmPassword || password
    }).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.registeredEmail.set(email);
        this.registrationSuccess.set(true);
        this.toastService.showSuccess('Registration successful! Please check your email to activate your account.', 'Account Created');
      },
      error: (err) => {
        this.isSubmitting.set(false);
        if (err.status === 409) {
          this.errorMessage.set(err.error?.detail || err.error?.message || 'An account with this email address already exists. Please sign in or use a different email.');
        } else {
          this.errorMessage.set(err.error?.detail || err.error?.message || (err.error?.errors ? Object.values(err.error.errors).flat().join(' ') : 'Registration failed. Please try again.'));
        }
      }
    });
  }
}
