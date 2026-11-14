import { Component, inject, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule, RouterLink } from '@angular/router';
import { AttemptService } from '../../../../core/services/attempt.service';
import { ToastService } from '../../../../core/services/toast.service';
import { AttemptReviewDto, OptionReviewDto } from '../../../../core/models/report.models';

@Component({
  selector: 'app-attempt-review',
  standalone: true,
  imports: [CommonModule, RouterModule, RouterLink],
  template: `
    <div class="container-fluid">
      <!-- Title & Back link -->
      <div class="d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3 mb-4">
        <div>
          <a routerLink="/user/attempts" class="text-decoration-none text-muted small d-inline-flex align-items-center gap-1 mb-1">
            <i class="bi bi-arrow-left"></i> Back to Attempts History
          </a>
          <h2 class="fw-bold mb-0">Examination Result & Review</h2>
        </div>
        @if (review()?.passed) {
          <button 
            type="button" 
            class="btn btn-success d-flex align-items-center gap-2 px-3 py-2" 
            [disabled]="isDownloadingCert()"
            (click)="downloadCertificate()">
            @if (isDownloadingCert()) {
              <span class="spinner-border spinner-border-sm me-1" role="status"></span>
            } @else {
              <i class="bi bi-award-fill fs-5"></i>
            }
            <span class="fw-semibold">Download Certificate (PDF)</span>
          </button>
        }
      </div>

      <!-- Loading State -->
      @if (isLoading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">Loading review...</span>
          </div>
          <p class="text-muted mt-2">Loading assessment details...</p>
        </div>
      } @else if (isInProgressError()) {
        <!-- Attempt Still In Progress Guard -->
        <div class="card border-0 shadow-sm text-center py-5">
          <div class="card-body">
            <i class="bi bi-clock-history fs-1 text-warning d-block mb-3"></i>
            <h4 class="fw-bold">Assessment In Progress</h4>
            <p class="text-muted mb-4">
              Detailed question review and scores are only accessible after your exam has been submitted.
            </p>
            <a [routerLink]="['/user/attempts', attemptId, 'play']" class="btn btn-primary px-4 py-2">
              <i class="bi bi-play-circle-fill me-1"></i> Continue / Resume Exam
            </a>
          </div>
        </div>
      } @else if (review(); as r) {
        <!-- Pending Scoring Notice Banner -->
        @if (isScoringPending()) {
          <div class="alert alert-info border-0 shadow-sm d-flex align-items-center gap-3 mb-4 p-3 rounded-3" role="alert">
            <div class="spinner-border spinner-border-sm text-primary flex-shrink-0" role="status"></div>
            <div class="flex-grow-1">
              <strong class="d-block mb-1">Result is being calculated...</strong>
              <span class="small">The automated scoring engine is finalizing your answers and marks. This page will automatically update in real-time.</span>
            </div>
            <button class="btn btn-sm btn-outline-primary ms-auto" (click)="loadReview(false)">
              <i class="bi bi-arrow-clockwise me-1"></i> Refresh Now
            </button>
          </div>
        }

        <!-- Summary Score Banner Card -->
        <div class="card border-0 shadow-sm mb-4">
          <div class="card-body p-4">
            <div class="row align-items-center g-4">
              <!-- Result Icon & Status -->
              <div class="col-12 col-md-4 text-center border-end-md">
                @if (isScoringPending()) {
                  <div class="d-inline-flex align-items-center justify-content-center rounded-circle p-4 mb-2 shadow-sm bg-warning-subtle text-warning" style="width: 90px; height: 90px;">
                    <i class="bi bi-hourglass-split fs-1"></i>
                  </div>
                  <h3 class="fw-bold mb-0 text-warning">CALCULATING</h3>
                  <span class="badge bg-warning text-dark mt-1">{{ r.status }}</span>
                } @else {
                  <div class="d-inline-flex align-items-center justify-content-center rounded-circle p-4 mb-2 shadow-sm"
                    [ngClass]="r.passed ? 'bg-success-subtle text-success' : 'bg-danger-subtle text-danger'" 
                    style="width: 90px; height: 90px;">
                    <i class="bi fs-1" [ngClass]="r.passed ? 'bi-check-circle-fill' : 'bi-x-circle-fill'"></i>
                  </div>
                  <h3 class="fw-bold mb-0" [ngClass]="r.passed ? 'text-success' : 'text-danger'">
                    {{ r.passed ? 'PASSED' : 'FAILED' }}
                  </h3>
                  <span class="badge mt-1" [ngClass]="r.passed ? 'bg-success' : 'bg-danger'">
                    {{ r.status }}
                  </span>
                }
              </div>

              <!-- Score Details -->
              <div class="col-12 col-md-8">
                <h4 class="fw-bold text-dark mb-3">{{ r.quizTitle }}</h4>
                <div class="row g-3">
                  <div class="col-6 col-sm-3">
                    <div class="text-muted small">Score Achieved</div>
                    <h5 class="fw-bold mb-0">
                      {{ r.score !== null && r.score !== undefined ? r.score : '--' }} / {{ r.totalMarks }}
                    </h5>
                  </div>
                  <div class="col-6 col-sm-3">
                    <div class="text-muted small">Percentage</div>
                    <h5 class="fw-bold mb-0">
                      {{ r.percent !== null && r.percent !== undefined ? (r.percent | number:'1.1-1') + '%' : '--%' }}
                    </h5>
                  </div>
                  <div class="col-6 col-sm-3">
                    <div class="text-muted small">Tab Switches</div>
                    <h5 class="fw-bold mb-0" [class.text-warning]="r.tabSwitchCount > 0">
                      {{ r.tabSwitchCount }}
                    </h5>
                  </div>
                  <div class="col-6 col-sm-3">
                    <div class="text-muted small">Submitted At</div>
                    <h6 class="fw-bold mb-0 text-muted">
                      {{ r.submittedAtUtc ? (r.submittedAtUtc | date:'shortTime') : '--' }}
                    </h6>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Question Breakdown Header -->
        <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
          <h5 class="fw-bold mb-0 d-flex align-items-center gap-2">
            <i class="bi bi-list-check text-primary"></i>
            <span>Detailed Question Review ({{ r.questions.length }} Questions)</span>
          </h5>
          <!-- Language Switcher -->
          <div class="btn-group btn-group-sm shadow-sm" role="group" aria-label="Language Toggle">
            <button 
              type="button" 
              class="btn py-1 px-3 fw-semibold" 
              [ngClass]="selectedLanguage() === 'en' ? 'btn-primary' : 'btn-outline-secondary'"
              (click)="setLanguage('en')">
              English
            </button>
            <button 
              type="button" 
              class="btn py-1 px-3 fw-semibold" 
              [ngClass]="selectedLanguage() === 'hi' ? 'btn-primary' : 'btn-outline-secondary'"
              (click)="setLanguage('hi')">
              हिंदी
            </button>
          </div>
        </div>

        <!-- Questions List -->
        <div class="d-flex flex-column gap-3 mb-5">
          @for (q of r.questions; track q.questionId; let idx = $index) {
            <div class="card border-0 shadow-sm">
              <div class="card-header bg-white py-3 border-bottom d-flex justify-content-between align-items-center">
                <div class="d-flex align-items-center gap-2">
                  <span class="badge rounded-pill" [ngClass]="q.isCorrect ? 'bg-success' : 'bg-danger'">
                    Q{{ idx + 1 }}
                  </span>
                  <span class="badge bg-light text-secondary border">
                    {{ q.type }}
                  </span>
                </div>
                <div class="fw-semibold small">
                  Marks: 
                  <span [ngClass]="q.isCorrect ? 'text-success' : 'text-danger'">
                    {{ q.marksAwarded }}
                  </span> / {{ q.marks }}
                </div>
              </div>

              <div class="card-body p-4">
                <h6 class="fw-semibold text-dark mb-3 lh-base">{{ getQuestionText(q) }}</h6>

                <!-- Options Review -->
                <div class="d-flex flex-column gap-2">
                  @for (opt of q.options; track opt.optionId) {
                    <div 
                      class="option-review-card p-3 rounded-3 border d-flex align-items-center justify-content-between"
                      [ngClass]="getOptionClass(opt)">
                      
                      <div class="d-flex align-items-center gap-3">
                        <i class="bi fs-5" [ngClass]="getOptionIcon(opt)"></i>
                        <span class="text-dark">{{ getOptionText(opt) }}</span>
                      </div>

                      <div>
                        @if (opt.isCorrect && isStudentSelected(opt)) {
                          <span class="badge bg-success">
                            <i class="bi bi-check-lg me-1"></i> Correct Choice
                          </span>
                        } @else if (opt.isCorrect && !isStudentSelected(opt)) {
                          <span class="badge bg-success-subtle text-success border border-success">
                            Correct Answer
                          </span>
                        } @else if (!opt.isCorrect && isStudentSelected(opt)) {
                          <span class="badge bg-danger">
                            <i class="bi bi-x-lg me-1"></i> Your Incorrect Choice
                          </span>
                        }
                      </div>
                    </div>
                  }
                </div>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    @media (min-width: 768px) {
      .border-end-md {
        border-right: 1px solid #e2e8f0;
      }
    }
    .option-review-card.correct-selected {
      background-color: #ecfdf5;
      border-color: #10b981 !important;
    }
    .option-review-card.correct-unselected {
      background-color: #f0fdf4;
      border-color: #86efac !important;
    }
    .option-review-card.incorrect-selected {
      background-color: #fef2f2;
      border-color: #ef4444 !important;
    }
    .option-review-card.neutral {
      background-color: #fff;
    }
  `]
})
export class AttemptReviewComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private attemptService = inject(AttemptService);
  private toastService = inject(ToastService);

  attemptId: string = '';
  review = signal<AttemptReviewDto | null>(null);
  isLoading = signal(true);
  isInProgressError = signal(false);
  isDownloadingCert = signal(false);
  selectedLanguage = signal<'en' | 'hi'>((typeof localStorage !== 'undefined' && localStorage.getItem('pushpak_exam_lang') === 'hi') ? 'hi' : 'en');

  setLanguage(lang: 'en' | 'hi'): void {
    this.selectedLanguage.set(lang);
    try {
      localStorage.setItem('pushpak_exam_lang', lang);
    } catch { }
  }

  getQuestionText(q: any): string {
    if (!q) return '';
    if (this.selectedLanguage() === 'hi') {
      return (q.textHi && q.textHi.trim().length > 0) ? q.textHi : (q.textEn || q.text || '');
    }
    return (q.textEn && q.textEn.trim().length > 0) ? q.textEn : (q.text || q.textHi || '');
  }

  getOptionText(opt: any): string {
    if (!opt) return '';
    if (this.selectedLanguage() === 'hi') {
      return (opt.textHi && opt.textHi.trim().length > 0) ? opt.textHi : (opt.textEn || opt.text || '');
    }
    return (opt.textEn && opt.textEn.trim().length > 0) ? opt.textEn : (opt.text || opt.textHi || '');
  }

  // If scoring is pending (flag from backend or null score), mark as pending
  isScoringPending = computed(() => {
    const r = this.review();
    if (!r) return false;
    return r.isScoringPending === true || r.score === null || r.score === undefined;
  });

  private pollingInterval: any = null;

  ngOnInit(): void {
    this.attemptId = this.route.snapshot.params['id'];
    this.loadReview(true);
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  loadReview(showInitialSpinner: boolean = true): void {
    if (showInitialSpinner) {
      this.isLoading.set(true);
    }

    this.attemptService.getReview(this.attemptId).subscribe({
      next: (res) => {
        this.review.set(res);
        this.isLoading.set(false);
        this.isInProgressError.set(false);

        // Check if scoring is pending
        if (this.isScoringPending()) {
          this.startPolling();
        } else {
          this.stopPolling();
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        if (err.status === 400 && err.error?.message?.includes('after submission')) {
          this.isInProgressError.set(true);
        } else {
          this.toastService.showError(err.error?.message || 'Unable to load attempt review.', 'Error');
        }
        this.stopPolling();
      }
    });
  }

  private startPolling(): void {
    if (this.pollingInterval) return;
    this.pollingInterval = setInterval(() => {
      this.attemptService.getReview(this.attemptId).subscribe({
        next: (res) => {
          this.review.set(res);
          if (!this.isScoringPending()) {
            this.stopPolling();
            this.toastService.showSuccess('Assessment evaluation finalized!', 'Grading Complete');
          }
        },
        error: () => this.stopPolling()
      });
    }, 2000);
  }

  private stopPolling(): void {
    if (this.pollingInterval) {
      clearInterval(this.pollingInterval);
      this.pollingInterval = null;
    }
  }

  isStudentSelected(opt: OptionReviewDto): boolean {
    return !!(opt.isSelected || opt.isSelectedByStudent);
  }

  getOptionClass(opt: OptionReviewDto): string {
    const selected = this.isStudentSelected(opt);
    if (opt.isCorrect && selected) return 'correct-selected';
    if (opt.isCorrect && !selected) return 'correct-unselected';
    if (!opt.isCorrect && selected) return 'incorrect-selected';
    return 'neutral';
  }

  getOptionIcon(opt: OptionReviewDto): string {
    const selected = this.isStudentSelected(opt);
    if (opt.isCorrect && selected) return 'bi-check-circle-fill text-success';
    if (opt.isCorrect && !selected) return 'bi-check-circle text-success';
    if (!opt.isCorrect && selected) return 'bi-x-circle-fill text-danger';
    return 'bi-circle text-muted';
  }

  downloadCertificate(): void {
    if (this.isDownloadingCert()) return;

    this.isDownloadingCert.set(true);
    this.attemptService.downloadCertificate(this.attemptId).subscribe({
      next: (blob) => {
        this.isDownloadingCert.set(false);
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `PushpakClasses_Certificate_${this.attemptId}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.toastService.showSuccess('Official certificate downloaded successfully!', 'Download Complete');
      },
      error: () => {
        this.isDownloadingCert.set(false);
        this.toastService.showError('Unable to download certificate.', 'Error');
      }
    });
  }
}
