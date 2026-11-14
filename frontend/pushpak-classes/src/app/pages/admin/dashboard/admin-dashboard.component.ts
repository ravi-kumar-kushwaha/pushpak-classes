import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, RouterLink } from '@angular/router';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { AdminDashboardDto } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, RouterLink],
  template: `
    <div class="container-fluid">
      <!-- Admin Header -->
      <div class="d-flex flex-wrap justify-content-between align-items-center mb-4 gap-3">
        <div>
          <h2 class="fw-bold mb-1">Administrator Dashboard</h2>
          <p class="text-muted mb-0">PushpakClasses System Overview & Real-Time Analytics</p>
        </div>
        <div class="d-flex gap-2">
          <a routerLink="/admin/quizzes" class="btn btn-primary d-flex align-items-center gap-2 shadow-sm">
            <i class="bi bi-journal-plus"></i>
            <span>Manage Quizzes</span>
          </a>
          <button class="btn btn-outline-secondary d-flex align-items-center gap-2 shadow-sm" (click)="loadDashboard()" [disabled]="isLoading()">
            <i class="bi bi-arrow-clockwise" [class.spin]="isLoading()"></i>
            <span>Refresh</span>
          </button>
        </div>
      </div>

      <!-- Loading State -->
      @if (isLoading()) {
        <div class="card border-0 shadow-sm py-5 text-center mb-4">
          <div class="card-body">
            <div class="spinner-border text-primary mb-3" role="status">
              <span class="visually-hidden">Loading metrics...</span>
            </div>
            <p class="text-muted mb-0">Fetching real-time metrics from PushpakClasses cluster...</p>
          </div>
        </div>
      }

      <!-- Error State -->
      @if (error() && !isLoading()) {
        <div class="alert alert-danger shadow-sm d-flex align-items-center justify-content-between mb-4" role="alert">
          <div class="d-flex align-items-center gap-2">
            <i class="bi bi-exclamation-triangle-fill fs-5"></i>
            <div>{{ error() }}</div>
          </div>
          <button class="btn btn-sm btn-outline-danger" (click)="loadDashboard()">
            <i class="bi bi-arrow-clockwise me-1"></i> Retry
          </button>
        </div>
      }

      <!-- Quick Metrics Cards -->
      @if (!isLoading() && stats()) {
        <div class="row g-3 mb-4">
          <div class="col-12 col-sm-6 col-xl-3">
            <div class="card border-0 shadow-sm h-100 hover-card">
              <div class="card-body d-flex align-items-center">
                <div class="bg-primary-subtle text-primary p-3 rounded-3 me-3">
                  <i class="bi bi-people-fill fs-4"></i>
                </div>
                <div>
                  <div class="text-muted small">Total Candidates</div>
                  <h4 class="fw-bold mb-0 text-dark">{{ stats()!.totalUsers | number }}</h4>
                </div>
              </div>
            </div>
          </div>

          <div class="col-12 col-sm-6 col-xl-3">
            <div class="card border-0 shadow-sm h-100 hover-card">
              <div class="card-body d-flex align-items-center">
                <div class="bg-success-subtle text-success p-3 rounded-3 me-3">
                  <i class="bi bi-journal-check fs-4"></i>
                </div>
                <div>
                  <div class="text-muted small">Total Quizzes</div>
                  <h4 class="fw-bold mb-0 text-dark">{{ stats()!.totalQuizzes | number }}</h4>
                </div>
              </div>
            </div>
          </div>

          <div class="col-12 col-sm-6 col-xl-3">
            <div class="card border-0 shadow-sm h-100 hover-card">
              <div class="card-body d-flex align-items-center">
                <div class="bg-warning-subtle text-warning p-3 rounded-3 me-3">
                  <i class="bi bi-clock-history fs-4"></i>
                </div>
                <div>
                  <div class="text-muted small">Attempts Logged</div>
                  <h4 class="fw-bold mb-0 text-dark">{{ stats()!.totalAttempts | number }}</h4>
                </div>
              </div>
            </div>
          </div>

          <div class="col-12 col-sm-6 col-xl-3">
            <div class="card border-0 shadow-sm h-100 hover-card">
              <div class="card-body d-flex align-items-center">
                <div class="bg-info-subtle text-info p-3 rounded-3 me-3">
                  <i class="bi bi-award-fill fs-4"></i>
                </div>
                <div>
                  <div class="text-muted small">Average Score</div>
                  <h4 class="fw-bold mb-0 text-dark">{{ stats()!.averageScore | number:'1.1-2' }}</h4>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Trend Section -->
        <div class="card border-0 shadow-sm mb-4">
          <div class="card-header bg-white border-0 py-3 d-flex justify-content-between align-items-center">
            <h5 class="fw-bold mb-0 text-dark">
              <i class="bi bi-graph-up text-primary me-2"></i>Recent Activity Trend
            </h5>
            <span class="badge bg-secondary-subtle text-secondary">30 Days Window</span>
          </div>
          <div class="card-body p-0">
            <div class="table-responsive">
              <table class="table table-hover align-middle mb-0">
                <thead class="table-light text-muted small text-uppercase">
                  <tr>
                    <th class="ps-4">Date (UTC)</th>
                    <th>Attempts</th>
                    <th>Avg Score</th>
                    <th class="pe-4 text-end">Load Indicator</th>
                  </tr>
                </thead>
                <tbody>
                  @for (day of getFilteredTrends(); track day.dateUtc) {
                    <tr>
                      <td class="ps-4 fw-semibold">{{ day.dateUtc | date:'mediumDate':'UTC' }}</td>
                      <td>
                        <span class="badge bg-primary-subtle text-primary">{{ day.attemptCount | number }} attempts</span>
                      </td>
                      <td>{{ day.averageScore | number:'1.1-2' }}</td>
                      <td class="pe-4 text-end">
                        <div class="progress d-inline-flex" style="width: 120px; height: 6px;">
                          <div class="progress-bar bg-success" [style.width.%]="getProgressPercent(day.attemptCount)"></div>
                        </div>
                      </td>
                    </tr>
                  }
                  @if (getFilteredTrends().length === 0) {
                    <tr>
                      <td colspan="4" class="text-center py-4 text-muted">
                        No activity recorded in the current window.
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }

      <!-- Quick Administration Actions -->
      <div class="row g-3">
        <div class="col-md-4">
          <div class="card border-0 shadow-sm h-100">
            <div class="card-body p-4 d-flex flex-column justify-content-between">
              <div>
                <div class="d-flex align-items-center gap-2 mb-2">
                  <i class="bi bi-journal-text fs-5 text-primary"></i>
                  <h5 class="fw-bold mb-0">Quiz Management</h5>
                </div>
                <p class="text-muted small mb-3">Create timed exams, configure duration, passing marks, and assign categories.</p>
              </div>
              <a routerLink="/admin/quizzes" class="btn btn-outline-primary btn-sm align-self-start">
                Quizzes List <i class="bi bi-arrow-right ms-1"></i>
              </a>
            </div>
          </div>
        </div>

        <div class="col-md-4">
          <div class="card border-0 shadow-sm h-100">
            <div class="card-body p-4 d-flex flex-column justify-content-between">
              <div>
                <div class="d-flex align-items-center gap-2 mb-2">
                  <i class="bi bi-tags fs-5 text-success"></i>
                  <h5 class="fw-bold mb-0">Category Taxonomy</h5>
                </div>
                <p class="text-muted small mb-3">Organize subject classifications and track quiz distributions.</p>
              </div>
              <a routerLink="/admin/categories" class="btn btn-outline-success btn-sm align-self-start">
                Manage Categories <i class="bi bi-arrow-right ms-1"></i>
              </a>
            </div>
          </div>
        </div>

        <div class="col-md-4">
          <div class="card border-0 shadow-sm h-100">
            <div class="card-body p-4 d-flex flex-column justify-content-between">
              <div>
                <div class="d-flex align-items-center gap-2 mb-2">
                  <i class="bi bi-people fs-5 text-warning"></i>
                  <h5 class="fw-bold mb-0">User Administration</h5>
                </div>
                <p class="text-muted small mb-3">Manage candidate accounts, toggle active status, review security audit trails.</p>
              </div>
              <a routerLink="/admin/users" class="btn btn-outline-warning btn-sm align-self-start">
                View Users <i class="bi bi-arrow-right ms-1"></i>
              </a>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .spin {
      animation: spin 1s linear infinite;
    }
    @keyframes spin {
      100% { transform: rotate(360deg); }
    }
    .hover-card {
      transition: transform 0.15s ease-in-out, box-shadow 0.15s ease-in-out;
    }
    .hover-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.08) !important;
    }
  `]
})
export class AdminDashboardComponent implements OnInit {
  private adminService = inject(AdminService);
  authService = inject(AuthService);

  stats = signal<AdminDashboardDto | null>(null);
  isLoading = signal<boolean>(true);
  error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.adminService.getDashboard().subscribe({
      next: (data) => {
        this.stats.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Unable to connect to PushpakClasses administration metrics service.');
        this.isLoading.set(false);
      }
    });
  }

  getFilteredTrends() {
    const list = this.stats()?.trendLast30Days || [];
    // Show days with activity or latest 5 days
    const activeDays = list.filter(d => d.attemptCount > 0);
    if (activeDays.length > 0) return activeDays;
    return list.slice(-5);
  }

  getProgressPercent(count: number): number {
    const max = Math.max(...(this.stats()?.trendLast30Days.map(d => d.attemptCount) || [100]));
    if (max === 0) return 0;
    return Math.min(100, Math.round((count / max) * 100));
  }
}
