import { Component, inject, OnInit, OnDestroy, HostListener, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject, Subscription, debounceTime } from 'rxjs';
import { AttemptService } from '../../../../core/services/attempt.service';
import { TimeService } from '../../../../core/services/time.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { ToastService } from '../../../../core/services/toast.service';
import {
  ActiveAttemptDetailsDto,
  AttemptQuestionDto,
  AttemptOptionDto,
  QuestionAnswerDto
} from '../../../../core/models/attempt.models';

@Component({
  selector: 'app-quiz-play',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    @if (isLoading()) {
      <div class="d-flex flex-column align-items-center justify-content-center min-vh-50 py-5">
        <div class="spinner-border text-primary mb-3" style="width: 3.5rem; height: 3.5rem;" role="status">
          <span class="visually-hidden">Loading exam...</span>
        </div>
        <h4 class="fw-bold">Loading Your Examination</h4>
        <p class="text-muted">Synchronizing with server and restoring draft answers...</p>
      </div>
    } @else if (attempt(); as att) {
      <div class="quiz-play-container">
        <!-- Sticky Exam Topbar with Authoritative Timer -->
        <header class="card border-0 shadow-sm mb-4 sticky-top rounded-0 rounded-bottom">
          <div class="card-body p-3 d-flex flex-wrap justify-content-between align-items-center gap-3">
            <div>
              <h5 class="fw-bold mb-0 text-dark">{{ att.quizTitle }}</h5>
              <div class="text-muted small d-flex align-items-center gap-2">
                <span>Question {{ currentQuestionIndex() + 1 }} of {{ questions().length }}</span>
                <span>•</span>
                <span class="d-flex align-items-center gap-1">
                  @if (saveStatus() === 'Saving...') {
                    <span class="spinner-border spinner-border-sm text-secondary" style="width: 12px; height: 12px;"></span>
                    <span class="text-secondary small">Saving...</span>
                  } @else if (saveStatus() === 'Saved') {
                    <i class="bi bi-cloud-check-fill text-success"></i>
                    <span class="text-success small">Answers saved</span>
                  } @else {
                    <i class="bi bi-cloud text-muted"></i>
                    <span class="text-muted small">Unsaved changes</span>
                  }
                </span>
              </div>
            </div>

            <!-- Language Toggle & Server-Authoritative Urgency Timer -->
            <div class="d-flex align-items-center gap-3">
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

              <div 
                class="timer-badge px-3 py-2 rounded-3 d-flex align-items-center gap-2 shadow-sm font-monospace fw-bold fs-5"
                [ngClass]="timerCssClass()">
                <i class="bi bi-stopwatch"></i>
                <span>{{ timerDisplay() }}</span>
              </div>

              <button 
                type="button" 
                class="btn btn-primary fw-semibold px-4" 
                [disabled]="isSubmitting() || isCompleted()" 
                (click)="confirmSubmit(false)">
                @if (isSubmitting()) {
                  <span class="spinner-border spinner-border-sm me-1"></span> Submitting...
                } @else {
                  <span>Submit Exam</span>
                }
              </button>
            </div>
          </div>
        </header>

        <!-- Main Examination Grid Layout -->
        <div class="row g-4">
          <!-- Left: Current Question Area -->
          <div class="col-12 col-lg-8">
            <div class="card border-0 shadow-sm mb-4">
              <div class="card-header bg-white py-3 border-bottom d-flex justify-content-between align-items-center">
                <span class="badge bg-primary-subtle text-primary fs-6 px-3 py-1">
                  Question {{ currentQuestionIndex() + 1 }}
                </span>
                <span class="text-muted small">
                  Marks: <strong class="text-dark">{{ currentQuestion()?.marks }}</strong>
                </span>
              </div>

              <div class="card-body p-4">
                <!-- Question Text -->
                <h5 class="fw-semibold text-dark mb-4 lh-base">
                  {{ getQuestionText(currentQuestion()) }}
                </h5>

                <!-- Options List based on Type -->
                <div class="options-container d-flex flex-column gap-2 mb-4">
                  @for (option of currentQuestion()?.options; track option.id; let idx = $index) {
                    <div 
                      class="option-card p-3 rounded-3 border d-flex align-items-center gap-3 cursor-pointer"
                      [class.selected]="isOptionSelected(currentQuestion()?.id!, option.id)"
                      (click)="onSelectOption(currentQuestion()?.id!, option.id, currentQuestion()?.type!)">
                      
                      <!-- Radio / Checkbox Indicator -->
                      <div class="option-indicator d-flex align-items-center justify-content-center">
                        @if (currentQuestion()?.type === 'MultiChoice') {
                          <i 
                            class="bi fs-5" 
                            [ngClass]="isOptionSelected(currentQuestion()?.id!, option.id) ? 'bi-check-square-fill text-primary' : 'bi-square text-secondary'">
                          </i>
                        } @else {
                          <i 
                            class="bi fs-5" 
                            [ngClass]="isOptionSelected(currentQuestion()?.id!, option.id) ? 'bi-record-circle-fill text-primary' : 'bi-circle text-secondary'">
                          </i>
                        }
                      </div>

                      <div class="flex-grow-1 user-select-none">
                        <span class="fw-semibold text-secondary me-2">{{ optionLetter(idx) }}.</span>
                        <span class="text-dark">{{ getOptionText(option) }}</span>
                      </div>
                    </div>
                  }
                </div>

                <!-- Clear Selection Button -->
                @if (hasSelectedOptions(currentQuestion()?.id!)) {
                  <button 
                    type="button" 
                    class="btn btn-outline-secondary btn-sm" 
                    (click)="clearChoice(currentQuestion()?.id!)">
                    <i class="bi bi-x-circle me-1"></i> Clear Selection
                  </button>
                }
              </div>

              <!-- Question Navigation Controls -->
              <div class="card-footer bg-white border-top py-3 d-flex justify-content-between align-items-center">
                <button 
                  type="button" 
                  class="btn btn-outline-secondary" 
                  [disabled]="currentQuestionIndex() <= 0" 
                  (click)="prevQuestion()">
                  <i class="bi bi-chevron-left me-1"></i> Previous
                </button>

                @if (currentQuestionIndex() < questions().length - 1) {
                  <button 
                    type="button" 
                    class="btn btn-primary px-4" 
                    (click)="nextQuestion()">
                    <span>Next</span> <i class="bi bi-chevron-right ms-1"></i>
                  </button>
                } @else {
                  <button 
                    type="button" 
                    class="btn btn-success px-4" 
                    [disabled]="isSubmitting() || isCompleted()" 
                    (click)="confirmSubmit(false)">
                    <span>Finish Exam</span> <i class="bi bi-check-all ms-1"></i>
                  </button>
                }
              </div>
            </div>
          </div>

          <!-- Right: Question Palette / Navigator -->
          <div class="col-12 col-lg-4">
            <div class="card border-0 shadow-sm sticky-top" style="top: 90px;">
              <div class="card-header bg-white py-3 border-bottom">
                <h6 class="fw-bold mb-0">Question Navigator</h6>
              </div>
              <div class="card-body p-3">
                <!-- Answered Summary -->
                <div class="d-flex justify-content-between align-items-center small text-muted mb-3">
                  <span>Answered: <strong class="text-primary">{{ answeredCount() }} / {{ questions().length }}</strong></span>
                  <span>Unanswered: <strong class="text-secondary">{{ questions().length - answeredCount() }}</strong></span>
                </div>

                <!-- Progress Bar -->
                <div class="progress mb-3" style="height: 6px;">
                  <div 
                    class="progress-bar bg-primary" 
                    role="progressbar" 
                    [style.width.%]="(answeredCount() / (questions().length || 1)) * 100">
                  </div>
                </div>

                <!-- Palette Grid Numbers -->
                <div class="palette-grid">
                  @for (q of questions(); track q.id; let idx = $index) {
                    <button 
                      type="button" 
                      class="palette-btn" 
                      [class.active]="currentQuestionIndex() === idx" 
                      [class.answered]="isQuestionAnswered(q.id)" 
                      [class.unanswered]="!isQuestionAnswered(q.id)" 
                      (click)="jumpToQuestion(idx)">
                      {{ idx + 1 }}
                    </button>
                  }
                </div>

                <hr class="my-3">

                <!-- Legend -->
                <div class="d-flex flex-wrap gap-3 small text-muted">
                  <div class="d-flex align-items-center gap-1">
                    <span class="legend-badge answered"></span> Answered
                  </div>
                  <div class="d-flex align-items-center gap-1">
                    <span class="legend-badge unanswered"></span> Unanswered
                  </div>
                  <div class="d-flex align-items-center gap-1">
                    <span class="legend-badge active"></span> Current
                  </div>
                </div>

                @if (tabSwitchCount() > 0) {
                  <div class="alert alert-warning py-2 px-3 small mt-3 mb-0 d-flex align-items-center gap-2">
                    <i class="bi bi-exclamation-triangle-fill text-warning"></i>
                    <div>Tab switches logged: <strong>{{ tabSwitchCount() }}</strong></div>
                  </div>
                }
              </div>
            </div>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .timer-badge {
      transition: all 0.3s ease;
    }
    .timer-badge.normal {
      background-color: #ecfdf5;
      color: #065f46;
      border: 1px solid #a7f3d0;
    }
    .timer-badge.warning {
      background-color: #fffbeb;
      color: #92400e;
      border: 1px solid #fde68a;
    }
    .timer-badge.danger {
      background-color: #fef2f2;
      color: #991b1b;
      border: 1px solid #fecaca;
      animation: pulse 1s infinite;
    }
    @keyframes pulse {
      0%, 100% { opacity: 1; transform: scale(1); }
      50% { opacity: 0.9; transform: scale(1.02); }
    }
    .option-card {
      transition: background-color 0.15s ease, border-color 0.15s ease;
      background-color: #fff;
    }
    .option-card:hover {
      background-color: #f8fafc;
      border-color: #cbd5e1;
    }
    .option-card.selected {
      background-color: #eff6ff;
      border-color: #3b82f6 !important;
    }
    .palette-grid {
      display: grid;
      grid-template-columns: repeat(5, 1fr);
      gap: 8px;
    }
    .palette-btn {
      aspect-ratio: 1;
      border-radius: 6px;
      font-weight: 600;
      font-size: 0.9rem;
      border: 1px solid #e2e8f0;
      background-color: #fff;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.15s;
    }
    .palette-btn.active {
      outline: 2px solid #2563eb;
      outline-offset: 2px;
    }
    .palette-btn.answered {
      background-color: #2563eb;
      color: #fff;
      border-color: #2563eb;
    }
    .palette-btn.unanswered {
      background-color: #f1f5f9;
      color: #475569;
    }
    .legend-badge {
      width: 12px;
      height: 12px;
      border-radius: 3px;
      display: inline-block;
    }
    .legend-badge.answered { background-color: #2563eb; }
    .legend-badge.unanswered { background-color: #f1f5f9; border: 1px solid #cbd5e1; }
    .legend-badge.active { background-color: #fff; border: 2px solid #2563eb; }
  `]
})
export class QuizPlayComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private attemptService = inject(AttemptService);
  timeService = inject(TimeService);
  private confirmService = inject(ConfirmDialogService);
  private toastService = inject(ToastService);

  attemptId: string = '';
  // Reactive signals for zoneless change detection
  attempt = signal<ActiveAttemptDetailsDto | null>(null);
  questions = signal<AttemptQuestionDto[]>([]);
  isLoading = signal(true);
  isCompleted = signal(false);

  currentQuestionIndex = signal<number>(0);
  remainingSeconds = signal<number>(0);
  isSubmitting = signal<boolean>(false);
  saveStatus = signal<'Saved' | 'Saving...' | 'Unsaved changes'>('Saved');
  tabSwitchCount = signal<number>(0);
  answersVersion = signal<number>(0);
  selectedLanguage = signal<'en' | 'hi'>((typeof localStorage !== 'undefined' && localStorage.getItem('pushpak_exam_lang') === 'hi') ? 'hi' : 'en');

  setLanguage(lang: 'en' | 'hi'): void {
    this.selectedLanguage.set(lang);
    try {
      localStorage.setItem('pushpak_exam_lang', lang);
    } catch { }
  }

  getQuestionText(q: AttemptQuestionDto | null | undefined): string {
    if (!q) return '';
    if (this.selectedLanguage() === 'hi') {
      return (q.textHi && q.textHi.trim().length > 0) ? q.textHi : (q.textEn || q.text || '');
    }
    return (q.textEn && q.textEn.trim().length > 0) ? q.textEn : (q.text || q.textHi || '');
  }

  getOptionText(opt: AttemptOptionDto | null | undefined): string {
    if (!opt) return '';
    if (this.selectedLanguage() === 'hi') {
      return (opt.textHi && opt.textHi.trim().length > 0) ? opt.textHi : (opt.textEn || opt.text || '');
    }
    return (opt.textEn && opt.textEn.trim().length > 0) ? opt.textEn : (opt.text || opt.textHi || '');
  }

  // Map storing questionId -> selectedOptionIds[]
  userAnswers = new Map<string, string[]>();

  // RxJS autosave subject with 10s debounce
  private autoSaveSubject = new Subject<void>();
  private autoSaveSub?: Subscription;
  private timerInterval?: any;

  timerDisplay = computed(() => this.timeService.formatDuration(this.remainingSeconds()));

  answeredCount = computed(() => {
    this.answersVersion(); // trigger reactive recomputation
    let count = 0;
    for (const q of this.questions()) {
      const selected = this.userAnswers.get(q.id) || [];
      if (selected.length > 0) count++;
    }
    return count;
  });

  currentQuestion = computed<AttemptQuestionDto | null>(() => {
    const qs = this.questions();
    if (qs.length === 0) return null;
    return qs[this.currentQuestionIndex()] || null;
  });

  timerCssClass = computed(() => {
    const sec = this.remainingSeconds();
    if (sec <= 60) return 'danger';
    if (sec <= 300) return 'warning';
    return 'normal';
  });

  ngOnInit(): void {
    this.attemptId = this.route.snapshot.params['id'];
    this.setupAutosave();
    this.setupTabSwitchDetection();
    this.loadAttempt();
  }

  ngOnDestroy(): void {
    this.cleanup();
  }

  private cleanup(): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
      this.timerInterval = undefined;
    }
    if (this.autoSaveSub) {
      this.autoSaveSub.unsubscribe();
      this.autoSaveSub = undefined;
    }
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
    window.removeEventListener('blur', this.onWindowBlur);
  }

  @HostListener('window:beforeunload', ['$event'])
  handleBeforeUnload(event: BeforeUnloadEvent): void {
    if (!this.isCompleted()) {
      this.flushBeaconSave();
      event.preventDefault();
      event.returnValue = 'You have an active exam in progress. Are you sure you want to leave?';
    }
  }

  private loadAttempt(): void {
    this.isLoading.set(true);
    this.attemptService.getActiveAttempt(this.attemptId).subscribe({
      next: (res) => {
        this.attempt.set(res);
        this.questions.set(res.questions || []);
        this.tabSwitchCount.set(res.tabSwitchCount);

        // Sync authoritative server clock
        this.timeService.syncWithServer(res.serverTimeUtc);

        // Restore draft answers from Redis
        if (res.draftAnswers && res.draftAnswers.length > 0) {
          for (const ans of res.draftAnswers) {
            this.userAnswers.set(ans.questionId, ans.selectedOptionIds);
          }
          this.answersVersion.update(v => v + 1);
        }

        this.isLoading.set(false);

        // If attempt is already submitted, redirect to review
        if (res.status === 'Submitted' || res.status === 'AutoSubmitted') {
          this.isCompleted.set(true);
          this.router.navigate(['/user/attempts', this.attemptId, 'review']);
          return;
        }

        // Start countdown
        this.startTimer();
      },
      error: (err) => {
        this.isLoading.set(false);
        this.toastService.showError(err.error?.message || 'Unable to load quiz attempt.', 'Error');
        this.router.navigate(['/user/quizzes']);
      }
    });
  }

  private startTimer(): void {
    const currentAttempt = this.attempt();
    if (!currentAttempt) return;

    // Calculate initial remaining seconds using server offset
    let initialRemaining = this.timeService.getRemainingSeconds(currentAttempt.endsAtUtc);

    // Failsafe: if an in-progress attempt calculates to <= 0, verify against the server's own authoritative timestamps
    if (initialRemaining <= 0 && currentAttempt.status === 'InProgress') {
      const serverNow = this.timeService.parseUtcTimestamp(currentAttempt.serverTimeUtc);
      const endsAt = this.timeService.parseUtcTimestamp(currentAttempt.endsAtUtc);
      if (!isNaN(serverNow) && !isNaN(endsAt) && endsAt > serverNow) {
        initialRemaining = Math.max(0, Math.floor((endsAt - serverNow) / 1000));
      }
    }

    this.remainingSeconds.set(initialRemaining);

    if (initialRemaining <= 0) {
      this.autoSubmitOnExpiry();
      return;
    }

    this.timerInterval = setInterval(() => {
      const att = this.attempt();
      if (!att) return;

      const remaining = this.timeService.getRemainingSeconds(att.endsAtUtc);
      this.remainingSeconds.set(remaining);

      if (remaining <= 0) {
        clearInterval(this.timerInterval);
        this.autoSubmitOnExpiry();
      }
    }, 1000);
  }

  private setupAutosave(): void {
    // 10-second debounce autosave
    this.autoSaveSub = this.autoSaveSubject.pipe(
      debounceTime(10000)
    ).subscribe(() => {
      this.saveDraft();
    });
  }

  private setupTabSwitchDetection(): void {
    document.addEventListener('visibilitychange', this.onVisibilityChange);
    window.addEventListener('blur', this.onWindowBlur);
  }

  private onVisibilityChange = (): void => {
    if (document.visibilityState === 'hidden' && !this.isCompleted() && !this.isLoading()) {
      this.triggerTabSwitch();
    }
  };

  private onWindowBlur = (): void => {
    if (!this.isCompleted() && !this.isLoading()) {
      this.triggerTabSwitch();
    }
  };

  private triggerTabSwitch(): void {
    this.attemptService.recordTabSwitch(this.attemptId).subscribe({
      next: (res) => {
        this.tabSwitchCount.set(res.tabSwitchCount);
        this.toastService.showWarning(
          `Tab switch detected (${res.tabSwitchCount}). Please stay on this tab during your exam.`,
          'Exam Integrity Warning'
        );
      }
    });
  }

  onSelectOption(questionId: string, optionId: string, type: string): void {
    if (this.isCompleted()) return;

    let selected = [...(this.userAnswers.get(questionId) || [])];

    if (type === 'MultiChoice') {
      const index = selected.indexOf(optionId);
      if (index >= 0) {
        selected.splice(index, 1);
      } else {
        selected.push(optionId);
      }
    } else {
      // SingleChoice or TrueFalse: exactly one choice
      selected = [optionId];
    }

    this.userAnswers.set(questionId, selected);
    this.answersVersion.update(v => v + 1);
    this.saveStatus.set('Unsaved changes');
    this.autoSaveSubject.next();
  }

  clearChoice(questionId: string): void {
    this.userAnswers.delete(questionId);
    this.answersVersion.update(v => v + 1);
    this.saveStatus.set('Unsaved changes');
    this.autoSaveSubject.next();
  }

  isOptionSelected(questionId: string, optionId: string): boolean {
    const selected = this.userAnswers.get(questionId) || [];
    return selected.includes(optionId);
  }

  hasSelectedOptions(questionId: string): boolean {
    const selected = this.userAnswers.get(questionId) || [];
    return selected.length > 0;
  }

  isQuestionAnswered(questionId: string): boolean {
    return this.hasSelectedOptions(questionId);
  }

  prevQuestion(): void {
    if (this.currentQuestionIndex() > 0) {
      this.saveDraftImmediately();
      this.currentQuestionIndex.update(i => i - 1);
    }
  }

  nextQuestion(): void {
    if (this.currentQuestionIndex() < this.questions().length - 1) {
      this.saveDraftImmediately();
      this.currentQuestionIndex.update(i => i + 1);
    }
  }

  jumpToQuestion(index: number): void {
    if (index >= 0 && index < this.questions().length && index !== this.currentQuestionIndex()) {
      this.saveDraftImmediately();
      this.currentQuestionIndex.set(index);
    }
  }

  optionLetter(index: number): string {
    return String.fromCharCode(65 + index); // A, B, C, D...
  }

  private buildAnswersPayload(): QuestionAnswerDto[] {
    const list: QuestionAnswerDto[] = [];
    this.userAnswers.forEach((selectedOptionIds, questionId) => {
      if (selectedOptionIds.length > 0) {
        list.push({ questionId, selectedOptionIds });
      }
    });
    return list;
  }

  saveDraft(): void {
    if (this.isCompleted() || this.isLoading()) return;

    this.saveStatus.set('Saving...');
    const payload = this.buildAnswersPayload();

    this.attemptService.saveDraft(this.attemptId, payload).subscribe({
      next: () => {
        this.saveStatus.set('Saved');
      },
      error: () => {
        this.saveStatus.set('Unsaved changes');
      }
    });
  }

  saveDraftImmediately(): void {
    this.saveDraft();
  }

  private flushBeaconSave(): void {
    const payload = this.buildAnswersPayload();
    const url = `${this.attemptService['apiUrl']}/${this.attemptId}/save`;
    const token = localStorage.getItem('pushpak_access_token');

    fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { 'Authorization': `Bearer ${token}` } : {})
      },
      body: JSON.stringify({ answers: payload }),
      keepalive: true
    }).catch(() => {});
  }

  async confirmSubmit(isAuto: boolean): Promise<void> {
    if (this.isCompleted()) return;

    if (!isAuto) {
      const answered = this.answeredCount();
      const total = this.questions().length;
      const unanswered = total - answered;

      let msg = `You have answered ${answered} of ${total} questions.`;
      if (unanswered > 0) {
        msg += ` You still have ${unanswered} unanswered question(s). Are you sure you want to finish and submit?`;
      } else {
        msg += ' Are you sure you want to submit your exam now?';
      }

      const confirmed = await this.confirmService.confirm({
        title: 'Submit Examination',
        message: msg,
        confirmText: 'Submit Final Answers',
        cancelText: 'Continue Exam'
      });

      if (!confirmed) return;
    }

    this.submitExam(isAuto);
  }

  private autoSubmitOnExpiry(): void {
    this.toastService.showWarning('Time has expired! Submitting your examination automatically...', 'Time Expired');
    this.submitExam(true);
  }

  private submitExam(isAuto: boolean): void {
    this.isCompleted.set(true);
    this.isSubmitting.set(true);

    // Save final answers first then submit
    const payload = this.buildAnswersPayload();
    this.attemptService.saveDraft(this.attemptId, payload).subscribe({
      next: () => {
        this.executeFinalSubmit();
      },
      error: () => {
        // Still proceed to submit even if draft save encounters 5s grace boundary
        this.executeFinalSubmit();
      }
    });
  }

  private executeFinalSubmit(): void {
    const payload = this.buildAnswersPayload();
    this.attemptService.submitAttempt(this.attemptId, payload).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.cleanup();
        this.toastService.showSuccess('Your exam has been submitted successfully!', 'Exam Submitted');
        this.router.navigate(['/user/attempts', this.attemptId, 'review']);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.cleanup();
        this.toastService.showError(err.error?.message || 'Error finalizing submission.', 'Submission Error');
        this.router.navigate(['/user/attempts', this.attemptId, 'review']);
      }
    });
  }
}
