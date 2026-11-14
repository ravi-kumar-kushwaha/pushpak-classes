import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, RouterLink, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { AdminCategoryDto } from '../../../core/models/admin.models';
import { ToastService } from '../../../core/services/toast.service';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';

@Component({
  selector: 'app-admin-category-list',
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
              <li class="breadcrumb-item active" aria-current="page">Categories</li>
            </ol>
          </nav>
          <h2 class="fw-bold mb-0">Category Taxonomy</h2>
        </div>
        <div class="d-flex gap-2">
          <a routerLink="/admin/dashboard" class="btn btn-outline-secondary d-flex align-items-center gap-1">
            <i class="bi bi-arrow-left"></i>
            <span>Back to Dashboard</span>
          </a>
        </div>
      </div>

      <!-- Add Category Card -->
      <div class="card border-0 shadow-sm mb-4">
        <div class="card-header bg-white border-0 py-3">
          <h5 class="fw-bold mb-0 text-dark">
            <i class="bi bi-plus-circle-fill text-success me-2"></i>Create New Category
          </h5>
        </div>
        <div class="card-body pt-0">
          <div class="row g-2 align-items-center">
            <div class="col-12 col-md-8">
              <input 
                type="text" 
                class="form-control" 
                placeholder="Enter category name (e.g. Quantitative Aptitude, General Science)..."
                [(ngModel)]="newCategoryName"
                (keyup.enter)="createCategory()">
            </div>
            <div class="col-12 col-md-4">
              <button 
                class="btn btn-success w-100 d-flex align-items-center justify-content-center gap-2"
                [disabled]="isCreating() || !newCategoryName.trim()"
                (click)="createCategory()">
                @if (isCreating()) {
                  <span class="spinner-border spinner-border-sm" role="status"></span>
                  <span>Adding...</span>
                } @else {
                  <i class="bi bi-check-lg"></i>
                  <span>Add Category</span>
                }
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Filter / Search Toolbar -->
      <div class="card border-0 shadow-sm mb-4">
        <div class="card-body p-3">
          <div class="row g-2 align-items-center">
            <div class="col-12 col-md-8">
              <div class="input-group">
                <span class="input-group-text bg-light border-end-0">
                  <i class="bi bi-search text-muted"></i>
                </span>
                <input 
                  type="text" 
                  class="form-control bg-light border-start-0" 
                  placeholder="Search categories by name..."
                  [(ngModel)]="searchTerm"
                  (keyup.enter)="onSearch()">
              </div>
            </div>
            <div class="col-12 col-md-4 d-flex gap-2 justify-content-end">
              <button class="btn btn-primary w-100" (click)="onSearch()" [disabled]="isLoading()">
                <i class="bi bi-search me-1"></i> Search
              </button>
              @if (searchTerm) {
                <button class="btn btn-outline-secondary" (click)="clearSearch()" [disabled]="isLoading()">
                  <i class="bi bi-x-lg"></i>
                </button>
              }
            </div>
          </div>
        </div>
      </div>

      <!-- Category List Table -->
      <div class="card border-0 shadow-sm">
        <div class="card-header bg-white border-0 py-3 d-flex justify-content-between align-items-center">
          <h5 class="fw-bold mb-0 text-dark">Taxonomy Classifications</h5>
          <span class="badge bg-primary-subtle text-primary">{{ totalCount() }} Total Categories</span>
        </div>
        <div class="card-body p-0">
          @if (error()) {
            <div class="alert alert-danger m-3 d-flex justify-content-between align-items-center" role="alert">
              <div><i class="bi bi-exclamation-triangle-fill me-2"></i>{{ error() }}</div>
              <button class="btn btn-sm btn-outline-danger" (click)="loadCategories()">Retry</button>
            </div>
          }

          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0">
              <thead class="table-light text-muted small text-uppercase">
                <tr>
                  <th class="ps-4">Category Name</th>
                  <th>Linked Quizzes</th>
                  <th class="pe-4 text-end">ID</th>
                </tr>
              </thead>
              <tbody>
                @if (isLoading()) {
                  <!-- Skeleton Loading Rows -->
                  @for (s of [1,2,3,4,5]; track s) {
                    <tr class="placeholder-glow">
                      <td class="ps-4">
                        <span class="placeholder col-6"></span>
                      </td>
                      <td>
                        <span class="placeholder col-4"></span>
                      </td>
                      <td class="pe-4 text-end">
                        <span class="placeholder col-8"></span>
                      </td>
                    </tr>
                  }
                } @else if (categories().length === 0) {
                  <tr>
                    <td colspan="3" class="text-center py-5 text-muted">
                      <i class="bi bi-tags fs-1 text-muted d-block mb-2"></i>
                      <h6 class="fw-bold text-muted">No Categories Found</h6>
                      <p class="small mb-0">Create your first category or try a different search filter.</p>
                    </td>
                  </tr>
                } @else {
                  @for (cat of categories(); track cat.id) {
                    <tr>
                      <td class="ps-4">
                        <div class="fw-semibold text-dark">{{ cat.name }}</div>
                      </td>
                      <td>
                        <span class="badge bg-secondary-subtle text-secondary">
                          <i class="bi bi-journal-check me-1"></i>
                          {{ cat.quizCount }} Quizzes
                        </span>
                      </td>
                      <td class="pe-4 text-end text-muted small font-monospace">
                        {{ cat.id }}
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
export class AdminCategoryListComponent implements OnInit {
  private adminService = inject(AdminService);
  private toastService = inject(ToastService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  categories = signal<AdminCategoryDto[]>([]);
  totalCount = signal<number>(0);
  page = signal<number>(1);
  pageSize = signal<number>(10);

  isLoading = signal<boolean>(true);
  isCreating = signal<boolean>(false);
  error = signal<string | null>(null);

  searchTerm = '';
  newCategoryName = '';

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const p = Number(params['page']);
      const s = Number(params['pageSize']);
      if (!isNaN(p) && p > 0) this.page.set(p);
      if (!isNaN(s) && s > 0) this.pageSize.set(s);
      if (params['search'] !== undefined) this.searchTerm = params['search'];
      this.loadCategories();
    });
  }

  loadCategories(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.adminService.getCategoriesPaged(
      this.page(),
      this.pageSize(),
      this.searchTerm
    ).subscribe({
      next: (data) => {
        this.categories.set(data.items);
        this.totalCount.set(data.totalCount);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load category list.');
        this.isLoading.set(false);
      }
    });
  }

  onSearch(): void {
    this.page.set(1);
    this.updateUrlParams();
  }

  clearSearch(): void {
    this.searchTerm = '';
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
        search: this.searchTerm || null
      },
      queryParamsHandling: 'merge'
    });
  }

  createCategory(): void {
    const trimmed = this.newCategoryName.trim();
    if (!trimmed) return;

    this.isCreating.set(true);
    this.adminService.createCategory(trimmed).subscribe({
      next: (created) => {
        this.isCreating.set(false);
        this.newCategoryName = '';
        this.toastService.showSuccess(`Category "${created.name}" created.`);
        this.loadCategories();
      },
      error: (err) => {
        this.isCreating.set(false);
        this.toastService.showError(err.error?.message || 'Failed to create category.');
      }
    });
  }
}
