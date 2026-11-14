import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { QuizService, CategoryOptionDto } from '../../../core/services/quiz.service';
import { TimeService } from '../../../core/services/time.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { ToastService } from '../../../core/services/toast.service';
import { AvailableQuizDto } from '../../../core/models/quiz.models';

@Component({
  selector: 'app-quiz-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="container-fluid">
      <!-- Title -->
      <div class="d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3 mb-4">
        <div>
          <h2 class="fw-bold mb-1">Available Quizzes</h2>
          <p class="text-muted mb-0">Select an assessment to start or resume your timed examination</p>
        </div>
      </div>

      <!-- Filters Toolbar -->
      <div class="card border-0 shadow-sm mb-4">
        <div class="card-body p-3">
          <div class="row g-2 align-items-center">
            <!-- Search Input -->
            <div class="col-12 col-md-4">
              <div class="input-group">
                <span class="input-group-text bg-white border-end-0">
                  <i class="bi bi-search text-muted"></i>
                </span>
                <input 
                  type="text" 
                  class="form-control border-start-0 ps-0" 
                  placeholder="Search title or description..." 
                  [ngModel]="searchTerm()" 
                  (ngModelChange)="onSearchChange($event)" />
                @if (searchTerm()) {
                  <button class="btn btn-outline-secondary border-start-0" type="button" (click)="clearSearch()">
                    <i class="bi bi-x-lg"></i>
                  </button>
                }
              </div>
            </div>

            <!-- Category Filter Dropdown -->
            <div class="col-12 col-md-3">
              <div class="input-group">
                <span class="input-group-text bg-white border-end-0">
                  <i class="bi bi-tags text-muted"></i>
                </span>
                <select 
                  class="form-select border-start-0 ps-0" 
                  [ngModel]="selectedCategoryId()" 
                  (ngModelChange)="onCategoryChange($event)">
                  <option value="">All Categories</option>
                  @for (cat of categories(); track cat.id) {
                    <option [value]="cat.id">{{ cat.name }}</option>
                  }
                </select>
              </div>
            </div>

            <!-- Status Filter Dropdown -->
            <div class="col-12 col-md-3">
              <div class="input-group">
                <span class="input-group-text bg-white border-end-0">
                  <i class="bi bi-funnel text-muted"></i>
                </span>
                <select 
                  class="form-select border-start-0 ps-0" 
                  [ngModel]="selectedStatus()" 
                  (ngModelChange)="onStatusChange($event)">
                  <option value="All">All Statuses</option>
                  <option value="Open">Open</option>
                  <option value="Attempted">Attempted</option>
                  <option value="Expired">Expired</option>
                  <option value="Upcoming">Upcoming</option>
                </select>
              </div>
            </div>

            <!-- Actions / Count -->
            <div class="col-12 col-md-2 d-flex justify-content-between justify-content-md-end align-items-center gap-2">
              <span class="text-muted small">
                <strong>{{ quizzes().length }}</strong> / <strong>{{ totalCount() }}</strong>
              </span>
              @if (searchTerm() || selectedCategoryId() || selectedStatus() !== 'All') {
                <button class="btn btn-sm btn-outline-danger" (click)="resetFilters()">
                  <i class="bi bi-arrow-counterclockwise"></i> Reset
                </button>
              }
            </div>
          </div>
        </div>
      </div>

      <!-- Error State -->
      @if (errorMessage()) {
        <div class="alert alert-danger d-flex align-items-center justify-content-between shadow-sm mb-4" role="alert">
          <div class="d-flex align-items-center gap-2">
            <i class="bi bi-exclamation-triangle-fill fs-5"></i>
            <div>{{ errorMessage() }}</div>
          </div>
          <button class="btn btn-sm btn-outline-danger" (click)="loadQuizzes()">
            <i class="bi bi-arrow-clockwise me-1"></i> Try Again
          </button>
        </div>
      }

      <!-- Loading State: Skeleton Cards -->
      @if (isLoading()) {
        <div class="row g-4">
          @for (placeholder of [1, 2, 3]; track placeholder) {
            <div class="col-12 col-md-6 col-xl-4">
              <div class="card border-0 shadow-sm p-4 placeholder-glow">
                <div class="d-flex justify-content-between mb-3">
                  <span class="placeholder col-4 py-2 rounded"></span>
                  <span class="placeholder col-3 py-2 rounded"></span>
                </div>
                <h5 class="placeholder col-8 py-2 mb-3"></h5>
                <p class="placeholder col-12 mb-2"></p>
                <p class="placeholder col-9 mb-4"></p>
                <div class="placeholder col-12 py-3 rounded mb-3"></div>
                <div class="placeholder col-12 py-3 rounded"></div>
              </div>
            </div>
          }
        </div>
      } @else if (quizzes().length === 0) {
        <!-- Empty State -->
        <div class="card border-0 shadow-sm text-center py-5">
          <div class="card-body">
            <i class="bi bi-journal-x fs-1 d-block mb-3 text-secondary"></i>
            <h4 class="fw-bold">No Quizzes Found</h4>
            <p class="text-muted mb-3">
              @if (searchTerm() || selectedCategoryId() || selectedStatus() !== 'All') {
                No assessments match your current filters. Try resetting the filters.
              } @else {
                There are currently no scheduled examinations open for your account.
              }
            </p>
            @if (searchTerm() || selectedCategoryId() || selectedStatus() !== 'All') {
              <button class="btn btn-primary btn-sm px-3" (click)="resetFilters()">
                <i class="bi bi-arrow-counterclockwise me-1"></i> Clear Filters
              </button>
            }
          </div>
        </div>
      } @else {
        <!-- Quizzes Grid View -->
        <div class="row g-4 mb-4">
          @for (quiz of quizzes(); track quiz.id) {
            <div class="col-12 col-md-6 col-xl-4">
              <div class="card border-0 shadow-sm h-100 card-hover d-flex flex-column">
                <div class="card-body p-4 flex-grow-1">
                  <!-- Category & Status Badge -->
                  <div class="d-flex justify-content-between align-items-start mb-2">
                    <span class="badge bg-primary-subtle text-primary text-uppercase px-2 py-1">
                      {{ quiz.categoryName }}
                    </span>
                    @if (quiz.hasActiveAttempt || quiz.status === 'Resume') {
                      <span class="badge bg-warning text-dark d-flex align-items-center gap-1">
                        <i class="bi bi-play-circle-fill"></i> Resume
                      </span>
                    } @else if (quiz.status === 'Expired') {
                      <span class="badge bg-danger d-flex align-items-center gap-1">
                        <i class="bi bi-clock-history"></i> Expired
                      </span>
                    } @else if (quiz.status === 'Attempted') {
                      <span class="badge bg-secondary d-flex align-items-center gap-1">
                        <i class="bi bi-check-circle"></i> Attempted
                      </span>
                    } @else if (quiz.status === 'Upcoming') {
                      <span class="badge bg-warning text-dark border d-flex align-items-center gap-1">
                        <i class="bi bi-calendar-event"></i> Upcoming
                      </span>
                    } @else {
                      <span class="badge bg-success-subtle text-success d-flex align-items-center gap-1">
                        <i class="bi bi-unlock-fill"></i> Open
                      </span>
                    }
                  </div>

                  <h5 class="fw-bold text-dark mb-2">{{ quiz.title }}</h5>
                  @if (quiz.description) {
                    <p class="text-muted small mb-3 text-truncate-2" style="min-height: 40px;">
                      {{ quiz.description }}
                    </p>
                  }

                  <!-- Key Stats Grid -->
                  <div class="row g-2 py-2 mb-3 bg-light rounded-3 text-center small">
                    <div class="col-4 border-end">
                      <div class="text-muted" style="font-size: 0.75rem;">Duration</div>
                      <div class="fw-bold text-dark">{{ quiz.durationMinutes }} mins</div>
                    </div>
                    <div class="col-4 border-end">
                      <div class="text-muted" style="font-size: 0.75rem;">Questions</div>
                      <div class="fw-bold text-dark">{{ quiz.questionCount }}</div>
                    </div>
                    <div class="col-4">
                      <div class="text-muted" style="font-size: 0.75rem;">Passing</div>
                      <div class="fw-bold text-success">{{ quiz.passingPercent }}%</div>
                    </div>
                  </div>

                  <!-- Schedule & Attempts Details -->
                  <div class="d-flex justify-content-between text-muted small mb-1">
                    <span>Attempts Allowed:</span>
                    <span class="fw-semibold text-dark">
                      {{ quiz.attemptsUsed }} / {{ quiz.allowedAttempts ?? quiz.maxAttempts }} ({{ quiz.attemptsRemaining }} left)
                    </span>
                  </div>
                  <div class="d-flex justify-content-between text-muted small mb-1">
                    <span>Window:</span>
                    <span class="text-dark">{{ quiz.startAtUtc | date:'short' }} - {{ quiz.endAtUtc | date:'short' }}</span>
                  </div>
                </div>

                <!-- Card Action Footer -->
                <div class="card-footer bg-white border-top-0 p-4 pt-0">
                  @if (quiz.hasActiveAttempt && quiz.activeAttemptId) {
                    <!-- Resume Active Attempt Button -->
                    <button 
                      class="btn btn-warning w-100 py-2 fw-semibold d-flex align-items-center justify-content-center gap-2" 
                      [disabled]="isStartingQuizId() === quiz.id"
                      (click)="resumeQuiz(quiz.activeAttemptId)">
                      <i class="bi bi-play-circle-fill fs-5"></i>
                      <span>Resume Assessment</span>
                    </button>
                  } @else if (quiz.status === 'Expired') {
                    <!-- Expired status: Disabled button with tooltip -->
                    <button 
                      class="btn btn-danger w-100 py-2 disabled" 
                      disabled 
                      title="This quiz has expired because its schedule ended and you did not attempt it.">
                      <i class="bi bi-clock-history me-1"></i> Expired (Window Closed)
                    </button>
                  } @else if (quiz.status === 'Upcoming') {
                    <!-- Upcoming schedule button with countdown -->
                    <button class="btn btn-outline-secondary w-100 py-2 disabled" disabled>
                      <i class="bi bi-clock me-1"></i> Starts in {{ getStartsInCountdown(quiz.startAtUtc) }}
                    </button>
                  } @else if (quiz.status === 'Attempted' || quiz.attemptsRemaining <= 0) {
                    <!-- Attempts Exhausted: Reattempt Request section -->
                    <div class="d-flex flex-column gap-2">
                      @if (quiz.reattemptStatus === 'Pending') {
                        <button class="btn btn-secondary w-100 py-2 disabled" disabled title="Reattempt request is currently being reviewed by an administrator">
                          <i class="bi bi-hourglass-split me-1"></i> Reattempt Pending Review
                        </button>
                      } @else if (quiz.reattemptStatus === 'Approved') {
                        <div class="alert alert-success py-1 px-2 mb-0 small text-center">
                          <i class="bi bi-check-circle me-1"></i> Reattempt Approved!
                        </div>
                        <button 
                          class="btn btn-primary w-100 py-2 fw-semibold" 
                          (click)="confirmStartQuiz(quiz)">
                          <i class="bi bi-pencil-square me-1"></i> Start Reattempt
                        </button>
                      } @else if (quiz.reattemptStatus === 'Rejected') {
                        <div class="alert alert-danger py-1 px-2 mb-0 small text-center">
                          <i class="bi bi-x-circle me-1"></i> Previous Reattempt Rejected
                        </div>
                        <button 
                          class="btn btn-outline-primary w-100 py-2 fw-semibold" 
                          (click)="openReattemptModal(quiz)">
                          <i class="bi bi-arrow-repeat me-1"></i> Request Reattempt Again
                        </button>
                      } @else {
                        <button 
                          class="btn btn-outline-primary w-100 py-2 fw-semibold" 
                          (click)="openReattemptModal(quiz)">
                          <i class="bi bi-arrow-repeat me-1"></i> Request Reattempt
                        </button>
                      }
                    </div>
                  } @else {
                    <!-- Start Quiz Button with double-click protection -->
                    <button 
                      class="btn btn-primary w-100 py-2 fw-semibold d-flex align-items-center justify-content-center gap-2" 
                      [disabled]="isStartingQuizId() !== null" 
                      (click)="confirmStartQuiz(quiz)">
                      @if (isStartingQuizId() === quiz.id) {
                        <span class="spinner-border spinner-border-sm" role="status"></span>
                        <span>Starting...</span>
                      } @else {
                        <i class="bi bi-pencil-square"></i>
                        <span>Start Quiz</span>
                      }
                    </button>
                  }
                </div>
              </div>
            </div>
          }
        </div>

        <!-- Pagination Controls -->
        @if (totalPages() > 1) {
          <div class="d-flex flex-column flex-sm-row justify-content-between align-items-center gap-3 pt-3 border-top">
            <span class="text-muted small">
              Page {{ pageNumber() }} of {{ totalPages() }} ({{ totalCount() }} total assessments)
            </span>
            <ul class="pagination pagination-sm mb-0">
              <li class="page-item" [class.disabled]="pageNumber() <= 1">
                <button class="page-link" (click)="onPageChange(pageNumber() - 1)" [disabled]="pageNumber() <= 1">
                  Previous
                </button>
              </li>
              @for (p of displayedPages(); track p) {
                <li class="page-item" [class.active]="p === pageNumber()">
                  <button class="page-link" (click)="onPageChange(p)">{{ p }}</button>
                </li>
              }
              <li class="page-item" [class.disabled]="pageNumber() >= totalPages()">
                <button class="page-link" (click)="onPageChange(pageNumber() + 1)" [disabled]="pageNumber() >= totalPages()">
                  Next
                </button>
              </li>
            </ul>
          </div>
        }
      }

      <!-- Reattempt Request Modal Overlay -->
      @if (activeReattemptQuiz()) {
        <div class="modal fade show d-block" tabindex="-1" style="background-color: rgba(0,0,0,0.5);">
          <div class="modal-dialog modal-dialog-centered">
            <div class="modal-content border-0 shadow">
              <div class="modal-header border-bottom">
                <h5 class="modal-title fw-bold">Request Reattempt</h5>
                <button type="button" class="btn-close" (click)="closeReattemptModal()"></button>
              </div>
              <div class="modal-body p-4">
                <p class="mb-3 text-muted small">
                  Quiz: <strong class="text-dark">{{ activeReattemptQuiz()!.title }}</strong>
                </p>
                <div class="mb-3">
                  <label class="form-label fw-semibold">Reason for Reattempt <span class="text-danger">*</span></label>
                  <textarea 
                    class="form-control" 
                    rows="4" 
                    placeholder="Describe why you need another attempt (e.g. computer crash, network disconnect, power disruption)..."
                    [(ngModel)]="reattemptReason">
                  </textarea>
                  @if (reattemptError()) {
                    <div class="text-danger small mt-1">{{ reattemptError() }}</div>
                  }
                </div>
              </div>
              <div class="modal-footer border-top">
                <button type="button" class="btn btn-outline-secondary" (click)="closeReattemptModal()">Cancel</button>
                <button 
                  type="button" 
                  class="btn btn-primary" 
                  [disabled]="isSubmittingReattempt() || !reattemptReason.trim()" 
                  (click)="submitReattemptRequest()">
                  @if (isSubmittingReattempt()) {
                    <span class="spinner-border spinner-border-sm me-1"></span> Submitting...
                  } @else {
                    <i class="bi bi-send me-1"></i> Submit Request
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
    .text-truncate-2 {
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .card-hover {
      transition: transform 0.2s ease, box-shadow 0.2s ease;
    }
    .card-hover:hover {
      transform: translateY(-2px);
      box-shadow: 0 .5rem 1rem rgba(0,0,0,.1) !important;
    }
  `]
})
export class QuizListComponent implements OnInit, OnDestroy {
  private quizService = inject(QuizService);
  private confirmService = inject(ConfirmDialogService);
  private toastService = inject(ToastService);
  private router = inject(Router);
  private timeService = inject(TimeService);

  quizzes = signal<AvailableQuizDto[]>([]);
  categories = signal<CategoryOptionDto[]>([]);
  isLoading = signal(true);
  isStartingQuizId = signal<string | null>(null);
  errorMessage = signal<string | null>(null);

  searchTerm = signal('');
  selectedCategoryId = signal('');
  selectedStatus = signal('All');
  pageNumber = signal(1);
  pageSize = signal(6);
  totalCount = signal(0);

  currentTime = signal(Date.now());
  private tickTimer: any;

  // Reattempt Modal State
  activeReattemptQuiz = signal<AvailableQuizDto | null>(null);
  reattemptReason = '';
  isSubmittingReattempt = signal(false);
  reattemptError = signal<string | null>(null);

  totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()) || 1);
  displayedPages = computed(() => {
    const total = this.totalPages();
    const current = this.pageNumber();
    const pages: number[] = [];
    const start = Math.max(1, current - 2);
    const end = Math.min(total, current + 2);
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  });

  ngOnInit(): void {
    this.loadCategories();
    this.loadQuizzes();
    this.startCountdownTimer();
  }

  ngOnDestroy(): void {
    if (this.tickTimer) {
      clearInterval(this.tickTimer);
    }
  }

  private startCountdownTimer(): void {
    this.tickTimer = setInterval(() => {
      this.currentTime.set(Date.now());
      this.checkQuizSchedules();
    }, 1000);
  }

  private checkQuizSchedules(): void {
    let changed = false;
    const currentList = this.quizzes();
    const now = this.timeService.nowUtc().getTime();

    for (const quiz of currentList) {
      const startMs = this.timeService.parseUtcTimestamp(quiz.startAtUtc);
      const endMs = this.timeService.parseUtcTimestamp(quiz.endAtUtc);

      if (quiz.status === 'Upcoming' && !isNaN(startMs) && now >= startMs) {
        if (!isNaN(endMs) && now > endMs && quiz.attemptsUsed === 0) {
          quiz.status = 'Expired';
        } else {
          quiz.status = 'Open';
        }
        changed = true;
      } else if (quiz.status === 'Open' && !isNaN(endMs) && now > endMs && quiz.attemptsUsed === 0) {
        quiz.status = 'Expired';
        changed = true;
      }
    }

    if (changed) {
      this.quizzes.set([...currentList]);
    }
  }

  formatHms(seconds: number): string {
    if (seconds <= 0) return '00:00:00';
    const hrs = Math.floor(seconds / 3600);
    const mins = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;
    const pad = (n: number) => n.toString().padStart(2, '0');
    return `${pad(hrs)}:${pad(mins)}:${pad(secs)}`;
  }

  getStartsInCountdown(startAtUtc: string): string {
    this.currentTime(); // read for reactivity every second
    const remainingSec = this.timeService.getRemainingSeconds(startAtUtc);
    return this.formatHms(remainingSec);
  }

  loadCategories(): void {
    this.quizService.getCategories().subscribe({
      next: (cats) => this.categories.set(cats || []),
      error: () => this.categories.set([])
    });
  }

  loadQuizzes(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.quizService.getAvailableQuizzes(
      this.pageNumber(),
      this.pageSize(),
      this.searchTerm(),
      this.selectedCategoryId(),
      undefined,
      undefined,
      this.selectedStatus()
    ).subscribe({
      next: (res) => {
        this.quizzes.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set(err.error?.message || 'Unable to load available quizzes. Please try again.');
      }
    });
  }

  onSearchChange(term: string): void {
    this.searchTerm.set(term);
    this.pageNumber.set(1);
    this.loadQuizzes();
  }

  clearSearch(): void {
    this.searchTerm.set('');
    this.pageNumber.set(1);
    this.loadQuizzes();
  }

  onCategoryChange(catId: string): void {
    this.selectedCategoryId.set(catId);
    this.pageNumber.set(1);
    this.loadQuizzes();
  }

  onStatusChange(status: string): void {
    this.selectedStatus.set(status);
    this.pageNumber.set(1);
    this.loadQuizzes();
  }

  resetFilters(): void {
    this.searchTerm.set('');
    this.selectedCategoryId.set('');
    this.selectedStatus.set('All');
    this.pageNumber.set(1);
    this.loadQuizzes();
  }

  onPageChange(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.pageNumber.set(page);
      this.loadQuizzes();
    }
  }

  resumeQuiz(attemptId: string): void {
    this.router.navigate(['/user/attempts', attemptId, 'play']);
  }

  async confirmStartQuiz(quiz: AvailableQuizDto): Promise<void> {
    if (this.isStartingQuizId() !== null) return; // Double-click protection

    const confirmed = await this.confirmService.confirm({
      title: `Start Quiz: ${quiz.title}`,
      message: `You are about to start this timed exam. You will have ${quiz.durationMinutes} minutes to complete ${quiz.questionCount} questions. The timer will start immediately and cannot be paused. Are you ready?`,
      confirmText: 'Begin Exam',
      cancelText: 'Cancel'
    });

    if (confirmed) {
      this.isStartingQuizId.set(quiz.id);
      this.quizService.startQuiz(quiz.id).subscribe({
        next: (startRes) => {
          this.isStartingQuizId.set(null);
          this.toastService.showSuccess('Quiz started! Good luck!', 'Exam Started');
          this.router.navigate(['/user/attempts', startRes.attemptId, 'play']);
        },
        error: (err) => {
          this.isStartingQuizId.set(null);
          this.toastService.showError(err.error?.message || 'Unable to start quiz.', 'Error');
        }
      });
    }
  }

  openReattemptModal(quiz: AvailableQuizDto): void {
    this.activeReattemptQuiz.set(quiz);
    this.reattemptReason = '';
    this.reattemptError.set(null);
  }

  closeReattemptModal(): void {
    this.activeReattemptQuiz.set(null);
    this.reattemptReason = '';
    this.reattemptError.set(null);
  }

  submitReattemptRequest(): void {
    const quiz = this.activeReattemptQuiz();
    if (!quiz) return;

    if (!this.reattemptReason.trim()) {
      this.reattemptError.set('Please provide a reason for the reattempt request.');
      return;
    }

    this.isSubmittingReattempt.set(true);
    this.reattemptError.set(null);

    this.quizService.requestReattempt(quiz.id, this.reattemptReason.trim()).subscribe({
      next: () => {
        this.isSubmittingReattempt.set(false);
        this.toastService.showSuccess('Reattempt request submitted successfully. An administrator will review your request.', 'Request Submitted');
        this.closeReattemptModal();
        this.loadQuizzes();
      },
      error: (err) => {
        this.isSubmittingReattempt.set(false);
        this.reattemptError.set(err.error?.message || 'Failed to submit reattempt request. Please try again.');
        this.toastService.showError(err.error?.message || 'Failed to submit request.', 'Error');
      }
    });
  }
}
