import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule, RouterLink } from '@angular/router';
import { AdminService } from '../../../../core/services/admin.service';
import { AttemptReviewDto, QuestionReviewDto, OptionReviewDto } from '../../../../core/models/report.models';

@Component({
  selector: 'app-admin-attempt-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, RouterLink],
  template: `
    <div class="container-fluid">
      <!-- Breadcrumb & Header -->
      <div class="d-flex flex-wrap justify-content-between align-items-center mb-4 gap-3">
        <div>
          <nav aria-label="breadcrumb">
            <ol class="breadcrumb mb-1 small">
              <li class="breadcrumb-item"><a routerLink="/admin/dashboard" class="text-decoration-none">Dashboard</a></li>
              <li class="breadcrumb-item"><a routerLink="/admin/attempts" class="text-decoration-none">Attempts</a></li>
              <li class="breadcrumb-item active" aria-current="page">Attempt Review</li>
            </ol>
          </nav>
          <h2 class="fw-bold mb-0">
            <i class="bi bi-file-earmark-check-fill text-primary me-2"></i>Candidate Attempt Review
          </h2>
        </div>
        <div class="d-flex gap-2">
          <a routerLink="/admin/attempts" class="btn btn-outline-secondary d-flex align-items-center gap-1">
            <i class="bi bi-arrow-left"></i>
            <span>Back to Attempts</span>
          </a>
        </div>
      </div>

      <!-- Loading / Error States -->
      @if (isLoading()) {
        <div class="card border-0 shadow-sm py-5 text-center mb-4">
          <div class="card-body">
            <div class="spinner-border text-primary mb-3" role="status"></div>
            <p class="text-muted small mb-0">Loading attempt review and answer sheet...</p>
          </div>
        </div>
      } @else if (error()) {
        <div class="alert alert-danger shadow-sm mb-4" role="alert">
          <i class="bi bi-exclamation-triangle-fill me-2"></i>{{ error() }}
        </div>
      } @else if (review()) {
        <!-- Candidate Summary Card -->
        <div class="card border-0 shadow-sm mb-4">
          <div class="card-body p-4">
            <div class="row g-4 align-items-center">
              <div class="col-12 col-md-4 border-end-md">
                <div class="text-muted small">Candidate Name & Email</div>
                <h5 class="fw-bold text-dark mb-1">{{ review()!.userFullName }}</h5>
                <div class="font-monospace small text-muted">{{ review()!.userEmail }}</div>
                <div class="mt-2 text-muted small">
                  <i class="bi bi-clock text-primary me-1"></i>
                  Submitted: {{ review()!.submittedAtUtc | date:'medium':'UTC' }}
                </div>
              </div>

              <div class="col-12 col-md-4 border-end-md">
                <div class="text-muted small">Quiz Title</div>
                <h5 class="fw-bold text-dark mb-1">{{ review()!.quizTitle }}</h5>
                <div class="d-flex align-items-center gap-2 mt-2">
                  <span class="badge" [ngClass]="review()!.passed ? 'bg-success' : 'bg-danger'">
                    <i class="bi" [ngClass]="review()!.passed ? 'bi-check-circle-fill' : 'bi-x-circle-fill'"></i>
                    {{ review()!.passed ? 'Passed Examination' : 'Did Not Pass' }}
                  </span>
                  <span class="badge bg-secondary-subtle text-secondary">{{ review()!.status }}</span>
                </div>
              </div>

              <div class="col-12 col-md-4">
                <div class="d-flex justify-content-between align-items-center mb-1">
                  <span class="text-muted small">Score Achieved:</span>
                  <span class="fw-bold fs-5 text-dark">
                    {{ review()!.score }} / {{ review()!.totalMarks }} Marks
                  </span>
                </div>
                <div class="d-flex justify-content-between align-items-center mb-2">
                  <span class="text-muted small">Percentage:</span>
                  <span class="fw-bold text-primary">{{ review()!.percent | number:'1.0-1' }}%</span>
                </div>
                <div class="d-flex justify-content-between align-items-center">
                  <span class="text-muted small">Anti-Cheat Tab Switches:</span>
                  @if (review()!.tabSwitchCount > 0) {
                    <span class="badge bg-danger-subtle text-danger">
                      <i class="bi bi-exclamation-octagon-fill me-1"></i>{{ review()!.tabSwitchCount }} Switches
                    </span>
                  } @else {
                    <span class="badge bg-success-subtle text-success">
                      <i class="bi bi-shield-check me-1"></i> Clean (0)
                    </span>
                  }
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Question Breakdown Section -->
        <div class="card border-0 shadow-sm">
          <div class="card-header bg-white border-0 py-3">
            <h5 class="fw-bold mb-0 text-dark">
              <i class="bi bi-list-check text-primary me-2"></i>Answer Sheet & Evaluation ({{ review()!.questions.length }} Questions)
            </h5>
          </div>

          <div class="card-body p-0">
            <div class="list-group list-group-flush">
              @for (q of review()!.questions; track q.questionId; let qIdx = $index) {
                <div class="list-group-item p-3 p-md-4">
                  <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
                    <div class="d-flex align-items-center gap-2">
                      <span class="badge bg-secondary rounded-pill">#{{ qIdx + 1 }}</span>
                      <span class="badge" [ngClass]="q.isCorrect ? 'bg-success-subtle text-success' : 'bg-danger-subtle text-danger'">
                        <i class="bi" [ngClass]="q.isCorrect ? 'bi-check-circle-fill' : 'bi-x-circle-fill'"></i>
                        {{ q.isCorrect ? 'Correct' : 'Incorrect' }}
                      </span>
                    </div>
                    <div class="fw-bold small">
                      Marks: <span [ngClass]="q.isCorrect ? 'text-success' : 'text-danger'">{{ q.marksAwarded }}</span> / {{ q.marks }}
                    </div>
                  </div>

                  <!-- Question Text -->
                  <h6 class="fw-bold mb-3 text-dark">{{ q.text }}</h6>

                  <!-- Options Review List -->
                  <div class="d-flex flex-column gap-2">
                    @for (opt of q.options; track opt.optionId; let optIdx = $index) {
                      <div 
                        class="p-3 rounded border d-flex align-items-center justify-content-between"
                        [ngClass]="getOptionClass(opt)">
                        <div class="d-flex align-items-center gap-2">
                          <span class="fw-bold small">{{ getLetter(optIdx) }}.</span>
                          <span>{{ opt.text }}</span>
                        </div>
                        <div class="d-flex align-items-center gap-2">
                          @if (opt.isSelected && opt.isCorrect) {
                            <span class="badge bg-success">
                              <i class="bi bi-check-lg me-1"></i> Correct Selection
                            </span>
                          } @else if (opt.isSelected && !opt.isCorrect) {
                            <span class="badge bg-danger">
                              <i class="bi bi-x-lg me-1"></i> Candidate Selected (Incorrect)
                            </span>
                          } @else if (!opt.isSelected && opt.isCorrect) {
                            <span class="badge bg-success-subtle text-success border border-success">
                              <i class="bi bi-check-circle me-1"></i> Correct Answer
                            </span>
                          }
                        </div>
                      </div>
                    }
                  </div>
                </div>
              }
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class AdminAttemptDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private adminService = inject(AdminService);

  review = signal<AttemptReviewDto | null>(null);
  isLoading = signal<boolean>(true);
  error = signal<string | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadReview(id);
    } else {
      this.error.set('No attempt ID provided.');
      this.isLoading.set(false);
    }
  }

  loadReview(attemptId: string): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.adminService.getAttemptReview(attemptId).subscribe({
      next: (data) => {
        this.review.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.message || 'Failed to load attempt review.');
        this.isLoading.set(false);
      }
    });
  }

  getLetter(index: number): string {
    return String.fromCharCode(65 + index);
  }

  getOptionClass(opt: OptionReviewDto): string {
    if (opt.isSelected && opt.isCorrect) {
      return 'border-success bg-success-subtle text-success-emphasis';
    }
    if (opt.isSelected && !opt.isCorrect) {
      return 'border-danger bg-danger-subtle text-danger-emphasis';
    }
    if (!opt.isSelected && opt.isCorrect) {
      return 'border-success bg-light text-success-emphasis';
    }
    return 'bg-light text-dark';
  }
}
