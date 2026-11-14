import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, RouterLink, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { AdminAttemptDto, AdminQuizDto } from '../../../core/models/admin.models';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';

@Component({
  selector: 'app-admin-attempt-list',
  standalone: true,
  imports: [CommonModule, RouterModule, RouterLink, FormsModule, PaginatorComponent],
  template: `
    <div class="container-fluid">
      <!-- Breadcrumb & Header -->
      <div class="d-flex flex-wrap justify-content-between align-items-center mb-4 gap-3">
        <div>
          <nav aria-label="breadcrumb">
            <ol class="breadcrumb mb-1 small">
              <li class="breadcrumb-item"><a routerLink="/admin/dashboard" class="text-decoration-none">Dashboard</a></li>
              <li class="breadcrumb-item active" aria-current="page">Attempts</li>
            </ol>
          </nav>
          <h2 class="fw-bold mb-0">Platform Exam Attempts</h2>
        </div>
        <div class="d-flex gap-2">
          <a routerLink="/admin/dashboard" class="btn btn-outline-secondary d-flex align-items-center gap-1">
            <i class="bi bi-arrow-left"></i>
            <span>Dashboard</span>
          </a>
        </div>
      </div>

      <!-- Filters & Search Toolbar -->
      <div class="card border-0 shadow-sm mb-4">
        <div class="card-body p-3">
          <div class="row g-2 align-items-center">
            <!-- Search -->
            <div class="col-12 col-md-4">
              <div class="input-group">
                <span class="input-group-text bg-light border-end-0">
                  <i class="bi bi-search text-muted"></i>
                </span>
                <input 
                  type="text" 
                  class="form-control bg-light border-start-0" 
                  placeholder="Candidate name or email..."
                  [(ngModel)]="searchTerm"
                  (keyup.enter)="onSearch()">
              </div>
            </div>

            <!-- Quiz Filter -->
            <div class="col-12 col-md-3">
              <select class="form-select bg-light" [(ngModel)]="selectedQuizId" (change)="onFilterChange()">
                <option value="">All Quizzes</option>
                @for (q of quizzes(); track q.id) {
                  <option [value]="q.id">{{ q.title }}</option>
                }
              </select>
            </div>

            <!-- Status Filter -->
            <div class="col-12 col-md-3">
              <select class="form-select bg-light" [(ngModel)]="selectedStatus" (change)="onFilterChange()">
                <option value="">All Statuses</option>
                <option value="Submitted">Submitted</option>
                <option value="AutoSubmitted">Auto Submitted</option>
                <option value="InProgress">In Progress</option>
              </select>
            </div>

            <!-- Apply Button -->
            <div class="col-12 col-md-2 d-flex gap-2 justify-content-end">
              <button class="btn btn-primary w-100" (click)="onSearch()" [disabled]="isLoading()">
                <i class="bi bi-funnel me-1"></i> Filter
              </button>
              @if (searchTerm || selectedQuizId || selectedStatus) {
                <button class="btn btn-outline-secondary" (click)="clearFilters()" [disabled]="isLoading()">
                  <i class="bi bi-arrow-counterclockwise"></i>
                </button>
              }
            </div>
          </div>
        </div>
      </div>

      <!-- Attempts Table -->
      <div class="card border-0 shadow-sm">
        <div class="card-header bg-white border-0 py-3 d-flex justify-content-between align-items-center">
          <h5 class="fw-bold mb-0 text-dark">Recent Submissions & In-Progress Attempts</h5>
          <span class="badge bg-secondary-subtle text-secondary">{{ totalAttempts() | number }} Total</span>
        </div>
        <div class="card-body p-0">
          @if (error()) {
            <div class="alert alert-danger m-3 d-flex justify-content-between align-items-center" role="alert">
              <div><i class="bi bi-exclamation-triangle-fill me-2"></i>{{ error() }}</div>
              <button class="btn btn-sm btn-outline-danger" (click)="loadAttempts()">Retry</button>
            </div>
          }

          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0">
              <thead class="table-light text-muted small text-uppercase">
                <tr>
                  <th class="ps-4">Candidate</th>
                  <th>Quiz Title</th>
                  <th>Status</th>
                  <th>Score / Marks</th>
                  <th>Result</th>
                  <th>Tab Switches</th>
                  <th>Started (UTC)</th>
                  <th class="pe-4 text-end">Action</th>
                </tr>
              </thead>
              <tbody>
                @if (isLoading()) {
                  <!-- Skeleton Loading Rows -->
                  @for (s of [1,2,3,4,5]; track s) {
                    <tr class="placeholder-glow">
                      <td class="ps-4">
                        <span class="placeholder col-6 d-block mb-1"></span>
                        <span class="placeholder col-4 small text-muted"></span>
                      </td>
                      <td><span class="placeholder col-6"></span></td>
                      <td><span class="placeholder col-4"></span></td>
                      <td><span class="placeholder col-5"></span></td>
                      <td><span class="placeholder col-3"></span></td>
                      <td><span class="placeholder col-2"></span></td>
                      <td><span class="placeholder col-4"></span></td>
                      <td class="pe-4 text-end"><span class="placeholder col-4"></span></td>
                    </tr>
                  }
                } @else if (attempts().length === 0) {
                  <tr>
                    <td colspan="8" class="text-center py-5 text-muted">
                      <i class="bi bi-clipboard-x fs-1 text-muted d-block mb-2"></i>
                      <h5 class="fw-bold text-muted">No Attempts Found</h5>
                      <p class="small mb-0">No exam attempts match your search or filter criteria.</p>
                    </td>
                  </tr>
                } @else {
                  @for (attempt of attempts(); track attempt.attemptId) {
                    <tr>
                      <td class="ps-4">
                        <div class="fw-semibold text-dark">{{ attempt.userFullName }}</div>
                        <div class="text-muted small font-monospace">{{ attempt.userEmail }}</div>
                      </td>
                      <td>
                        <div class="fw-semibold">{{ attempt.quizTitle }}</div>
                      </td>
                      <td>
                        <span class="badge" [ngClass]="getStatusBadge(attempt.status)">
                          {{ attempt.status }}
                        </span>
                      </td>
                      <td>
                        <span class="fw-bold">{{ attempt.score }}</span> / {{ attempt.totalMarks }}
                        <span class="text-muted small ms-1">({{ attempt.percent | number:'1.0-1' }}%)</span>
                      </td>
                      <td>
                        @if (attempt.status === 'Submitted' || attempt.status === 'AutoSubmitted') {
                          @if (attempt.passed) {
                            <span class="badge bg-success-subtle text-success">
                              <i class="bi bi-check-circle-fill me-1"></i> Passed
                            </span>
                          } @else {
                            <span class="badge bg-danger-subtle text-danger">
                              <i class="bi bi-x-circle-fill me-1"></i> Failed
                            </span>
                          }
                        } @else {
                          <span class="badge bg-light text-muted border">In Progress</span>
                        }
                      </td>
                      <td>
                        @if (attempt.tabSwitchCount > 0) {
                          <span class="badge bg-warning-subtle text-warning">
                            <i class="bi bi-exclamation-triangle-fill me-1"></i> {{ attempt.tabSwitchCount }}
                          </span>
                        } @else {
                          <span class="text-muted small">0</span>
                        }
                      </td>
                      <td class="text-muted small">
                        {{ attempt.startedAtUtc | date:'short':'UTC' }}
                      </td>
                      <td class="pe-4 text-end">
                        @if (attempt.status === 'Submitted' || attempt.status === 'AutoSubmitted') {
                          <a 
                            [routerLink]="['/admin/attempts', attempt.attemptId]" 
                            class="btn btn-sm btn-outline-primary d-inline-flex align-items-center gap-1">
                            <i class="bi bi-eye"></i>
                            <span>Review</span>
                          </a>
                        } @else {
                          <span class="badge bg-light text-muted">Running</span>
                        }
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          </div>

          <!-- Reusable Paginator -->
          <app-paginator
            [page]="page()"
            [pageSize]="pageSize()"
            [totalCount]="totalAttempts()"
            [pageSizeOptions]="[10, 25, 50]"
            [disabled]="isLoading()"
            (pageChange)="onPageChange($event)"
            (pageSizeChange)="onPageSizeChange($event)">
          </app-paginator>
        </div>
      </div>
    </div>
  `
})
export class AdminAttemptListComponent implements OnInit {
  private adminService = inject(AdminService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  attempts = signal<AdminAttemptDto[]>([]);
  quizzes = signal<AdminQuizDto[]>([]);
  totalAttempts = signal<number>(0);
  page = signal<number>(1);
  pageSize = signal<number>(10);

  isLoading = signal<boolean>(true);
  error = signal<string | null>(null);

  searchTerm = '';
  selectedQuizId = '';
  selectedStatus = '';

  ngOnInit(): void {
    this.loadQuizzesList();
    this.route.queryParams.subscribe(params => {
      const p = Number(params['page']);
      const s = Number(params['pageSize']);
      if (!isNaN(p) && p > 0) this.page.set(p);
      if (!isNaN(s) && s > 0) this.pageSize.set(s);
      if (params['search'] !== undefined) this.searchTerm = params['search'];
      if (params['quizId'] !== undefined) this.selectedQuizId = params['quizId'];
      if (params['status'] !== undefined) this.selectedStatus = params['status'];
      this.loadAttempts();
    });
  }

  loadQuizzesList(): void {
    this.adminService.getQuizzes(1, 100).subscribe({
      next: (res) => this.quizzes.set(res.items),
      error: () => {}
    });
  }

  loadAttempts(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.adminService.getAttempts(
      this.page(),
      this.pageSize(),
      this.searchTerm,
      this.selectedQuizId || undefined,
      undefined,
      this.selectedStatus || undefined
    ).subscribe({
      next: (res) => {
        this.attempts.set(res.items);
        this.totalAttempts.set(res.totalCount);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to retrieve attempt records.');
        this.isLoading.set(false);
      }
    });
  }

  onSearch(): void {
    this.page.set(1);
    this.updateUrlParams();
  }

  onFilterChange(): void {
    this.page.set(1);
    this.updateUrlParams();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.selectedQuizId = '';
    this.selectedStatus = '';
    this.page.set(1);
    this.updateUrlParams();
  }

  onPageChange(newPage: number): void {
    this.page.set(newPage);
    this.updateUrlParams();
  }

  onPageSizeChange(newSize: number): void {
    this.pageSize.set(newSize);
    this.page.set(1);
    this.updateUrlParams();
  }

  private updateUrlParams(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        page: this.page(),
        pageSize: this.pageSize(),
        search: this.searchTerm || null,
        quizId: this.selectedQuizId || null,
        status: this.selectedStatus || null
      },
      queryParamsHandling: 'merge'
    });
  }

  getStatusBadge(status: string): string {
    switch (status) {
      case 'Submitted':
        return 'bg-success-subtle text-success';
      case 'AutoSubmitted':
        return 'bg-info-subtle text-info';
      case 'InProgress':
        return 'bg-warning-subtle text-warning';
      default:
        return 'bg-secondary-subtle text-secondary';
    }
  }
}
