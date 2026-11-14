import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule, RouterLink } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-verify-email',
  standalone: true,
  imports: [CommonModule, RouterModule, RouterLink],
  template: `
    <div class="card shadow-lg border-0 rounded-4">
      <div class="card-body p-4 p-md-5 text-center">
        @if (isVerifying()) {
          <div class="py-4">
            <div class="spinner-border text-primary mb-3" style="width: 3rem; height: 3rem;" role="status">
              <span class="visually-hidden">Verifying email...</span>
            </div>
            <h4 class="fw-bold text-dark">Verifying Email</h4>
            <p class="text-muted small">Please wait while we activate your account...</p>
          </div>
        } @else if (verificationSuccess()) {
          <div class="py-4">
            <div class="d-inline-flex align-items-center justify-content-center bg-success-subtle text-success rounded-circle mb-3" style="width: 64px; height: 64px;">
              <i class="bi bi-patch-check-fill fs-2"></i>
            </div>
            <h4 class="fw-bold text-dark">Email Verified!</h4>
            <p class="text-muted small mb-4">
              Your email address has been verified successfully. Your account is now active and ready.
            </p>
            <a routerLink="/auth/login" class="btn btn-primary px-4 py-2">
              Sign In to Your Account
            </a>
          </div>
        } @else {
          <div class="py-4">
            <div class="d-inline-flex align-items-center justify-content-center bg-danger-subtle text-danger rounded-circle mb-3" style="width: 64px; height: 64px;">
              <i class="bi bi-x-circle-fill fs-2"></i>
            </div>
            <h4 class="fw-bold text-dark">Verification Failed</h4>
            <p class="text-muted small mb-4">
              {{ errorMessage() || 'The verification link is invalid, expired, or has already been used.' }}
            </p>
            <div class="d-flex justify-content-center gap-2">
              <a routerLink="/auth/login" class="btn btn-outline-secondary px-4 py-2">
                Back to Login
              </a>
              <a routerLink="/auth/signup" class="btn btn-primary px-4 py-2">
                Register Again
              </a>
            </div>
          </div>
        }
      </div>
    </div>
  `
})
export class VerifyEmailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);

  isVerifying = signal(true);
  verificationSuccess = signal(false);
  errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    const token = this.route.snapshot.queryParams['token'];
    const email = this.route.snapshot.queryParams['email'] || undefined;
    if (!token) {
      this.isVerifying.set(false);
      this.verificationSuccess.set(false);
      this.errorMessage.set('No verification token was provided in the link.');
      return;
    }

    this.authService.verifyEmail({ token, email }).subscribe({
      next: () => {
        this.isVerifying.set(false);
        this.verificationSuccess.set(true);
        this.toastService.showSuccess('Email verified successfully! You can now log in.', 'Verified');
      },
      error: (err) => {
        this.isVerifying.set(false);
        this.verificationSuccess.set(false);
        this.errorMessage.set(err.error?.detail || err.error?.message || 'Verification token is invalid or has expired.');
      }
    });
  }
}
