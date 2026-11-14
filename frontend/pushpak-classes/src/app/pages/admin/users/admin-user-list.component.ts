import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, RouterLink, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { AdminUserDto } from '../../../core/models/admin.models';
import { ToastService } from '../../../core/services/toast.service';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';

@Component({
  selector: 'app-admin-user-list',
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
              <li class="breadcrumb-item active" aria-current="page">Users</li>
            </ol>
          </nav>
          <h2 class="fw-bold mb-0">User Administration</h2>
        </div>
        <div class="d-flex gap-2">
          <a routerLink="/admin/dashboard" class="btn btn-outline-secondary d-flex align-items-center gap-1">
            <i class="bi bi-arrow-left"></i>
            <span>Back to Dashboard</span>
          </a>
        </div>
      </div>

      <!-- Search & Role Toolbar -->
      <div class="card border-0 shadow-sm mb-4">
        <div class="card-body p-3">
          <div class="row g-2 align-items-center">
            <div class="col-12 col-md-6">
              <div class="input-group">
                <span class="input-group-text bg-light border-end-0">
                  <i class="bi bi-search text-muted"></i>
                </span>
                <input 
                  type="text" 
                  class="form-control bg-light border-start-0" 
                  placeholder="Search by candidate name or email..."
                  [(ngModel)]="searchTerm"
                  (keyup.enter)="onSearch()">
              </div>
            </div>
            <div class="col-12 col-md-3">
              <select class="form-select bg-light" [(ngModel)]="selectedRole" (change)="onRoleChange()">
                <option value="">All Roles</option>
                <option value="User">User / Candidate</option>
                <option value="Admin">Administrator</option>
              </select>
            </div>
            <div class="col-12 col-md-3 d-flex gap-2 justify-content-end">
              <button class="btn btn-primary w-100" (click)="onSearch()" [disabled]="isLoading()">
                <i class="bi bi-search me-1"></i> Search
              </button>
              @if (searchTerm || selectedRole) {
                <button class="btn btn-outline-secondary" (click)="clearFilters()" [disabled]="isLoading()">
                  <i class="bi bi-arrow-counterclockwise"></i>
                </button>
              }
            </div>
          </div>
        </div>
      </div>

      <!-- Users Table -->
      <div class="card border-0 shadow-sm">
        <div class="card-header bg-white border-0 py-3 d-flex justify-content-between align-items-center">
          <h5 class="fw-bold mb-0 text-dark">Accounts Directory</h5>
          <span class="badge bg-secondary-subtle text-secondary">{{ totalUsers() | number }} Total Accounts</span>
        </div>
        <div class="card-body p-0">
          @if (error()) {
            <div class="alert alert-danger m-3 d-flex justify-content-between align-items-center" role="alert">
              <div><i class="bi bi-exclamation-triangle-fill me-2"></i>{{ error() }}</div>
              <button class="btn btn-sm btn-outline-danger" (click)="loadUsers()">Retry</button>
            </div>
          }

          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0">
              <thead class="table-light text-muted small text-uppercase">
                <tr>
                  <th class="ps-4">Candidate / Administrator</th>
                  <th>Email</th>
                  <th>Role</th>
                  <th>Status</th>
                  <th>Created (UTC)</th>
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
                        <span class="placeholder col-3 small text-muted"></span>
                      </td>
                      <td><span class="placeholder col-7 font-monospace"></span></td>
                      <td><span class="placeholder col-4"></span></td>
                      <td><span class="placeholder col-4"></span></td>
                      <td><span class="placeholder col-5"></span></td>
                      <td class="pe-4 text-end"><span class="placeholder col-4"></span></td>
                    </tr>
                  }
                } @else if (users().length === 0) {
                  <tr>
                    <td colspan="6" class="text-center py-5 text-muted">
                      <i class="bi bi-people fs-1 text-muted d-block mb-2"></i>
                      <h5 class="fw-bold text-muted">No Accounts Found</h5>
                      <p class="small mb-0">No users match your current search or filter criteria.</p>
                    </td>
                  </tr>
                } @else {
                  @for (user of users(); track user.id) {
                    <tr>
                      <td class="ps-4">
                        <div class="fw-semibold text-dark">{{ user.fullName }}</div>
                        @if (user.phoneNumber) {
                          <div class="text-muted small">{{ user.phoneNumber }}</div>
                        }
                      </td>
                      <td class="font-monospace small">{{ user.email }}</td>
                      <td>
                        @if (user.role === 'Admin') {
                          <span class="badge bg-danger-subtle text-danger">Administrator</span>
                        } @else {
                          <span class="badge bg-primary-subtle text-primary">Candidate</span>
                        }
                      </td>
                      <td>
                        @if (user.isActive) {
                          <span class="badge bg-success-subtle text-success">
                            <i class="bi bi-check-circle-fill me-1"></i> Active
                          </span>
                        } @else {
                          <span class="badge bg-danger-subtle text-danger">
                            <i class="bi bi-x-circle-fill me-1"></i> Suspended
                          </span>
                        }
                      </td>
                      <td class="text-muted small">
                        {{ user.createdAtUtc | date:'shortDate':'UTC' }}
                      </td>
                      <td class="pe-4 text-end">
                        @if (isCurrentLoggedUser(user.id)) {
                          <span class="badge bg-secondary-subtle text-secondary small">Self</span>
                        } @else {
                          <button 
                            class="btn btn-sm d-inline-flex align-items-center gap-1"
                            [ngClass]="user.isActive ? 'btn-outline-danger' : 'btn-outline-success'"
                            [disabled]="togglingUserId() === user.id"
                            (click)="toggleStatus(user)">
                            @if (togglingUserId() === user.id) {
                              <span class="spinner-border spinner-border-sm" role="status"></span>
                            }
                            <span>{{ user.isActive ? 'Deactivate' : 'Activate' }}</span>
                          </button>
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
            [totalCount]="totalUsers()"
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
export class AdminUserListComponent implements OnInit {
  private adminService = inject(AdminService);
  private authService = inject(AuthService);
  private toastService = inject(ToastService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  users = signal<AdminUserDto[]>([]);
  totalUsers = signal<number>(0);
  page = signal<number>(1);
  pageSize = signal<number>(10);

  isLoading = signal<boolean>(true);
  error = signal<string | null>(null);
  togglingUserId = signal<string | null>(null);

  searchTerm = '';
  selectedRole = '';

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const p = Number(params['page']);
      const s = Number(params['pageSize']);
      if (!isNaN(p) && p > 0) this.page.set(p);
      if (!isNaN(s) && s > 0) this.pageSize.set(s);
      if (params['search'] !== undefined) this.searchTerm = params['search'];
      if (params['role'] !== undefined) this.selectedRole = params['role'];
      this.loadUsers();
    });
  }

  isCurrentLoggedUser(userId: string): boolean {
    return this.authService.currentUser()?.id === userId;
  }

  loadUsers(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.adminService.getUsers(
      this.page(),
      this.pageSize(),
      this.searchTerm,
      this.selectedRole || undefined
    ).subscribe({
      next: (res) => {
        this.users.set(res.items);
        this.totalUsers.set(res.totalCount);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to retrieve accounts list.');
        this.isLoading.set(false);
      }
    });
  }

  onSearch(): void {
    this.page.set(1);
    this.updateUrlParams();
  }

  onRoleChange(): void {
    this.page.set(1);
    this.updateUrlParams();
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.selectedRole = '';
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
        role: this.selectedRole || null
      },
      queryParamsHandling: 'merge'
    });
  }

  toggleStatus(user: AdminUserDto): void {
    const nextState = !user.isActive;
    this.togglingUserId.set(user.id);

    this.adminService.toggleUserStatus(user.id, nextState).subscribe({
      next: () => {
        this.togglingUserId.set(null);
        user.isActive = nextState;
        this.toastService.showSuccess(`User account ${nextState ? 'activated' : 'deactivated'} successfully.`);
      },
      error: (err) => {
        this.togglingUserId.set(null);
        this.toastService.showError(err.error?.message || 'Could not update user status.');
      }
    });
  }
}
