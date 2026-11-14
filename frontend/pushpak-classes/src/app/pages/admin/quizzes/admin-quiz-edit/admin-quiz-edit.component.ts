import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, FormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule, RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { AdminService } from '../../../../core/services/admin.service';
import { ToastService } from '../../../../core/services/toast.service';
import {
  AdminCategoryDto,
  AdminQuizDto,
  AdminQuestionDto,
  AdminQuestionType,
  AdminOptionDto,
  CreateQuestionRequestDto,
  UpdateQuestionRequestDto
} from '../../../../core/models/admin.models';

@Component({
  selector: 'app-admin-quiz-edit',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterModule, RouterLink],
  template: `
    <div class="container-fluid">
      <!-- Breadcrumb & Header -->
      <div class="d-flex flex-wrap justify-content-between align-items-center mb-4 gap-3">
        <div>
          <nav aria-label="breadcrumb">
            <ol class="breadcrumb mb-1 small">
              <li class="breadcrumb-item"><a routerLink="/admin/dashboard" class="text-decoration-none">Dashboard</a></li>
              <li class="breadcrumb-item"><a routerLink="/admin/quizzes" class="text-decoration-none">Quizzes</a></li>
              <li class="breadcrumb-item active" aria-current="page">{{ isEditMode() ? 'Edit Quiz' : 'New Quiz' }}</li>
            </ol>
          </nav>
          <h2 class="fw-bold mb-0">
            <i class="bi" [ngClass]="isEditMode() ? 'bi-pencil-square text-primary' : 'bi-plus-circle text-success'"></i>
            <span class="ms-2">{{ isEditMode() ? 'Edit Quiz & Question Bank' : 'Create New Assessment' }}</span>
          </h2>
        </div>
        <div class="d-flex gap-2">
          @if (isEditMode() && quiz()) {
            <button 
              type="button" 
              class="btn" 
              [ngClass]="quiz()!.isPublished ? 'btn-outline-danger' : 'btn-success'"
              (click)="togglePublish()"
              [disabled]="isSaving()">
              <i class="bi" [ngClass]="quiz()!.isPublished ? 'bi-eye-slash' : 'bi-check2-circle'"></i>
              <span class="ms-1">{{ quiz()!.isPublished ? 'Unpublish Quiz' : 'Publish Quiz' }}</span>
            </button>
          }
          <a routerLink="/admin/quizzes" class="btn btn-outline-secondary d-flex align-items-center gap-1">
            <i class="bi bi-arrow-left"></i>
            <span>Back to Quizzes</span>
          </a>
        </div>
      </div>

      <!-- Main Quiz Settings Form -->
      <div class="row g-4 mb-4">
        <div class="col-12 col-xl-8">
          <div class="card border-0 shadow-sm">
            <div class="card-header bg-white border-0 py-3">
              <h5 class="fw-bold mb-0 text-dark">
                <i class="bi bi-sliders text-primary me-2"></i>General Information & Rules
              </h5>
            </div>
            <div class="card-body pt-0">
              <form [formGroup]="quizForm" (ngSubmit)="onSubmitQuiz()">
                <div class="row g-3">
                  <!-- Title -->
                  <div class="col-12">
                    <label class="form-label fw-semibold">Quiz Title <span class="text-danger">*</span></label>
                    <input 
                      type="text" 
                      class="form-control" 
                      formControlName="title" 
                      placeholder="e.g. UPSC General Studies Mock Examination 2026"
                      [class.is-invalid]="f['title'].touched && f['title'].invalid">
                    @if (f['title'].touched && f['title'].invalid) {
                      <div class="invalid-feedback">Quiz title is required (maximum 250 characters).</div>
                    }
                  </div>

                  <!-- Category -->
                  <div class="col-12 col-md-6">
                    <label class="form-label fw-semibold">Category <span class="text-danger">*</span></label>
                    <select 
                      class="form-select" 
                      formControlName="categoryId"
                      [class.is-invalid]="f['categoryId'].touched && f['categoryId'].invalid">
                      <option value="">Select Category...</option>
                      @for (cat of categories(); track cat.id) {
                        <option [value]="cat.id">{{ cat.name }}</option>
                      }
                    </select>
                    @if (f['categoryId'].touched && f['categoryId'].invalid) {
                      <div class="invalid-feedback">Please select a category.</div>
                    }
                  </div>

                  <!-- Duration (Minutes) -->
                  <div class="col-12 col-md-6">
                    <label class="form-label fw-semibold">Duration (Minutes) <span class="text-danger">*</span></label>
                    <div class="input-group">
                      <input 
                        type="number" 
                        class="form-control" 
                        formControlName="durationMinutes" 
                        min="1"
                        [class.is-invalid]="f['durationMinutes'].touched && f['durationMinutes'].invalid">
                      <span class="input-group-text bg-light text-muted">Minutes</span>
                    </div>
                    @if (f['durationMinutes'].touched && f['durationMinutes'].invalid) {
                      <div class="invalid-feedback d-block">Duration must be at least 1 minute.</div>
                    }
                  </div>

                  <!-- Start Time (Local input converted to UTC) -->
                  <div class="col-12 col-md-6">
                    <label class="form-label fw-semibold">Scheduled Start (Local) <span class="text-danger">*</span></label>
                    <input 
                      type="datetime-local" 
                      class="form-control" 
                      formControlName="startAtLocal"
                      [class.is-invalid]="f['startAtLocal'].touched && f['startAtLocal'].invalid">
                    <div class="form-text small text-muted">Converted to server-authoritative UTC on save.</div>
                  </div>

                  <!-- End Time (Local input converted to UTC) -->
                  <div class="col-12 col-md-6">
                    <label class="form-label fw-semibold">Scheduled Deadline (Local) <span class="text-danger">*</span></label>
                    <input 
                      type="datetime-local" 
                      class="form-control" 
                      formControlName="endAtLocal"
                      [class.is-invalid]="f['endAtLocal'].touched && f['endAtLocal'].invalid">
                    @if (quizForm.hasError('invalidSchedule')) {
                      <div class="text-danger small mt-1">Deadline must be after Scheduled Start time.</div>
                    }
                  </div>

                  <!-- Passing Percent & Max Attempts -->
                  <div class="col-12 col-md-6">
                    <label class="form-label fw-semibold">Passing Score (%)</label>
                    <div class="input-group">
                      <input 
                        type="number" 
                        class="form-control" 
                        formControlName="passingPercent" 
                        min="0" 
                        max="100">
                      <span class="input-group-text bg-light text-muted">%</span>
                    </div>
                  </div>

                  <div class="col-12 col-md-6">
                    <label class="form-label fw-semibold">Max Attempts per Candidate</label>
                    <input 
                      type="number" 
                      class="form-control" 
                      formControlName="maxAttempts" 
                      min="1">
                  </div>

                  <!-- Description -->
                  <div class="col-12">
                    <label class="form-label fw-semibold">Description / Candidate Instructions</label>
                    <textarea 
                      class="form-control" 
                      rows="3" 
                      formControlName="description" 
                      placeholder="Instructions, negative marking rules, topics covered..."></textarea>
                  </div>

                  <!-- Anti-Cheat / Randomization Toggles -->
                  <div class="col-12 col-md-6">
                    <div class="form-check form-switch mt-2">
                      <input class="form-check-input" type="checkbox" id="shuffleQuestions" formControlName="shuffleQuestions">
                      <label class="form-check-label fw-semibold" for="shuffleQuestions">
                        Shuffle Questions per Candidate
                      </label>
                      <div class="text-muted small">Questions appear in randomized order for each attempt.</div>
                    </div>
                  </div>

                  <div class="col-12 col-md-6">
                    <div class="form-check form-switch mt-2">
                      <input class="form-check-input" type="checkbox" id="shuffleOptions" formControlName="shuffleOptions">
                      <label class="form-check-label fw-semibold" for="shuffleOptions">
                        Shuffle Options per Question
                      </label>
                      <div class="text-muted small">Option choices are randomized for candidates.</div>
                    </div>
                  </div>

                  <!-- Submit Quiz Button -->
                  <div class="col-12 text-end pt-3">
                    <button 
                      type="submit" 
                      class="btn btn-primary px-4 py-2 d-inline-flex align-items-center gap-2"
                      [disabled]="isSaving() || quizForm.invalid">
                      @if (isSaving()) {
                        <span class="spinner-border spinner-border-sm" role="status"></span>
                      } @else {
                        <i class="bi bi-cloud-arrow-up-fill"></i>
                      }
                      <span>{{ isEditMode() ? 'Update Quiz Settings' : 'Save & Continue to Questions' }}</span>
                    </button>
                  </div>
                </div>
              </form>
            </div>
          </div>
        </div>

        <!-- Quiz Status Sidebar Card -->
        <div class="col-12 col-xl-4">
          <div class="card border-0 shadow-sm mb-3">
            <div class="card-header bg-white border-0 py-3">
              <h5 class="fw-bold mb-0 text-dark">
                <i class="bi bi-info-circle text-info me-2"></i>Assessment Overview
              </h5>
            </div>
            <div class="card-body pt-0">
              <ul class="list-group list-group-flush small">
                <li class="list-group-item d-flex justify-content-between px-0">
                  <span class="text-muted">Status:</span>
                  @if (quiz()?.isPublished) {
                    <span class="badge bg-success-subtle text-success">
                      <i class="bi bi-check-circle-fill me-1"></i> Published
                    </span>
                  } @else {
                    <span class="badge bg-warning-subtle text-warning">
                      <i class="bi bi-clock-history me-1"></i> Draft Mode
                    </span>
                  }
                </li>
                <li class="list-group-item d-flex justify-content-between px-0">
                  <span class="text-muted">Total Questions:</span>
                  <span class="fw-bold">{{ questions().length }}</span>
                </li>
                <li class="list-group-item d-flex justify-content-between px-0">
                  <span class="text-muted">Total Marks:</span>
                  <span class="fw-bold">{{ computeTotalMarks() }} Marks</span>
                </li>
                <li class="list-group-item d-flex justify-content-between px-0">
                  <span class="text-muted">Passing Score:</span>
                  <span class="fw-bold">{{ quizForm.value.passingPercent }}%</span>
                </li>
                <li class="list-group-item d-flex justify-content-between px-0">
                  <span class="text-muted">Server Authoritative:</span>
                  <span class="badge bg-primary-subtle text-primary">UTC Time</span>
                </li>
              </ul>
            </div>
          </div>
        </div>
      </div>

      <!-- Question Builder Section (Available in Edit Mode) -->
      @if (isEditMode() && quizId()) {
        <div class="card border-0 shadow-sm mb-5">
          <div class="card-header bg-white border-0 py-3 d-flex flex-wrap justify-content-between align-items-center gap-2">
            <div>
              <h5 class="fw-bold mb-0 text-dark">
                <i class="bi bi-question-diamond-fill text-primary me-2"></i>Question Bank ({{ questions().length }})
              </h5>
              <div class="text-muted small">Configure question prompts, choice types, marks, and correct answer keys.</div>
            </div>
            <div class="d-flex align-items-center gap-2">
              @if (quiz()?.isPublished && (quiz()?.attemptCount ?? 0) > 0) {
                <span class="badge bg-warning-subtle text-warning border px-2 py-1 small">
                  <i class="bi bi-lock-fill me-1"></i>Locked (Quiz has attempts)
                </span>
              }
              <button 
                type="button" 
                class="btn btn-primary btn-sm d-flex align-items-center gap-1 shadow-sm"
                [disabled]="quiz()?.isPublished && (quiz()?.attemptCount ?? 0) > 0"
                (click)="openAddQuestionModal()">
                <i class="bi bi-plus-lg"></i>
                <span>Add Question</span>
              </button>
            </div>
          </div>

          <div class="card-body p-0">
            @if (quiz()?.isPublished && (quiz()?.attemptCount ?? 0) > 0) {
              <div class="alert alert-warning m-3 d-flex align-items-center gap-2 py-2 small mb-0">
                <i class="bi bi-shield-exclamation fs-5"></i>
                <div>
                  <strong>Integrity Protection:</strong> Questions cannot be added, edited, reordered or deleted because candidates have already taken or started this assessment.
                </div>
              </div>
            }

            @if (isLoadingQuestions()) {
              <div class="text-center py-5">
                <div class="spinner-border text-primary mb-2" role="status"></div>
                <p class="text-muted small">Loading questions...</p>
              </div>
            } @else if (questions().length === 0) {
              <div class="text-center py-5">
                <i class="bi bi-question-circle fs-1 text-muted"></i>
                <h6 class="fw-bold mt-2 text-muted">No Questions Added Yet</h6>
                <p class="text-muted small mb-3">Add at least one question before publishing this quiz.</p>
                <button type="button" class="btn btn-outline-primary btn-sm" (click)="openAddQuestionModal()">
                  <i class="bi bi-plus-circle me-1"></i> Add First Question
                </button>
              </div>
            } @else {
              <div class="list-group list-group-flush">
                @for (q of questions(); track q.id; let idx = $index) {
                  <div class="list-group-item p-3 p-md-4">
                    <div class="d-flex flex-wrap justify-content-between align-items-start gap-2 mb-2">
                      <div class="d-flex align-items-center gap-2">
                        <span class="badge bg-secondary rounded-pill">#{{ idx + 1 }}</span>
                        <span class="badge" [ngClass]="getTypeBadge(q.type)">{{ getTypeName(q.type) }}</span>
                        <span class="badge bg-light text-dark border">{{ q.marks }} Marks</span>
                      </div>
                      <div class="d-flex gap-1">
                        <button 
                          type="button" 
                          class="btn btn-sm btn-outline-secondary" 
                          title="Move Up" 
                          [disabled]="idx === 0 || (quiz()?.isPublished && (quiz()?.attemptCount ?? 0) > 0)"
                          (click)="moveQuestion(idx, -1)">
                          <i class="bi bi-arrow-up"></i>
                        </button>
                        <button 
                          type="button" 
                          class="btn btn-sm btn-outline-secondary" 
                          title="Move Down" 
                          [disabled]="idx === questions().length - 1 || (quiz()?.isPublished && (quiz()?.attemptCount ?? 0) > 0)"
                          (click)="moveQuestion(idx, 1)">
                          <i class="bi bi-arrow-down"></i>
                        </button>
                        <button 
                          type="button" 
                          class="btn btn-sm btn-outline-primary" 
                          title="Edit question / fix typos"
                          (click)="editQuestion(q)">
                          <i class="bi bi-pencil"></i>
                        </button>
                        <button 
                          type="button" 
                          class="btn btn-sm btn-outline-danger" 
                          [disabled]="quiz()?.isPublished && (quiz()?.attemptCount ?? 0) > 0"
                          (click)="deleteQuestion(q)">
                          <i class="bi bi-trash"></i>
                        </button>
                      </div>
                    </div>

                    <!-- Question Text -->
                    <h6 class="fw-bold mb-3 text-dark">{{ q.text }}</h6>

                    <!-- Options Grid -->
                    <div class="row g-2">
                      @for (opt of q.options; track $index; let optIdx = $index) {
                        <div class="col-12 col-md-6">
                          <div 
                            class="p-2 rounded border d-flex align-items-center justify-content-between"
                            [ngClass]="opt.isCorrect ? 'border-success bg-success-subtle text-success-emphasis' : 'bg-light text-dark'">
                            <div class="d-flex align-items-center gap-2 text-truncate">
                              <span class="fw-bold small">{{ getLetter(optIdx) }}.</span>
                              <span class="text-truncate">{{ opt.text }}</span>
                            </div>
                            @if (opt.isCorrect) {
                              <span class="badge bg-success small ms-2">
                                <i class="bi bi-check-lg"></i> Correct
                              </span>
                            }
                          </div>
                        </div>
                      }
                    </div>
                  </div>
                }
              </div>
            }
          </div>
        </div>
      } @else {
        <!-- Guide box for create mode -->
        <div class="alert alert-info shadow-sm d-flex align-items-center gap-3">
          <i class="bi bi-info-circle-fill fs-3 text-info"></i>
          <div>
            <h6 class="fw-bold mb-1">Save General Information to Add Questions</h6>
            <p class="small mb-0">Once you save the quiz parameters above, the Question Builder will activate automatically allowing you to build your question bank.</p>
          </div>
        </div>
      }

      <!-- Question Editor Modal / Backdrop -->
      @if (showQuestionModal()) {
        <div class="modal fade show d-block" tabindex="-1" style="background-color: rgba(0, 0, 0, 0.5); z-index: 1060;">
          <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
            <div class="modal-content shadow border-0">
              <div class="modal-header">
                <h5 class="modal-title fw-bold">
                  {{ editingQuestionId() ? 'Edit Question' : 'Add New Question' }}
                </h5>
                <button type="button" class="btn-close" (click)="closeQuestionModal()"></button>
              </div>

              <div class="modal-body">
                <!-- Question Text -->
                <div class="mb-3">
                  <label class="form-label fw-semibold">Question Prompt (English) <span class="text-danger">*</span></label>
                  <textarea 
                    class="form-control" 
                    rows="2" 
                    [(ngModel)]="questionForm.text" 
                    [class.is-invalid]="fieldErrors()['text']"
                    placeholder="Enter the question text or problem statement in English..."></textarea>
                  @if (fieldErrors()['text']) {
                    <div class="invalid-feedback d-block">{{ fieldErrors()['text'] }}</div>
                  }
                </div>

                <!-- Question Text (Hindi) -->
                <div class="mb-3">
                  <label class="form-label fw-semibold">Question Prompt (हिंदी / Hindi) <span class="text-muted fw-normal">(Optional)</span></label>
                  <textarea 
                    class="form-control" 
                    rows="2" 
                    [(ngModel)]="questionForm.textHi" 
                    placeholder="प्रश्न का हिंदी विवरण दर्ज करें..."></textarea>
                </div>

                @if (isQuizLockedWithAttempts()) {
                  <div class="alert alert-warning py-2 mb-3 small d-flex align-items-center gap-2">
                    <i class="bi bi-shield-exclamation fs-5"></i>
                    <div>
                      <strong>Integrity Protection:</strong> This quiz has recorded candidate attempts. You can fix typos in the prompt and option texts. Question type, marks, and answer key structure are locked.
                    </div>
                  </div>
                }

                <div class="row g-3 mb-4">
                  <!-- Question Type -->
                  <div class="col-12 col-md-6">
                    <label class="form-label fw-semibold">Question Type</label>
                    <select class="form-select" [ngModel]="questionForm.type" (ngModelChange)="onTypeSelected($event)" [disabled]="isQuizLockedWithAttempts()">
                      <option [value]="1">Single Choice (1 Correct)</option>
                      <option [value]="2">Multiple Choice (>= 1 Correct)</option>
                      <option [value]="3">True / False</option>
                    </select>
                  </div>

                  <!-- Marks -->
                  <div class="col-12 col-md-6">
                    <label class="form-label fw-semibold">Marks Awarded <span class="text-danger">*</span></label>
                    <input 
                      type="number" 
                      class="form-control" 
                      [(ngModel)]="questionForm.marks" 
                      min="0.5" 
                      step="0.5"
                      [disabled]="isQuizLockedWithAttempts()"
                      [class.is-invalid]="fieldErrors()['marks']">
                    @if (fieldErrors()['marks']) {
                      <div class="invalid-feedback d-block">{{ fieldErrors()['marks'] }}</div>
                    }
                  </div>
                </div>

                <!-- Options Builder -->
                <div class="mb-3">
                  <div class="d-flex justify-content-between align-items-center mb-2">
                    <label class="form-label fw-semibold mb-0">
                      Answer Choices & Correct Key <span class="text-danger">*</span>
                    </label>
                    @if (questionForm.type !== 3 && !isQuizLockedWithAttempts()) {
                      <button 
                        type="button" 
                        class="btn btn-outline-primary btn-sm" 
                        (click)="addOption()"
                        [disabled]="questionForm.options.length >= 6">
                        <i class="bi bi-plus me-1"></i> Add Option
                      </button>
                    }
                  </div>
                  <div class="text-muted small mb-3">
                    @if (questionForm.type === 1) {
                      Select the single radio button marking the 1 correct answer.
                    } @else if (questionForm.type === 2) {
                      Check all boxes that apply as correct answers.
                    } @else {
                      Select whether True or False is the correct answer.
                    }
                  </div>

                  <div class="d-flex flex-column gap-2">
                    @for (opt of questionForm.options; track $index; let idx = $index) {
                      <div>
                        <div class="input-group">
                          <span class="input-group-text bg-light fw-bold">{{ getLetter(idx) }}</span>
                          <!-- Correct Selector -->
                          <span class="input-group-text bg-white">
                            @if (questionForm.type === 1 || questionForm.type === 3) {
                              <input 
                                class="form-check-input mt-0" 
                                type="radio" 
                                name="singleChoiceKey" 
                                [checked]="opt.isCorrect"
                                [disabled]="isQuizLockedWithAttempts()"
                                (change)="setSingleCorrect(idx)">
                            } @else {
                              <input 
                                class="form-check-input mt-0" 
                                type="checkbox" 
                                [disabled]="isQuizLockedWithAttempts()"
                                [(ngModel)]="opt.isCorrect">
                            }
                          </span>
                          <!-- Option Text Input (English) -->
                          <input 
                            type="text" 
                            class="form-control" 
                            [(ngModel)]="opt.text" 
                            placeholder="Option text (English)..."
                            [class.is-invalid]="fieldErrors()['option_' + idx]"
                            [readonly]="questionForm.type === 3">
                          <!-- Option Text Input (Hindi) -->
                          @if (questionForm.type !== 3) {
                            <input 
                              type="text" 
                              class="form-control" 
                              [(ngModel)]="opt.textHi" 
                              placeholder="विकल्प (हिंदी)...">
                          }
                          <!-- Remove button (if > 2 options) -->
                          @if (!isQuizLockedWithAttempts() && questionForm.type !== 3 && questionForm.options.length > 2) {
                            <button 
                              type="button" 
                              class="btn btn-outline-danger" 
                              (click)="removeOption(idx)">
                              <i class="bi bi-trash"></i>
                            </button>
                          }
                        </div>
                        @if (fieldErrors()['option_' + idx]) {
                          <div class="invalid-feedback d-block small mt-1">{{ fieldErrors()['option_' + idx] }}</div>
                        }
                      </div>
                    }
                  </div>

                  <!-- Error message if validation fails -->
                  @if (questionValidationError()) {
                    <div class="alert alert-danger py-2 mt-3 mb-0 small">
                      <i class="bi bi-exclamation-triangle-fill me-1"></i>
                      {{ questionValidationError() }}
                    </div>
                  }
                </div>
              </div>

              <div class="modal-footer">
                <button type="button" class="btn btn-outline-secondary" (click)="closeQuestionModal()">Cancel</button>
                <button 
                  type="button" 
                  class="btn btn-primary d-inline-flex align-items-center gap-2" 
                  (click)="saveQuestionModal()"
                  [disabled]="isSavingQuestion()">
                  @if (isSavingQuestion()) {
                    <span class="spinner-border spinner-border-sm" role="status"></span>
                  }
                  <span>{{ editingQuestionId() ? 'Update Question' : 'Save Question' }}</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class AdminQuizEditComponent implements OnInit {
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private adminService = inject(AdminService);
  private toastService = inject(ToastService);

  quizId = signal<string | null>(null);
  isEditMode = signal<boolean>(false);
  quiz = signal<AdminQuizDto | null>(null);
  categories = signal<AdminCategoryDto[]>([]);
  questions = signal<AdminQuestionDto[]>([]);

  isSaving = signal<boolean>(false);
  isLoadingQuestions = signal<boolean>(false);

  // Question modal state (converted to Angular Signals for reliable Zoneless CD)
  showQuestionModal = signal<boolean>(false);
  editingQuestionId = signal<string | null>(null);
  isSavingQuestion = signal<boolean>(false);
  questionValidationError = signal<string | null>(null);
  fieldErrors = signal<Record<string, string>>({});

  isQuizLockedWithAttempts = computed(() => {
    const q = this.quiz();
    return !!(q && q.isPublished && (q.attemptCount ?? 0) > 0);
  });

  questionForm = {
    text: '',
    textHi: '',
    type: 1 as AdminQuestionType,
    marks: 1,
    order: 1,
    options: [
      { text: '', textHi: '', isCorrect: false },
      { text: '', textHi: '', isCorrect: false },
      { text: '', textHi: '', isCorrect: false },
      { text: '', textHi: '', isCorrect: false }
    ] as { id?: string; text: string; textHi?: string; isCorrect: boolean }[]
  };

  quizForm: FormGroup = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(250)]],
    description: [''],
    categoryId: ['', Validators.required],
    durationMinutes: [60, [Validators.required, Validators.min(1)]],
    startAtLocal: [this.toLocalInputString(new Date()), Validators.required],
    endAtLocal: [this.toLocalInputString(new Date(Date.now() + 7 * 24 * 3600 * 1000)), Validators.required],
    passingPercent: [40, [Validators.required, Validators.min(0), Validators.max(100)]],
    maxAttempts: [1, [Validators.required, Validators.min(1)]],
    shuffleQuestions: [false],
    shuffleOptions: [false]
  }, { validators: this.scheduleValidator });

  get f() {
    return this.quizForm.controls;
  }

  ngOnInit(): void {
    this.loadCategories();

    const paramMap$ = this.route.paramMap || of(this.route.snapshot?.paramMap);
    if (paramMap$) {
      paramMap$.subscribe(params => {
        const id = params?.get ? params.get('id') : null;
        if (id) {
          this.quizId.set(id);
          this.isEditMode.set(true);
          this.loadQuiz(id);
          this.loadQuestions(id);
        } else {
          this.quizId.set(null);
          this.isEditMode.set(false);
          this.quiz.set(null);
          this.questions.set([]);
        }
      });
    }
  }

  scheduleValidator(group: FormGroup) {
    const start = group.get('startAtLocal')?.value;
    const end = group.get('endAtLocal')?.value;
    if (start && end && new Date(start) >= new Date(end)) {
      return { invalidSchedule: true };
    }
    return null;
  }

  toLocalInputString(date: Date): string {
    const pad = (n: number) => n < 10 ? '0' + n : n.toString();
    const y = date.getFullYear();
    const m = pad(date.getMonth() + 1);
    const d = pad(date.getDate());
    const h = pad(date.getHours());
    const min = pad(date.getMinutes());
    return `${y}-${m}-${d}T${h}:${min}`;
  }

  loadCategories(): void {
    this.adminService.getCategories().subscribe({
      next: (cats) => this.categories.set(cats),
      error: () => {}
    });
  }

  loadQuiz(id: string): void {
    this.adminService.getQuizById(id).subscribe({
      next: (q) => {
        this.quiz.set(q);
        this.quizForm.patchValue({
          title: q.title,
          description: q.description || '',
          categoryId: q.categoryId,
          durationMinutes: q.durationMinutes,
          startAtLocal: this.toLocalInputString(new Date(q.startAtUtc)),
          endAtLocal: this.toLocalInputString(new Date(q.endAtUtc)),
          passingPercent: q.passingPercent,
          maxAttempts: q.maxAttempts,
          shuffleQuestions: q.shuffleQuestions,
          shuffleOptions: q.shuffleOptions
        });
      },
      error: (err) => {
        this.toastService.showError(err.error?.message || 'Failed to load quiz details.');
        this.router.navigate(['/admin/quizzes']);
      }
    });
  }

  loadQuestions(quizId: string): void {
    this.isLoadingQuestions.set(true);
    this.adminService.getQuestions(quizId).subscribe({
      next: (data) => {
        this.questions.set(data);
        this.isLoadingQuestions.set(false);
      },
      error: () => {
        this.isLoadingQuestions.set(false);
      }
    });
  }

  computeTotalMarks(): number {
    return this.questions().reduce((sum, q) => sum + (q.marks || 0), 0);
  }

  onSubmitQuiz(): void {
    if (this.quizForm.invalid) {
      this.quizForm.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    const formVal = this.quizForm.value;

    const startAtUtc = new Date(formVal.startAtLocal).toISOString();
    const endAtUtc = new Date(formVal.endAtLocal).toISOString();

    if (this.isEditMode() && this.quizId()) {
      this.adminService.updateQuiz(this.quizId()!, {
        title: formVal.title,
        description: formVal.description,
        categoryId: formVal.categoryId,
        durationMinutes: formVal.durationMinutes,
        startAtUtc,
        endAtUtc,
        passingPercent: formVal.passingPercent,
        maxAttempts: formVal.maxAttempts,
        shuffleQuestions: formVal.shuffleQuestions,
        shuffleOptions: formVal.shuffleOptions
      }).subscribe({
        next: (updated) => {
          this.isSaving.set(false);
          this.quiz.set(updated);
          this.toastService.showSuccess('Quiz settings updated successfully.');
        },
        error: (err) => {
          this.isSaving.set(false);
          this.toastService.showError(err.error?.message || 'Failed to update quiz.');
        }
      });
    } else {
      this.adminService.createQuiz({
        title: formVal.title,
        description: formVal.description,
        categoryId: formVal.categoryId,
        durationMinutes: formVal.durationMinutes,
        startAtUtc,
        endAtUtc,
        passingPercent: formVal.passingPercent,
        maxAttempts: formVal.maxAttempts,
        shuffleQuestions: formVal.shuffleQuestions,
        shuffleOptions: formVal.shuffleOptions
      }).subscribe({
        next: (created) => {
          this.isSaving.set(false);
          this.toastService.showSuccess('Quiz created! Now add questions below.');
          this.router.navigate(['/admin/quizzes', created.id, 'edit']);
        },
        error: (err) => {
          this.isSaving.set(false);
          this.toastService.showError(err.error?.message || 'Failed to create quiz.');
        }
      });
    }
  }

  togglePublish(): void {
    if (!this.quizId() || !this.quiz()) return;
    const nextState = !this.quiz()!.isPublished;

    this.isSaving.set(true);
    this.adminService.toggleQuizPublish(this.quizId()!, nextState).subscribe({
      next: (res) => {
        this.isSaving.set(false);
        this.quiz.set(res);
        this.toastService.showSuccess(`Quiz ${nextState ? 'published' : 'unpublished'} successfully.`);
      },
      error: (err) => {
        this.isSaving.set(false);
        this.toastService.showError(err.error?.message || 'Failed to update publish status.');
      }
    });
  }

  // --- Question Builder Logic ---

  normalizeType(type: any): AdminQuestionType {
    if (typeof type === 'string') {
      const lower = type.toLowerCase();
      if (lower === 'singlechoice') return 1;
      if (lower === 'multichoice' || lower === 'multiplechoice') return 2;
      if (lower === 'truefalse') return 3;
    }
    const num = Number(type);
    return (num === 1 || num === 2 || num === 3) ? num as AdminQuestionType : 1;
  }

  openAddQuestionModal(): void {
    this.editingQuestionId.set(null);
    this.questionValidationError.set(null);
    this.fieldErrors.set({});
    this.questionForm = {
      text: '',
      textHi: '',
      type: 1,
      marks: 1,
      order: this.questions().length + 1,
      options: [
        { text: '', textHi: '', isCorrect: true },
        { text: '', textHi: '', isCorrect: false },
        { text: '', textHi: '', isCorrect: false },
        { text: '', textHi: '', isCorrect: false }
      ]
    };
    this.showQuestionModal.set(true);
  }

  editQuestion(q: AdminQuestionDto): void {
    this.editingQuestionId.set(q.id);
    this.questionValidationError.set(null);
    this.fieldErrors.set({});
    const normalizedType = this.normalizeType(q.type);
    this.questionForm = {
      text: q.text || q.textEn || '',
      textHi: q.textHi || '',
      type: normalizedType,
      marks: q.marks,
      order: q.order,
      options: (q.options || []).map(o => ({
        id: o.id,
        text: o.text || o.textEn || '',
        textHi: o.textHi || '',
        isCorrect: o.isCorrect
      }))
    };
    this.showQuestionModal.set(true);
  }

  closeQuestionModal(): void {
    this.showQuestionModal.set(false);
    this.editingQuestionId.set(null);
    this.questionValidationError.set(null);
    this.fieldErrors.set({});
  }

  onTypeSelected(val: any): void {
    this.questionForm.type = this.normalizeType(val);
    this.onQuestionTypeChange();
  }

  onQuestionTypeChange(): void {
    const t = this.normalizeType(this.questionForm.type);
    this.questionForm.type = t;
    if (t === 3) { // True/False
      this.questionForm.options = [
        { text: 'True', textHi: 'सत्य (True)', isCorrect: true },
        { text: 'False', textHi: 'असत्य (False)', isCorrect: false }
      ];
    } else if (this.questionForm.options.length < 2) {
      this.questionForm.options = [
        { text: '', textHi: '', isCorrect: true },
        { text: '', textHi: '', isCorrect: false },
        { text: '', textHi: '', isCorrect: false },
        { text: '', textHi: '', isCorrect: false }
      ];
    }
  }

  addOption(): void {
    this.questionForm.options.push({ text: '', textHi: '', isCorrect: false });
  }

  removeOption(index: number): void {
    if (this.questionForm.options.length > 2) {
      this.questionForm.options.splice(index, 1);
    }
  }

  setSingleCorrect(index: number): void {
    this.questionForm.options.forEach((opt, idx) => {
      opt.isCorrect = idx === index;
    });
  }

  parseServerErrors(err: any): { summary: string; fields: Record<string, string> } {
    const fields: Record<string, string> = {};
    let summary = err.error?.detail || err.error?.message || 'An error occurred while saving the question.';
    if (err.error?.errors && typeof err.error.errors === 'object') {
      const msgs: string[] = [];
      for (const [key, val] of Object.entries(err.error.errors)) {
        const errorList = Array.isArray(val) ? val.join(' ') : String(val);
        const lowerKey = key.toLowerCase();
        if (lowerKey.includes('text')) fields['text'] = errorList;
        if (lowerKey.includes('mark')) fields['marks'] = errorList;
        if (lowerKey.includes('option')) {
          fields['options'] = errorList;
          const match = key.match(/\[(\d+)\]/);
          if (match) {
            fields[`option_${match[1]}`] = errorList;
          }
        }
        msgs.push(errorList);
      }
      if (msgs.length > 0) {
        summary = msgs.join('. ');
      }
    }
    return { summary, fields };
  }

  saveQuestionModal(): void {
    this.questionValidationError.set(null);
    const errors: Record<string, string> = {};

    const text = (this.questionForm.text || '').trim();
    if (!text) {
      errors['text'] = 'Question prompt text is required.';
    }

    const marks = Number(this.questionForm.marks);
    if (isNaN(marks) || marks <= 0) {
      errors['marks'] = 'Marks must be greater than 0.';
    }

    const textHi = (this.questionForm.textHi || '').trim();
    const type = this.normalizeType(this.questionForm.type);
    const opts = (this.questionForm.options || []).map(o => ({
      id: o.id || undefined,
      text: (o.text || '').trim(),
      textEn: (o.text || '').trim(),
      textHi: (o.textHi || '').trim(),
      isCorrect: !!o.isCorrect
    }));

    if (opts.length < 2) {
      errors['options'] = 'A question must contain at least 2 options.';
    }

    opts.forEach((opt, idx) => {
      if (!opt.text) {
        errors[`option_${idx}`] = `Option ${this.getLetter(idx)} cannot be empty.`;
      }
    });

    const correctCount = opts.filter(o => o.isCorrect).length;

    if (type === 1) { // SingleChoice
      if (correctCount !== 1) {
        errors['options'] = 'Single choice question must have exactly 1 correct option.';
      }
    } else if (type === 2) { // MultiChoice
      if (correctCount < 1) {
        errors['options'] = 'Multiple choice question must have at least 1 correct option.';
      }
    } else if (type === 3) { // TrueFalse
      if (opts.length !== 2 || correctCount !== 1) {
        errors['options'] = 'True/False question must have exactly 1 correct answer (True or False).';
      }
    }

    if (Object.keys(errors).length > 0) {
      this.fieldErrors.set(errors);
      this.questionValidationError.set(errors['options'] || errors['text'] || errors['marks'] || 'Please correct the validation errors above.');
      return;
    }

    if (!this.quizId()) return;

    this.isSavingQuestion.set(true);

    const isEdit = !!this.editingQuestionId();
    const saveObservable = isEdit
      ? this.adminService.updateQuestion(this.quizId()!, this.editingQuestionId()!, {
          text,
          textEn: text,
          textHi,
          type,
          marks,
          order: this.questionForm.order,
          options: opts
        })
      : this.adminService.createQuestion(this.quizId()!, {
          text,
          textEn: text,
          textHi,
          type,
          marks,
          order: this.questionForm.order || (this.questions().length + 1),
          options: opts
        });

    saveObservable.subscribe({
      next: () => {
        this.isSavingQuestion.set(false);
        this.showQuestionModal.set(false);
        this.toastService.showSuccess(isEdit ? 'Question updated successfully.' : 'Question added to quiz.');
        this.loadQuestions(this.quizId()!);
      },
      error: (err) => {
        this.isSavingQuestion.set(false);
        if (err.status === 409) {
          const conflictMsg = err.error?.detail || err.error?.message || 'Quiz already has attempts, unpublish or duplicate it';
          this.questionValidationError.set(conflictMsg);
          this.toastService.showError(conflictMsg);
          return;
        }
        const parsed = this.parseServerErrors(err);
        this.questionValidationError.set(parsed.summary);
        this.fieldErrors.set(parsed.fields);
      }
    });
  }

  deleteQuestion(q: AdminQuestionDto): void {
    if (!confirm(`Are you sure you want to delete question "${q.text.slice(0, 40)}..."?`)) {
      return;
    }

    this.adminService.deleteQuestion(this.quizId()!, q.id).subscribe({
      next: () => {
        this.toastService.showSuccess('Question removed from quiz.');
        this.loadQuestions(this.quizId()!);
      },
      error: (err) => {
        this.toastService.showError(err.error?.message || 'Could not delete question.');
      }
    });
  }

  moveQuestion(index: number, direction: number): void {
    const list = [...this.questions()];
    const targetIdx = index + direction;
    if (targetIdx < 0 || targetIdx >= list.length) return;

    const temp = list[index];
    list[index] = list[targetIdx];
    list[targetIdx] = temp;

    this.questions.set(list);

    const questionIds = list.map(q => q.id);
    this.adminService.reorderQuestions(this.quizId()!, questionIds).subscribe({
      next: () => {
        this.toastService.showSuccess('Question order saved.');
      },
      error: () => {
        this.loadQuestions(this.quizId()!);
      }
    });
  }

  getLetter(index: number): string {
    return String.fromCharCode(65 + index);
  }

  getTypeName(type: any): string {
    const t = this.normalizeType(type);
    switch (t) {
      case 1: return 'Single Choice';
      case 2: return 'Multiple Choice';
      case 3: return 'True/False';
      default: return 'Question';
    }
  }

  getTypeBadge(type: any): string {
    const t = this.normalizeType(type);
    switch (t) {
      case 1: return 'bg-primary-subtle text-primary';
      case 2: return 'bg-purple-subtle text-purple';
      case 3: return 'bg-info-subtle text-info';
      default: return 'bg-secondary-subtle text-secondary';
    }
  }
}
