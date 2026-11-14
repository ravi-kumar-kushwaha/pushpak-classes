import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, RouterLink],
  template: `
    <div class="card shadow-lg border-0 rounded-4">
      <div class="card-body p-4 p-md-5">
        @if (requestSubmitted) {
          <div class="text-center py-4">
            <div class="d-inline-flex align-items-center justify-content-center bg-info-subtle text-info rounded-circle mb-3" style="width: 64px; height: 64px;">
              <i class="bi bi-send-check-fill fs-2"></i>
            </div>
            <h4 class="fw-bold text-dark">Check Your Inbox</h4>
            <p class="text-muted small mb-4">
              If an account with that email exists, we have sent instructions to reset your password. 
              The reset link will remain valid for 30 minutes.
            </p>
            <a routerLink="/auth/login" class="btn btn-outline-primary px-4 py-2">
              Back to Login
            </a>
          </div>
        } @else {
          <div class="text-center mb-4">
            <div class="d-inline-flex align-items-center justify-content-center bg-warning-subtle text-warning rounded-circle mb-3" style="width: 56px; height: 56px;">
              <i class="bi bi-key-fill fs-3 text-dark"></i>
            </div>
            <h3 class="fw-bold text-dark">Forgot Password?</h3>
            <p class="text-muted small">Enter your email and we'll send you a link to reset your password</p>
          </div>

          <form [formGroup]="forgotForm" (ngSubmit)="onSubmit()">
            <div class="mb-4">
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
                <div class="text-danger small mt-1">Please enter a valid email address.</div>
              }
            </div>

            <button 
              type="submit" 
              class="btn btn-primary w-100 py-2 fw-semibold d-flex align-items-center justify-content-center gap-2" 
              [disabled]="forgotForm.invalid || isSubmitting">
              @if (isSubmitting) {
                <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
                <span>Sending link...</span>
              } @else {
                <span>Send Reset Link</span>
                <i class="bi bi-arrow-right"></i>
              }
            </button>
          </form>

          <div class="text-center mt-4 pt-2 border-top">
            <p class="text-muted small mb-0">
              Remember your password? 
              <a routerLink="/auth/login" class="text-primary fw-semibold text-decoration-none">Back to Sign In</a>
            </p>
          </div>
        }
      </div>
    </div>
  `
})
export class ForgotPasswordComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);

  forgotForm: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]]
  });

  isSubmitting = false;
  requestSubmitted = false;

  get f() {
    return this.forgotForm.controls;
  }

  onSubmit(): void {
    if (this.forgotForm.invalid) {
      this.forgotForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    const { email } = this.forgotForm.value;

    this.authService.forgotPassword({ email }).subscribe({
      next: () => {
        this.isSubmitting = false;
        this.requestSubmitted = true;
      },
      error: () => {
        // Anti-enumeration: still display generic confirmation even on failure
        this.isSubmitting = false;
        this.requestSubmitted = true;
      }
    });
  }
}
