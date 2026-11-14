import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/services/auth.service';
import { ReportService } from '../../../core/services/report.service';
import { AttemptService } from '../../../core/services/attempt.service';
import { ToastService } from '../../../core/services/toast.service';
import { UserDashboardDto } from '../../../core/models/report.models';

@Component({
  selector: 'app-user-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, RouterLink],
  template: `
    <div class="container-fluid">
      <!-- Greeting Header -->
      <div class="d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3 mb-4">
        <div>
          <h2 class="fw-bold mb-1">Welcome back, {{ authService.currentUser()?.fullName }}!</h2>
          <p class="text-muted mb-0">PushpakClasses Timed Assessment & Quiz Portal</p>
        </div>
        <a routerLink="/user/quizzes" class="btn btn-primary d-flex align-items-center gap-2 px-3 py-2">
          <i class="bi bi-play-circle-fill fs-5"></i>
          <span class="fw-semibold">Take a Quiz</span>
        </a>
      </div>

      <!-- Quick Metrics Cards -->
      <div class="row g-3 mb-4">
        <!-- Total Attempts -->
        <div class="col-12 col-sm-6 col-xl-3">
          <div class="card border-0 shadow-sm h-100 card-hover">
            <div class="card-body d-flex align-items-center">
              <div class="bg-primary-subtle text-primary p-3 rounded-4 me-3">
                <i class="bi bi-journal-check fs-3"></i>
              </div>
              <div>
                <div class="text-muted small fw-medium">Total Attempts</div>
                <h3 class="fw-bold mb-0 text-dark">{{ dashboardData()?.totalAttempts ?? 0 }}</h3>
              </div>
            </div>
          </div>
        </div>

        <!-- Average Score -->
        <div class="col-12 col-sm-6 col-xl-3">
          <div class="card border-0 shadow-sm h-100 card-hover">
            <div class="card-body d-flex align-items-center">
              <div class="bg-success-subtle text-success p-3 rounded-4 me-3">
                <i class="bi bi-graph-up-arrow fs-3"></i>
              </div>
              <div>
                <div class="text-muted small fw-medium">Average Score</div>
                <h3 class="fw-bold mb-0 text-dark">
                  {{ dashboardData() ? (dashboardData()!.averageScore | number:'1.1-1') + '%' : '0.0%' }}
                </h3>
              </div>
            </div>
          </div>
        </div>

        <!-- Best Score -->
        <div class="col-12 col-sm-6 col-xl-3">
          <div class="card border-0 shadow-sm h-100 card-hover">
            <div class="card-body d-flex align-items-center">
              <div class="bg-warning-subtle text-warning p-3 rounded-4 me-3">
                <i class="bi bi-trophy-fill fs-3 text-warning"></i>
              </div>
              <div>
                <div class="text-muted small fw-medium">Best Score</div>
                <h3 class="fw-bold mb-0 text-dark">
                  {{ dashboardData() ? (dashboardData()!.bestScore | number:'1.1-1') + '%' : '0.0%' }}
                </h3>
              </div>
            </div>
          </div>
        </div>

        <!-- Pass Rate -->
        <div class="col-12 col-sm-6 col-xl-3">
          <div class="card border-0 shadow-sm h-100 card-hover">
            <div class="card-body d-flex align-items-center">
              <div class="bg-info-subtle text-info p-3 rounded-4 me-3">
                <i class="bi bi-patch-check-fill fs-3 text-info"></i>
              </div>
              <div>
                <div class="text-muted small fw-medium">Pass Rate</div>
                <h3 class="fw-bold mb-0 text-dark">
                  {{ dashboardData() ? (dashboardData()!.passRate | number:'1.1-1') + '%' : '0.0%' }}
                </h3>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Recent Attempts Section -->
      <div class="card border-0 shadow-sm mb-4">
        <div class="card-header bg-white py-3 border-bottom d-flex justify-content-between align-items-center">
          <h5 class="fw-bold mb-0 d-flex align-items-center gap-2">
            <i class="bi bi-clock-history text-primary"></i>
            <span>Recent Test Attempts</span>
          </h5>
          <a routerLink="/user/attempts" class="btn btn-outline-secondary btn-sm">
            View All History <i class="bi bi-arrow-right ms-1"></i>
          </a>
        </div>
        <div class="card-body p-0">
          @if (isLoading()) {
            <div class="text-center py-5">
              <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading dashboard...</span>
              </div>
            </div>
          } @else if (hasError()) {
            <div class="text-center py-5 text-muted">
              <i class="bi bi-exclamation-triangle fs-1 text-danger d-block mb-2"></i>
              <p class="mb-3 text-dark fw-medium">Failed to load recent attempts.</p>
              <button class="btn btn-primary btn-sm px-3" (click)="loadDashboard()">
                <i class="bi bi-arrow-clockwise me-1"></i> Retry
              </button>
            </div>
          } @else if (!dashboardData()?.recentAttempts || dashboardData()!.recentAttempts.length === 0) {
            <div class="text-center py-5 text-muted">
              <i class="bi bi-inbox fs-1 d-block mb-2 text-secondary"></i>
              <p class="mb-2">You haven't attempted any quizzes yet.</p>
              <a routerLink="/user/quizzes" class="btn btn-outline-primary btn-sm">
                Explore Available Quizzes
              </a>
            </div>
          } @else {
            <div class="table-responsive">
              <table class="table table-hover align-middle mb-0">
                <thead class="table-light">
                  <tr>
                    <th>Quiz Title</th>
                    <th>Category</th>
                    <th>Date Taken</th>
                    <th>Score</th>
                    <th>Status</th>
                    <th class="text-end">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  @for (attempt of dashboardData()!.recentAttempts; track attempt.id) {
                    <tr>
                      <td class="fw-semibold">{{ attempt.quizTitle }}</td>
                      <td>
                        <span class="badge bg-secondary-subtle text-secondary">
                          {{ attempt.categoryName }}
                        </span>
                      </td>
                      <td class="text-muted small">
                        {{ attempt.startedAtUtc | date:'medium' }}
                      </td>
                      <td>
                        <div class="fw-semibold">
                          {{ attempt.score }} / {{ attempt.totalMarks }}
                        </div>
                        <div class="small text-muted">
                          {{ attempt.percent | number:'1.1-1' }}%
                        </div>
                      </td>
                      <td>
                        @if (attempt.passed) {
                          <span class="badge bg-success">Passed</span>
                        } @else if (attempt.status === 'InProgress') {
                          <span class="badge bg-warning text-dark">In Progress</span>
                        } @else {
                          <span class="badge bg-danger">Failed</span>
                        }
                      </td>
                      <td class="text-end">
                        <div class="btn-group btn-group-sm">
                          <a 
                            [routerLink]="['/user/attempts', attempt.id, 'review']" 
                            class="btn btn-outline-primary" 
                            title="Review Attempt">
                            <i class="bi bi-eye"></i> Review
                          </a>
                          @if (attempt.passed) {
                            <button 
                              class="btn btn-outline-success" 
                              (click)="downloadCertificate(attempt.id)" 
                              title="Download PDF Certificate">
                              <i class="bi bi-award"></i>
                            </button>
                          }
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class UserDashboardComponent implements OnInit {
  authService = inject(AuthService);
  private reportService = inject(ReportService);
  private attemptService = inject(AttemptService);
  private toastService = inject(ToastService);

  dashboardData = signal<UserDashboardDto | null>(null);
  isLoading = signal(true);
  hasError = signal(false);

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    this.isLoading.set(true);
    this.hasError.set(false);
    this.reportService.getUserDashboard()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (data) => {
          this.dashboardData.set(data);
        },
        error: () => {
          this.hasError.set(true);
          this.toastService.showError('Unable to load dashboard data. Please try again.', 'Error');
        }
      });
  }

  downloadCertificate(attemptId: string): void {
    this.attemptService.downloadCertificate(attemptId).subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `PushpakClasses_Certificate_${attemptId}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.toastService.showSuccess('Certificate downloaded successfully!', 'Download Complete');
      },
      error: () => {
        this.toastService.showError('Unable to download certificate.', 'Error');
      }
    });
  }
}
