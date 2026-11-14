import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { ActivatedRoute, RouterModule, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

function passwordPolicyValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (!value) return null;

  const hasUpperCase = /[A-Z]/.test(value);
  const hasLowerCase = /[a-z]/.test(value);
  const hasNumeric = /[0-9]/.test(value);
  const hasSpecial = /[^a-zA-Z0-9]/.test(value);
  const isLengthValid = value.length >= 8;

  const valid = hasUpperCase && hasLowerCase && hasNumeric && hasSpecial && isLengthValid;
  return !valid ? { passwordPolicy: true } : null;
}

function passwordMatchValidator(group: AbstractControl): ValidationErrors | null {
  const password = group.get('newPassword')?.value;
  const confirmPassword = group.get('confirmPassword')?.value;
  return password === confirmPassword ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, RouterLink],
  template: `
    <div class="card shadow-lg border-0 rounded-4">
      <div class="card-body p-4 p-md-5">
        @if (resetSuccess) {
          <div class="text-center py-4">
            <div class="d-inline-flex align-items-center justify-content-center bg-success-subtle text-success rounded-circle mb-3" style="width: 64px; height: 64px;">
              <i class="bi bi-shield-check fs-2"></i>
            </div>
            <h4 class="fw-bold text-dark">Password Reset Complete</h4>
            <p class="text-muted small mb-4">
              Your password has been successfully updated. You can now log in using your new credentials.
            </p>
            <a routerLink="/auth/login" class="btn btn-primary px-4 py-2">
              Sign In
            </a>
          </div>
        } @else if (!token) {
          <div class="text-center py-4">
            <div class="d-inline-flex align-items-center justify-content-center bg-danger-subtle text-danger rounded-circle mb-3" style="width: 64px; height: 64px;">
              <i class="bi bi-exclamation-triangle-fill fs-2"></i>
            </div>
            <h4 class="fw-bold text-dark">Invalid Reset Link</h4>
            <p class="text-muted small mb-4">
              The password reset token is missing or malformed. Please request a new password reset link.
            </p>
            <a routerLink="/auth/forgot-password" class="btn btn-outline-primary px-4 py-2">
              Request New Link
            </a>
          </div>
        } @else {
          <div class="text-center mb-4">
            <div class="d-inline-flex align-items-center justify-content-center bg-primary-subtle text-primary rounded-circle mb-3" style="width: 56px; height: 56px;">
              <i class="bi bi-shield-lock-fill fs-3"></i>
            </div>
            <h3 class="fw-bold text-dark">Set New Password</h3>
            <p class="text-muted small">Choose a secure password for your PushpakClasses account</p>
          </div>

          @if (errorMessage) {
            <div class="alert alert-danger d-flex align-items-center gap-2 py-2" role="alert">
              <i class="bi bi-exclamation-octagon-fill"></i>
              <div>{{ errorMessage }}</div>
            </div>
          }

          <form [formGroup]="resetForm" (ngSubmit)="onSubmit()">
            <!-- New Password -->
            <div class="mb-3">
              <label for="newPassword" class="form-label small fw-semibold">New Password</label>
              <div class="input-group">
                <span class="input-group-text bg-light border-end-0">
                  <i class="bi bi-lock text-muted"></i>
                </span>
                <input 
                  [type]="showPassword ? 'text' : 'password'" 
                  id="newPassword" 
                  formControlName="newPassword" 
                  class="form-control border-start-0 border-end-0 ps-0" 
                  [class.is-invalid]="f['newPassword'].touched && f['newPassword'].invalid"
                  placeholder="Min 8 chars with upper, lower, digit & special" />
                <button 
                  type="button" 
                  class="btn btn-light border border-start-0" 
                  (click)="showPassword = !showPassword">
                  <i class="bi" [ngClass]="showPassword ? 'bi-eye-slash' : 'bi-eye'"></i>
                </button>
              </div>
              @if (f['newPassword'].touched && f['newPassword'].invalid) {
                <div class="text-danger small mt-1">
                  Password must be at least 8 characters and contain uppercase, lowercase, digit, and special character.
                </div>
              }
            </div>

            <!-- Confirm New Password -->
            <div class="mb-4">
              <label for="confirmPassword" class="form-label small fw-semibold">Confirm Password</label>
              <div class="input-group">
                <span class="input-group-text bg-light border-end-0">
                  <i class="bi bi-shield-lock text-muted"></i>
                </span>
                <input 
                  [type]="showPassword ? 'text' : 'password'" 
                  id="confirmPassword" 
                  formControlName="confirmPassword" 
                  class="form-control border-start-0 ps-0" 
                  [class.is-invalid]="(f['confirmPassword'].touched && f['confirmPassword'].invalid) || (resetForm.errors?.['passwordMismatch'] && f['confirmPassword'].touched)"
                  placeholder="Re-enter new password" />
              </div>
              @if (resetForm.errors?.['passwordMismatch'] && f['confirmPassword'].touched) {
                <div class="text-danger small mt-1">Passwords do not match.</div>
              }
            </div>

            <!-- Submit Button -->
            <button 
              type="submit" 
              class="btn btn-primary w-100 py-2 fw-semibold d-flex align-items-center justify-content-center gap-2" 
              [disabled]="resetForm.invalid || isSubmitting">
              @if (isSubmitting) {
                <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
                <span>Updating password...</span>
              } @else {
                <span>Reset Password</span>
                <i class="bi bi-arrow-right"></i>
              }
            </button>
          </form>

          <div class="text-center mt-4 pt-2 border-top">
            <p class="text-muted small mb-0">
              <a routerLink="/auth/login" class="text-primary fw-semibold text-decoration-none">Back to Sign In</a>
            </p>
          </div>
        }
      </div>
    </div>
  `
})
export class ResetPasswordComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);

  token: string | null = null;
  isSubmitting = false;
  showPassword = false;
  errorMessage: string | null = null;
  resetSuccess = false;

  resetForm: FormGroup = this.fb.group({
    newPassword: ['', [Validators.required, passwordPolicyValidator]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: passwordMatchValidator });

  get f() {
    return this.resetForm.controls;
  }

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParams['token'] || null;
  }

  onSubmit(): void {
    if (this.resetForm.invalid || !this.token) {
      this.resetForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = null;

    const email = this.route.snapshot.queryParams['email'] || undefined;
    const { newPassword, confirmPassword } = this.resetForm.value;

    this.authService.resetPassword({
      token: this.token,
      email,
      newPassword,
      confirmNewPassword: confirmPassword || newPassword
    }).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.resetSuccess = true;
        this.toastService.showSuccess('Your password has been reset successfully.', 'Password Changed');
      },
      error: (err) => {
        this.isSubmitting = false;
        this.errorMessage = err.error?.message || 'Password reset link has expired or is invalid.';
      }
    });
  }
}
