import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, RouterLink, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { TimeService } from '../../../core/services/time.service';
import { AdminQuizDto, AdminCategoryDto } from '../../../core/models/admin.models';
import { ToastService } from '../../../core/services/toast.service';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';

@Component({
  selector: 'app-admin-quiz-list',
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
              <li class="breadcrumb-item active" aria-current="page">Quizzes</li>
            </ol>
          </nav>
          <h2 class="fw-bold mb-0">Quiz Management</h2>
        </div>
        <div class="d-flex gap-2">
          <a routerLink="/admin/quizzes/new" class="btn btn-primary d-flex align-items-center gap-2 shadow-sm">
            <i class="bi bi-plus-circle"></i>
            <span>Create New Quiz</span>
          </a>
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
            <div class="col-12 col-md-5">
              <div class="input-group">
                <span class="input-group-text bg-light border-end-0">
                  <i class="bi bi-search text-muted"></i>
                </span>
                <input 
                  type="text" 
                  class="form-control bg-light border-start-0" 
                  placeholder="Search quizzes by title..."
                  [(ngModel)]="searchTerm"
                  (keyup.enter)="onSearch()">
              </div>
            </div>
            <div class="col-12 col-md-4">
              <select class="form-select bg-light" [(ngModel)]="selectedCategory" (change)="onCategoryChange()">
                <option value="">All Categories</option>
                @for (cat of categories(); track cat.id) {
                  <option [value]="cat.id">{{ cat.name }} ({{ cat.quizCount }} quizzes)</option>
                }
              </select>
            </div>
            <div class="col-12 col-md-3 d-flex gap-2 justify-content-end">
              <button class="btn btn-primary w-100" (click)="onSearch()" [disabled]="isLoading()">
                <i class="bi bi-filter me-1"></i> Apply Filter
              </button>
              @if (searchTerm || selectedCategory) {
                <button class="btn btn-outline-secondary" (click)="clearFilters()" [disabled]="isLoading()">
                  <i class="bi bi-arrow-counterclockwise"></i>
                </button>
              }
            </div>
          </div>
        </div>
      </div>

      <!-- Quizzes Table -->
      <div class="card border-0 shadow-sm">
        <div class="card-header bg-white border-0 py-3 d-flex justify-content-between align-items-center">
          <h5 class="fw-bold mb-0 text-dark">Assessments Bank</h5>
          <span class="badge bg-secondary-subtle text-secondary">{{ totalCount() }} Total Quizzes</span>
        </div>
        <div class="card-body p-0">
          @if (error()) {
            <div class="alert alert-danger m-3 d-flex justify-content-between align-items-center" role="alert">
              <div><i class="bi bi-exclamation-triangle-fill me-2"></i>{{ error() }}</div>
              <button class="btn btn-sm btn-outline-danger" (click)="loadQuizzes()">Retry</button>
            </div>
          }

          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0">
              <thead class="table-light text-muted small text-uppercase">
                <tr>
                  <th class="ps-4">Quiz Title</th>
                  <th>Category</th>
                  <th>Schedule (Local)</th>
                  <th>Duration</th>
                  <th>Questions</th>
                  <th>Passing %</th>
                  <th>Status</th>
                  <th class="pe-4 text-end">Actions</th>
                </tr>
              </thead>
              <tbody>
                @if (isLoading()) {
                  <!-- Skeleton Loading Rows -->
                  @for (s of [1,2,3,4,5]; track s) {
                    <tr class="placeholder-glow">
                      <td class="ps-4">
                        <span class="placeholder col-7 d-block mb-1"></span>
                        <span class="placeholder col-4 small text-muted"></span>
                      </td>
                      <td><span class="placeholder col-5"></span></td>
                      <td><span class="placeholder col-6"></span></td>
                      <td><span class="placeholder col-4"></span></td>
                      <td><span class="placeholder col-4"></span></td>
                      <td><span class="placeholder col-3"></span></td>
                      <td><span class="placeholder col-4"></span></td>
                      <td class="pe-4 text-end"><span class="placeholder col-6"></span></td>
                    </tr>
                  }
                } @else if (quizzes().length === 0) {
                  <tr>
                    <td colspan="8" class="text-center py-5 text-muted">
                      <i class="bi bi-journal-x fs-1 text-muted d-block mb-2"></i>
                      <h5 class="fw-bold text-muted">No Quizzes Found</h5>
                      <p class="small mb-3">Get started by creating your first timed examination assessment.</p>
                      <a routerLink="/admin/quizzes/new" class="btn btn-outline-primary btn-sm">
                        <i class="bi bi-plus-lg me-1"></i> Create Quiz Now
                      </a>
                    </td>
                  </tr>
                } @else {
                  @for (quiz of quizzes(); track quiz.id) {
                    <tr>
                      <td class="ps-4">
                        <div class="fw-bold text-dark">{{ quiz.title }}</div>
                        <div class="text-muted small text-truncate" style="max-width: 250px;">
                          {{ quiz.description || 'No description provided.' }}
                        </div>
                      </td>
                      <td>
                        <span class="badge bg-secondary-subtle text-secondary">{{ quiz.categoryName }}</span>
                      </td>
                      <td>
                        <div class="small fw-semibold text-dark">{{ quiz.startAtUtc | date:'short' }}</div>
                        <div class="text-muted small">to {{ quiz.endAtUtc | date:'short' }}</div>
                      </td>
                      <td>
                        <i class="bi bi-stopwatch text-muted me-1"></i>
                        {{ quiz.durationMinutes }} mins
                      </td>
                      <td>
                        <span class="badge bg-info-subtle text-info">{{ quiz.questionCount }} questions</span>
                      </td>
                      <td>{{ quiz.passingPercent }}%</td>
                      <td>
                        @if (!quiz.isPublished) {
                          <span class="badge bg-secondary-subtle text-secondary">
                            <i class="bi bi-file-earmark me-1"></i> Draft
                          </span>
                        } @else {
                          @switch (getAdminQuizStatus(quiz)) {
                            @case ('Upcoming') {
                              <span class="badge bg-warning text-dark border">
                                <i class="bi bi-calendar-event me-1"></i> Upcoming
                              </span>
                            }
                            @case ('Expired') {
                              <span class="badge bg-danger">
                                <i class="bi bi-clock-history me-1"></i> Expired
                              </span>
                            }
                            @case ('Open') {
                              <span class="badge bg-success-subtle text-success">
                                <i class="bi bi-unlock-fill me-1"></i> Open
                              </span>
                            }
                          }
                        }
                      </td>
                      <td class="pe-4 text-end">
                        <div class="d-inline-flex gap-1">
                          <!-- Edit Button -->
                          <a 
                            [routerLink]="['/admin/quizzes', quiz.id, 'edit']" 
                            class="btn btn-sm btn-outline-primary"
                            title="Edit quiz & questions">
                            <i class="bi bi-pencil-square"></i>
                            <span class="d-none d-md-inline ms-1">Edit</span>
                          </a>

                          <!-- Publish / Unpublish Toggle with spinner -->
                          <button 
                            class="btn btn-sm d-inline-flex align-items-center gap-1" 
                            [ngClass]="quiz.isPublished ? 'btn-outline-warning' : 'btn-outline-success'"
                            [disabled]="publishingQuizId() === quiz.id || deletingQuizId() === quiz.id"
                            (click)="togglePublish(quiz)"
                            [title]="quiz.isPublished ? 'Unpublish quiz' : 'Publish quiz'">
                            @if (publishingQuizId() === quiz.id) {
                              <span class="spinner-border spinner-border-sm" role="status"></span>
                            } @else {
                              <i class="bi" [ngClass]="quiz.isPublished ? 'bi-eye-slash' : 'bi-check2-circle'"></i>
                            }
                            <span class="d-none d-md-inline ms-1">{{ quiz.isPublished ? 'Unpublish' : 'Publish' }}</span>
                          </button>

                          <!-- Delete Button with spinner -->
                          <button 
                            class="btn btn-sm btn-outline-danger" 
                            [disabled]="deletingQuizId() === quiz.id || publishingQuizId() === quiz.id"
                            (click)="deleteQuiz(quiz)"
                            title="Delete quiz">
                            @if (deletingQuizId() === quiz.id) {
                              <span class="spinner-border spinner-border-sm" role="status"></span>
                            } @else {
                              <i class="bi bi-trash"></i>
                            }
                          </button>
                        </div>
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
            [totalCount]="totalCount()"
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
export class AdminQuizListComponent implements OnInit {
  private adminService = inject(AdminService);
  private toastService = inject(ToastService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private timeService = inject(TimeService);

  quizzes = signal<AdminQuizDto[]>([]);
  categories = signal<AdminCategoryDto[]>([]);
  totalCount = signal<number>(0);
  page = signal<number>(1);
  pageSize = signal<number>(10);

  isLoading = signal<boolean>(true);
  error = signal<string | null>(null);

  publishingQuizId = signal<string | null>(null);
  deletingQuizId = signal<string | null>(null);

  searchTerm = '';
  selectedCategory = '';

  getAdminQuizStatus(quiz: AdminQuizDto): 'Draft' | 'Upcoming' | 'Open' | 'Expired' {
    if (!quiz.isPublished) return 'Draft';
    const now = this.timeService.nowUtc().getTime();
    const startMs = this.timeService.parseUtcTimestamp(quiz.startAtUtc);
    const endMs = this.timeService.parseUtcTimestamp(quiz.endAtUtc);

    if (!isNaN(startMs) && now < startMs) return 'Upcoming';
    if (!isNaN(endMs) && now > endMs) return 'Expired';
    return 'Open';
  }

  ngOnInit(): void {
    this.loadCategories();
    this.route.queryParams.subscribe(params => {
      const p = Number(params['page']);
      const s = Number(params['pageSize']);
      if (!isNaN(p) && p > 0) this.page.set(p);
      if (!isNaN(s) && s > 0) this.pageSize.set(s);
      if (params['search'] !== undefined) this.searchTerm = params['search'];
      if (params['categoryId'] !== undefined) this.selectedCategory = params['categoryId'];
      this.loadQuizzes();
    });
  }

  loadCategories(): void {
    this.adminService.getCategories().subscribe({
      next: (data) => this.categories.set(data),
      error: () => {}
    });
  }

  loadQuizzes(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.adminService.getQuizzes(
      this.page(),
      this.pageSize(),
      this.searchTerm,
      this.selectedCategory || undefined
    ).subscribe({
      next: (res) => {
        this.quizzes.set(res.items);
        this.totalCount.set(res.totalCount);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load quizzes list.');
        this.isLoading.set(false);
      }
    });
  }

  onSearch(): void {
    this.page.set(1);
    this.updateUrlParams();
  }

  onCategoryChange(): void {
    this.page.set(1);
    this.updateUrlParams();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.selectedCategory = '';
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
        categoryId: this.selectedCategory || null
      },
      queryParamsHandling: 'merge'
    });
  }

  togglePublish(quiz: AdminQuizDto): void {
    const nextState = !quiz.isPublished;
    this.publishingQuizId.set(quiz.id);

    this.adminService.toggleQuizPublish(quiz.id, nextState).subscribe({
      next: () => {
        this.publishingQuizId.set(null);
        quiz.isPublished = nextState;
        this.toastService.showSuccess(`Quiz ${nextState ? 'published' : 'unpublished'} successfully.`);
      },
      error: (err) => {
        this.publishingQuizId.set(null);
        this.toastService.showError(err.error?.message || 'Failed to update publish state.');
      }
    });
  }

  deleteQuiz(quiz: AdminQuizDto): void {
    if (!confirm(`Are you sure you want to delete the quiz "${quiz.title}"?`)) {
      return;
    }

    this.deletingQuizId.set(quiz.id);
    this.adminService.deleteQuiz(quiz.id).subscribe({
      next: () => {
        this.deletingQuizId.set(null);
        this.toastService.showSuccess(`Quiz "${quiz.title}" deleted.`);
        this.loadQuizzes();
      },
      error: (err) => {
        this.deletingQuizId.set(null);
        this.toastService.showError(err.error?.message || 'Could not delete quiz.');
      }
    });
  }
}
