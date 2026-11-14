import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, RouterLink, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../core/services/admin.service';
import { ToastService } from '../../../core/services/toast.service';
import { ReattemptRequestDto } from '../../../core/models/quiz.models';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';

@Component({
  selector: 'app-admin-reattempt-list',
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
              <li class="breadcrumb-item active" aria-current="page">Reattempt Requests</li>
            </ol>
          </nav>
          <h2 class="fw-bold mb-0">Quiz Reattempt Requests</h2>
        </div>
        <div class="d-flex gap-2">
          <a routerLink="/admin/dashboard" class="btn btn-outline-secondary d-flex align-items-center gap-1">
            <i class="bi bi-arrow-left"></i>
            <span>Dashboard</span>
          </a>
        </div>
      </div>

      <!-- Filters Toolbar -->
      <div class="card border-0 shadow-sm mb-4">
        <div class="card-body p-3">
          <div class="row g-2 align-items-center">
            <!-- Search -->
            <div class="col-12 col-md-5">
              <div class="input-group">
                <span class="input-group-text bg-light border-end-0">
                  <i class="bi bi-search text-muted"></i>
                </span>
                <input 
                  type="text" 
                  class="form-control bg-light border-start-0" 
                  placeholder="Search candidate name, email, quiz or reason..."
                  [(ngModel)]="searchTerm"
                  (keyup.enter)="onSearch()">
              </div>
            </div>

            <!-- Status Filter -->
            <div class="col-12 col-md-4">
              <select class="form-select bg-light" [(ngModel)]="selectedStatus" (change)="onStatusChange()">
                <option value="">All Statuses</option>
                <option value="Pending">Pending Review</option>
                <option value="Approved">Approved</option>
                <option value="Rejected">Rejected</option>
              </select>
            </div>

            <!-- Apply & Reset Buttons -->
            <div class="col-12 col-md-3 d-flex justify-content-end gap-2">
              <button class="btn btn-primary flex-grow-1" (click)="onSearch()" [disabled]="isLoading()">
                <i class="bi bi-funnel me-1"></i> Filter
              </button>
              @if (searchTerm || selectedStatus) {
                <button class="btn btn-outline-secondary" (click)="resetFilters()" [disabled]="isLoading()">
                  <i class="bi bi-arrow-counterclockwise"></i>
                </button>
              }
            </div>
          </div>
        </div>
      </div>

      <!-- Table Card -->
      <div class="card border-0 shadow-sm">
        <div class="card-header bg-white border-0 py-3 d-flex justify-content-between align-items-center">
          <h5 class="fw-bold mb-0 text-dark">Submitted Appeals & Exceptions</h5>
          <span class="badge bg-secondary-subtle text-secondary">{{ totalCount() | number }} Total</span>
        </div>
        <div class="card-body p-0">
          @if (error()) {
            <div class="alert alert-danger m-3 d-flex justify-content-between align-items-center" role="alert">
              <div><i class="bi bi-exclamation-triangle-fill me-2"></i>{{ error() }}</div>
              <button class="btn btn-sm btn-outline-danger" (click)="loadRequests()">Retry</button>
            </div>
          }

          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0">
              <thead class="table-light border-bottom">
                <tr>
                  <th class="ps-4">Candidate</th>
                  <th>Quiz Title</th>
                  <th>Reason</th>
                  <th>Submitted</th>
                  <th>Status</th>
                  <th>Review Notes</th>
                  <th class="pe-4 text-end">Actions</th>
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
                      <td><span class="placeholder col-8"></span></td>
                      <td><span class="placeholder col-4"></span></td>
                      <td><span class="placeholder col-3"></span></td>
                      <td><span class="placeholder col-4"></span></td>
                      <td class="pe-4 text-end"><span class="placeholder col-4"></span></td>
                    </tr>
                  }
                } @else if (requests().length === 0) {
                  <tr>
                    <td colspan="7" class="text-center py-5 text-muted">
                      <i class="bi bi-inbox fs-1 d-block mb-2 text-secondary"></i>
                      <p class="mb-0">No reattempt requests found matching your filters.</p>
                    </td>
                  </tr>
                } @else {
                  @for (req of requests(); track req.id) {
                    <tr>
                      <td class="ps-4">
                        <div class="fw-semibold text-dark">{{ req.userFullName }}</div>
                        <div class="small text-muted">{{ req.userEmail }}</div>
                      </td>
                      <td>
                        <span class="fw-medium text-dark">{{ req.quizTitle }}</span>
                      </td>
                      <td style="max-width: 250px;">
                        <span class="small text-break">{{ req.reason }}</span>
                      </td>
                      <td class="small text-muted">
                        {{ req.createdAtUtc | date:'medium' }}
                      </td>
                      <td>
                        @if (req.status === 'Pending') {
                          <span class="badge bg-warning text-dark d-flex align-items-center gap-1 w-fit">
                            <i class="bi bi-hourglass-split"></i> Pending
                          </span>
                        } @else if (req.status === 'Approved') {
                          <span class="badge bg-success d-flex align-items-center gap-1 w-fit">
                            <i class="bi bi-check-circle"></i> Approved
                          </span>
                        } @else {
                          <span class="badge bg-danger d-flex align-items-center gap-1 w-fit">
                            <i class="bi bi-x-circle"></i> Rejected
                          </span>
                        }
                      </td>
                      <td class="small text-muted" style="max-width: 200px;">
                        @if (req.reviewNotes) {
                          <span class="text-dark">{{ req.reviewNotes }}</span>
                          @if (req.reviewedByUserName) {
                            <div class="small text-secondary">By {{ req.reviewedByUserName }}</div>
                          }
                        } @else {
                          <span class="text-muted fst-italic">None</span>
                        }
                      </td>
                      <td class="pe-4 text-end">
                        @if (req.status === 'Pending') {
                          <div class="btn-group btn-group-sm">
                            <button 
                              class="btn btn-outline-success" 
                              title="Approve Reattempt" 
                              (click)="openReviewModal(req, 'Approved')">
                              <i class="bi bi-check-lg me-1"></i> Approve
                            </button>
                            <button 
                              class="btn btn-outline-danger" 
                              title="Reject Reattempt" 
                              (click)="openReviewModal(req, 'Rejected')">
                              <i class="bi bi-x-lg me-1"></i> Reject
                            </button>
                          </div>
                        } @else {
                          <span class="small text-muted">Reviewed</span>
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
            [totalCount]="totalCount()"
            [pageSizeOptions]="[10, 25, 50]"
            [disabled]="isLoading()"
            (pageChange)="onPageChange($event)"
            (pageSizeChange)="onPageSizeChange($event)">
          </app-paginator>
        </div>
      </div>

      <!-- Review Action Modal -->
      @if (reviewingRequest()) {
        <div class="modal fade show d-block" tabindex="-1" style="background-color: rgba(0,0,0,0.5);">
          <div class="modal-dialog modal-dialog-centered">
            <div class="modal-content border-0 shadow">
              <div class="modal-header border-bottom">
                <h5 class="modal-title fw-bold">
                  {{ reviewDecision === 'Approved' ? 'Approve' : 'Reject' }} Reattempt Request
                </h5>
                <button type="button" class="btn-close" (click)="closeReviewModal()"></button>
              </div>
              <div class="modal-body p-4">
                <div class="mb-3 p-3 bg-light rounded">
                  <div class="small text-muted mb-1">Candidate: <strong>{{ reviewingRequest()!.userFullName }}</strong> ({{ reviewingRequest()!.userEmail }})</div>
                  <div class="small text-muted mb-1">Quiz: <strong>{{ reviewingRequest()!.quizTitle }}</strong></div>
                  <div class="small text-muted">Reason: <em>"{{ reviewingRequest()!.reason }}"</em></div>
                </div>

                @if (reviewDecision === 'Approved') {
                  <div class="alert alert-success small mb-3">
                    <i class="bi bi-info-circle me-1"></i> Approving this request will immediately grant <strong>1 extra attempt</strong> for this quiz to the candidate, and send an email notification.
                  </div>
                } @else {
                  <div class="alert alert-warning small mb-3">
                    <i class="bi bi-exclamation-triangle me-1"></i> Rejecting this request will keep the candidate's attempt limits unchanged, and notify them of the decision.
                  </div>
                }

                <div class="mb-3">
                  <label class="form-label fw-semibold">Reviewer Notes (Optional)</label>
                  <textarea 
                    class="form-control" 
                    rows="3" 
                    placeholder="Provide any feedback or context for the candidate..."
                    [(ngModel)]="reviewNotes">
                  </textarea>
                </div>
              </div>
              <div class="modal-footer border-top">
                <button type="button" class="btn btn-outline-secondary" (click)="closeReviewModal()">Cancel</button>
                <button 
                  type="button" 
                  [class]="reviewDecision === 'Approved' ? 'btn btn-success d-inline-flex align-items-center gap-2' : 'btn btn-danger d-inline-flex align-items-center gap-2'"
                  [disabled]="isSubmittingReview()"
                  (click)="submitReview()">
                  @if (isSubmittingReview()) {
                    <span class="spinner-border spinner-border-sm" role="status"></span>
                    <span>Processing...</span>
                  } @else {
                    <i [class]="reviewDecision === 'Approved' ? 'bi bi-check-lg' : 'bi bi-x-lg'"></i>
                    <span>Confirm {{ reviewDecision }}</span>
                  }
                </button>
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .w-fit {
      width: fit-content;
    }
  `]
})
export class AdminReattemptListComponent implements OnInit {
  private adminService = inject(AdminService);
  private toastService = inject(ToastService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  requests = signal<ReattemptRequestDto[]>([]);
  isLoading = signal(true);
  error = signal<string | null>(null);
  totalCount = signal(0);
  page = signal(1);
  pageSize = signal(10);

  searchTerm = '';
  selectedStatus = '';

  // Review Modal
  reviewingRequest = signal<ReattemptRequestDto | null>(null);
  reviewDecision: 'Approved' | 'Rejected' = 'Approved';
  reviewNotes = '';
  isSubmittingReview = signal(false);

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const p = Number(params['page']);
      const s = Number(params['pageSize']);
      if (!isNaN(p) && p > 0) this.page.set(p);
      if (!isNaN(s) && s > 0) this.pageSize.set(s);
      if (params['search'] !== undefined) this.searchTerm = params['search'];
      if (params['status'] !== undefined) this.selectedStatus = params['status'];
      this.loadRequests();
    });
  }

  loadRequests(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.adminService.getReattemptRequests(
      this.page(),
      this.pageSize(),
      this.searchTerm,
      this.selectedStatus
    ).subscribe({
      next: (res) => {
        this.requests.set(res.items);
        this.totalCount.set(res.totalCount);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load reattempt requests.');
        this.isLoading.set(false);
      }
    });
  }

  onSearch(): void {
    this.page.set(1);
    this.updateUrlParams();
  }

  onStatusChange(): void {
    this.page.set(1);
    this.updateUrlParams();
  }

  resetFilters(): void {
    this.searchTerm = '';
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
        status: this.selectedStatus || null
      },
      queryParamsHandling: 'merge'
    });
  }

  openReviewModal(req: ReattemptRequestDto, decision: 'Approved' | 'Rejected'): void {
    this.reviewingRequest.set(req);
    this.reviewDecision = decision;
    this.reviewNotes = '';
  }

  closeReviewModal(): void {
    this.reviewingRequest.set(null);
    this.reviewNotes = '';
  }

  submitReview(): void {
    const req = this.reviewingRequest();
    if (!req) return;

    this.isSubmittingReview.set(true);

    this.adminService.reviewReattemptRequest(req.id, this.reviewDecision, this.reviewNotes.trim()).subscribe({
      next: (updated) => {
        this.isSubmittingReview.set(false);
        this.toastService.showSuccess(`Reattempt request ${this.reviewDecision.toLowerCase()} successfully.`);
        this.closeReviewModal();
        this.loadRequests();
      },
      error: (err) => {
        this.isSubmittingReview.set(false);
        this.toastService.showError(err.error?.message || `Failed to ${this.reviewDecision.toLowerCase()} reattempt request.`);
      }
    });
  }
}
