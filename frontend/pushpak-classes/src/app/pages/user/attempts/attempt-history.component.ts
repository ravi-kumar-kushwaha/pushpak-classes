import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, RouterLink } from '@angular/router';
import { ReportService } from '../../../core/services/report.service';
import { AttemptService } from '../../../core/services/attempt.service';
import { ToastService } from '../../../core/services/toast.service';
import { UserAttemptHistoryDto } from '../../../core/models/report.models';

@Component({
  selector: 'app-attempt-history',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, RouterLink],
  template: `
    <div class="container-fluid">
      <!-- Title -->
      <div class="d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3 mb-4">
        <div>
          <h2 class="fw-bold mb-1">My Attempt History</h2>
          <p class="text-muted mb-0">Review all your previous exam results, scores, and certificates</p>
        </div>
      </div>

      <!-- Attempts Table Card -->
      <div class="card border-0 shadow-sm">
        <div class="card-body p-4">
          <!-- Search & Controls Toolbar -->
          <div class="d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3 mb-3">
            <div class="input-group" style="max-width: 380px;">
              <span class="input-group-text bg-white border-end-0">
                <i class="bi bi-search text-muted"></i>
              </span>
              <input 
                type="text" 
                class="form-control border-start-0 ps-0" 
                placeholder="Search by quiz title or category..." 
                [ngModel]="searchTerm()" 
                (ngModelChange)="onSearch($event)" />
              @if (searchTerm()) {
                <button class="btn btn-outline-secondary border-start-0" type="button" (click)="clearSearch()">
                  <i class="bi bi-x-lg"></i>
                </button>
              }
            </div>

            <div class="d-flex align-items-center gap-3">
              <div class="d-flex align-items-center gap-2">
                <label class="text-muted small text-nowrap">Items per page:</label>
                <select 
                  class="form-select form-select-sm w-auto" 
                  [ngModel]="pageSize()" 
                  (ngModelChange)="onPageSizeChange($event)">
                  <option [value]="15">15</option>
                  <option [value]="25">25</option>
                  <option [value]="50">50</option>
                  <option [value]="100">100</option>
                </select>
              </div>

              <div class="text-muted small">
                Total Attempts: <strong>{{ totalCount() }}</strong>
              </div>
            </div>
          </div>

          <!-- Table Responsive -->
          <div class="table-responsive">
            @if (isLoading()) {
              <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                  <span class="visually-hidden">Loading attempts...</span>
                </div>
              </div>
            } @else if (attempts().length === 0) {
              <div class="text-center py-5 text-muted">
                <i class="bi bi-journal-x fs-1 d-block mb-3 text-secondary"></i>
                <h5 class="fw-bold text-dark mb-1">No Attempt History Yet</h5>
                <p class="text-muted mb-3">You haven't attempted any quizzes yet. Explore available examinations and test your knowledge!</p>
                <a routerLink="/user/quizzes" class="btn btn-primary btn-sm px-3">
                  <i class="bi bi-play-circle-fill me-1"></i> Explore Available Quizzes
                </a>
              </div>
            } @else {
              <table class="table table-hover align-middle mb-0">
                <thead class="table-light">
                  <tr>
                    <th>Quiz Title</th>
                    <th>Category</th>
                    <th>Status</th>
                    <th>Submitted Time</th>
                    <th>Score</th>
                    <th>Percentage</th>
                    <th>Result</th>
                    <th>Tab Switches</th>
                    <th class="text-end">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  @for (attempt of attempts(); track attempt.id) {
                    <tr>
                      <!-- Quiz Title -->
                      <td class="fw-semibold text-dark">{{ attempt.quizTitle }}</td>

                      <!-- Category -->
                      <td>
                        <span class="badge bg-secondary-subtle text-secondary">
                          {{ attempt.categoryName }}
                        </span>
                      </td>

                      <!-- Status Badge (InProgress, Submitted, AutoSubmitted) -->
                      <td>
                        @if (attempt.status === 'InProgress') {
                          <span class="badge bg-warning text-dark">
                            <i class="bi bi-play-circle-fill me-1"></i> InProgress
                          </span>
                        } @else if (attempt.status === 'AutoSubmitted') {
                          <span class="badge bg-info text-dark">
                            <i class="bi bi-clock-history me-1"></i> AutoSubmitted
                          </span>
                        } @else {
                          <span class="badge bg-primary-subtle text-primary">
                            <i class="bi bi-check2-circle me-1"></i> Submitted
                          </span>
                        }
                      </td>

                      <!-- Submitted Time in Local Time -->
                      <td class="text-muted small">
                        @if (attempt.submittedAtUtc) {
                          {{ attempt.submittedAtUtc | date:'medium' }}
                        } @else {
                          <span class="text-secondary italic">In progress</span>
                        }
                      </td>

                      <!-- Score -->
                      <td class="fw-bold">
                        @if (attempt.score !== null && attempt.score !== undefined) {
                          {{ attempt.score }} / {{ attempt.totalMarks }}
                        } @else {
                          <span class="text-muted">-- / {{ attempt.totalMarks }}</span>
                        }
                      </td>

                      <!-- Percentage -->
                      <td>
                        @if (attempt.percent !== null && attempt.percent !== undefined) {
                          <div class="d-flex align-items-center gap-2">
                            <span class="small fw-semibold">{{ attempt.percent | number:'1.1-1' }}%</span>
                            <div class="progress flex-grow-1" style="height: 4px; min-width: 60px;">
                              <div 
                                class="progress-bar" 
                                [ngClass]="attempt.passed ? 'bg-success' : 'bg-danger'" 
                                role="progressbar" 
                                [style.width.%]="attempt.percent">
                              </div>
                            </div>
                          </div>
                        } @else {
                          <span class="text-muted small">Calculating...</span>
                        }
                      </td>

                      <!-- Result -->
                      <td>
                        @if (attempt.status === 'InProgress') {
                          <span class="badge bg-warning text-dark">In Progress</span>
                        } @else if (attempt.score === null || attempt.score === undefined) {
                          <span class="badge bg-info-subtle text-info-emphasis">Evaluating</span>
                        } @else if (attempt.passed) {
                          <span class="badge bg-success">Passed</span>
                        } @else {
                          <span class="badge bg-danger">Failed</span>
                        }
                      </td>

                      <!-- Tab Switches -->
                      <td>
                        @if (attempt.tabSwitchCount > 0) {
                          <span class="badge bg-warning-subtle text-warning-emphasis">
                            <i class="bi bi-exclamation-triangle me-1"></i>{{ attempt.tabSwitchCount }}
                          </span>
                        } @else {
                          <span class="text-muted small">0</span>
                        }
                      </td>

                      <!-- Actions -->
                      <td class="text-end">
                        <div class="btn-group btn-group-sm">
                          @if (attempt.status === 'InProgress') {
                            <!-- Resume InProgress Attempt -->
                            <a 
                              [routerLink]="['/user/attempts', attempt.id, 'play']" 
                              class="btn btn-warning text-dark fw-semibold" 
                              title="Resume Quiz Attempt">
                              <i class="bi bi-play-fill me-1"></i> Resume
                            </a>
                          } @else {
                            <!-- Review Submitted Attempt -->
                            <a 
                              [routerLink]="['/user/attempts', attempt.id, 'review']" 
                              class="btn btn-outline-primary" 
                              title="View Question Review">
                              <i class="bi bi-eye me-1"></i> Review
                            </a>
                          }

                          @if (attempt.passed) {
                            <button 
                              class="btn btn-outline-success" 
                              [disabled]="isDownloadingId() === attempt.id"
                              (click)="downloadCertificate(attempt.id)" 
                              title="Download PDF Certificate">
                              @if (isDownloadingId() === attempt.id) {
                                <span class="spinner-border spinner-border-sm me-1"></span>
                              } @else {
                                <i class="bi bi-award me-1"></i>
                              }
                              <span>Certificate</span>
                            </button>
                          }
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </div>

          <!-- Pagination Footer -->
          @if (totalCount() > 0) {
            <div class="d-flex flex-column flex-sm-row justify-content-between align-items-center gap-3 pt-3 mt-3 border-top">
              <span class="text-muted small">
                Showing <strong>{{ startItem() }}</strong> to <strong>{{ endItem() }}</strong> of <strong>{{ totalCount() }}</strong> recorded attempts
              </span>
              <ul class="pagination pagination-sm mb-0">
                <!-- First Page -->
                <li class="page-item" [class.disabled]="pageNumber() <= 1">
                  <button class="page-link" (click)="onPageChange(1)" [disabled]="pageNumber() <= 1" title="First Page">
                    <i class="bi bi-chevron-double-left"></i>
                  </button>
                </li>
                <!-- Previous Page -->
                <li class="page-item" [class.disabled]="pageNumber() <= 1">
                  <button class="page-link" (click)="onPageChange(pageNumber() - 1)" [disabled]="pageNumber() <= 1" title="Previous Page">
                    <i class="bi bi-chevron-left"></i>
                  </button>
                </li>
                <!-- Numeric Pages -->
                @for (p of displayedPages(); track p) {
                  <li class="page-item" [class.active]="p === pageNumber()">
                    <button class="page-link" (click)="onPageChange(p)">{{ p }}</button>
                  </li>
                }
                <!-- Next Page -->
                <li class="page-item" [class.disabled]="pageNumber() >= totalPages()">
                  <button class="page-link" (click)="onPageChange(pageNumber() + 1)" [disabled]="pageNumber() >= totalPages()" title="Next Page">
                    <i class="bi bi-chevron-right"></i>
                  </button>
                </li>
                <!-- Last Page -->
                <li class="page-item" [class.disabled]="pageNumber() >= totalPages()">
                  <button class="page-link" (click)="onPageChange(totalPages())" [disabled]="pageNumber() >= totalPages()" title="Last Page">
                    <i class="bi bi-chevron-double-right"></i>
                  </button>
                </li>
              </ul>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class AttemptHistoryComponent implements OnInit {
  private reportService = inject(ReportService);
  private attemptService = inject(AttemptService);
  private toastService = inject(ToastService);

  // Reactive signals for zoneless change detection
  attempts = signal<UserAttemptHistoryDto[]>([]);
  isLoading = signal(true);
  searchTerm = signal('');
  pageNumber = signal(1);
  pageSize = signal(15);
  totalCount = signal(0);
  isDownloadingId = signal<string | null>(null);

  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()) || 1);
  startItem = computed(() => this.totalCount() === 0 ? 0 : (this.pageNumber() - 1) * this.pageSize() + 1);
  endItem = computed(() => Math.min(this.pageNumber() * this.pageSize(), this.totalCount()));

  displayedPages = computed(() => {
    const total = this.totalPages();
    const current = this.pageNumber();
    const maxVisible = 5;

    let start = Math.max(1, current - 2);
    let end = Math.min(total, start + maxVisible - 1);

    if (end - start < maxVisible - 1) {
      start = Math.max(1, end - maxVisible + 1);
    }

    const pages: number[] = [];
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  });

  ngOnInit(): void {
    this.loadAttempts();
  }

  loadAttempts(): void {
    this.isLoading.set(true);
    this.reportService.getUserAttempts(this.pageNumber(), this.pageSize(), this.searchTerm()).subscribe({
      next: (res) => {
        this.attempts.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }

  onSearch(term: string): void {
    this.searchTerm.set(term);
    this.pageNumber.set(1);
    this.loadAttempts();
  }

  clearSearch(): void {
    this.searchTerm.set('');
    this.pageNumber.set(1);
    this.loadAttempts();
  }

  onPageChange(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.pageNumber.set(page);
      this.loadAttempts();
    }
  }

  onPageSizeChange(newSize: any): void {
    const size = Number(newSize) || 15;
    this.pageSize.set(size);
    this.pageNumber.set(1);
    this.loadAttempts();
  }

  downloadCertificate(attemptId: string): void {
    if (this.isDownloadingId() !== null) return; // double-click protection

    this.isDownloadingId.set(attemptId);
    this.attemptService.downloadCertificate(attemptId).subscribe({
      next: (blob) => {
        this.isDownloadingId.set(null);
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `PushpakClasses_Certificate_${attemptId}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.toastService.showSuccess('Official certificate downloaded successfully!', 'Download Complete');
      },
      error: () => {
        this.isDownloadingId.set(null);
        this.toastService.showError('Unable to download certificate.', 'Error');
      }
    });
  }
}
