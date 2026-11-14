import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { QuizPlayComponent } from './quiz-play.component';
import { AttemptService } from '../../../../core/services/attempt.service';
import { TimeService } from '../../../../core/services/time.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { ToastService } from '../../../../core/services/toast.service';
import { ActiveAttemptDetailsDto } from '../../../../core/models/attempt.models';

describe('QuizPlayComponent', () => {
  let component: QuizPlayComponent;
  let fixture: ComponentFixture<QuizPlayComponent>;
  let mockAttemptService: any;
  let mockTimeService: any;
  let mockRouter: any;

  const mockActiveAttempt: ActiveAttemptDetailsDto = {
    attemptId: 'att-123',
    quizId: 'quiz-1',
    quizTitle: 'Science Assessment',
    durationMinutes: 20,
    startedAtUtc: new Date().toISOString(),
    endsAtUtc: new Date(Date.now() + 1200000).toISOString(),
    serverTimeUtc: new Date().toISOString(),
    status: 'InProgress',
    tabSwitchCount: 0,
    questions: [
      {
        id: 'q-1',
        text: 'What is H2O?',
        type: 'SingleChoice',
        marks: 2,
        order: 1,
        options: [
          { id: 'opt-1', text: 'Water' },
          { id: 'opt-2', text: 'Hydrogen' }
        ]
      },
      {
        id: 'q-2',
        text: 'Select noble gases',
        type: 'MultiChoice',
        marks: 3,
        order: 2,
        options: [
          { id: 'opt-3', text: 'Helium' },
          { id: 'opt-4', text: 'Neon' },
          { id: 'opt-5', text: 'Nitrogen' }
        ]
      }
    ],
    draftAnswers: []
  };

  beforeEach(async () => {
    mockAttemptService = {
      getActiveAttempt: vi.fn().mockReturnValue(of(mockActiveAttempt)),
      saveDraft: vi.fn().mockReturnValue(of({ message: 'Saved' })),
      submitAttempt: vi.fn().mockReturnValue(of({ status: 'Submitted' })),
      recordTabSwitch: vi.fn().mockReturnValue(of({ tabSwitchCount: 1 }))
    };

    mockTimeService = {
      syncWithServer: vi.fn(),
      getRemainingSeconds: vi.fn().mockReturnValue(600), // 10 minutes
      formatDuration: vi.fn().mockReturnValue('10:00')
    };

    mockRouter = {
      navigate: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [QuizPlayComponent],
      providers: [
        { provide: AttemptService, useValue: mockAttemptService },
        { provide: TimeService, useValue: mockTimeService },
        { provide: Router, useValue: mockRouter },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { params: { id: 'att-123' } } }
        },
        ConfirmDialogService,
        ToastService
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(QuizPlayComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    component.ngOnDestroy();
  });

  it('should initialize and load active attempt with questions', () => {
    expect(component.attempt()).toBeTruthy();
    expect(component.questions().length).toBe(2);
    expect(component.currentQuestion()?.id).toBe('q-1');
  });

  it('should track answered vs unanswered count', () => {
    expect(component.answeredCount()).toBe(0);

    // Select option for single choice question q-1
    component.onSelectOption('q-1', 'opt-1', 'SingleChoice');
    expect(component.answeredCount()).toBe(1);
    expect(component.isQuestionAnswered('q-1')).toBe(true);
    expect(component.isQuestionAnswered('q-2')).toBe(false);
  });

  it('should support multiple choice option toggle', () => {
    // Select opt-3
    component.onSelectOption('q-2', 'opt-3', 'MultiChoice');
    expect(component.isOptionSelected('q-2', 'opt-3')).toBe(true);

    // Select opt-4
    component.onSelectOption('q-2', 'opt-4', 'MultiChoice');
    expect(component.isOptionSelected('q-2', 'opt-3')).toBe(true);
    expect(component.isOptionSelected('q-2', 'opt-4')).toBe(true);

    // Deselect opt-3
    component.onSelectOption('q-2', 'opt-3', 'MultiChoice');
    expect(component.isOptionSelected('q-2', 'opt-3')).toBe(false);
    expect(component.isOptionSelected('q-2', 'opt-4')).toBe(true);
  });

  it('should navigate between questions', () => {
    expect(component.currentQuestionIndex()).toBe(0);

    component.nextQuestion();
    expect(component.currentQuestionIndex()).toBe(1);

    component.prevQuestion();
    expect(component.currentQuestionIndex()).toBe(0);

    component.jumpToQuestion(1);
    expect(component.currentQuestionIndex()).toBe(1);
  });

  it('should display questions and options in Hindi when Hindi is selected, and fallback to English when missing', () => {
    const bilingualQuestion = {
      id: 'q-bilingual',
      text: 'English Question Prompt',
      textEn: 'English Question Prompt',
      textHi: 'हिंदी प्रश्न विवरण',
      type: 'SingleChoice',
      marks: 2,
      order: 1,
      options: [
        { id: 'opt-b1', text: 'Option A English', textEn: 'Option A English', textHi: 'विकल्प A हिंदी' },
        { id: 'opt-b2', text: 'Option B Fallback English' } // No textHi
      ]
    };

    // 1. By default or when 'en' is selected
    component.setLanguage('en');
    expect(component.selectedLanguage()).toBe('en');
    expect(component.getQuestionText(bilingualQuestion)).toBe('English Question Prompt');
    expect(component.getOptionText(bilingualQuestion.options[0])).toBe('Option A English');

    // 2. When candidate selects 'hi'
    component.setLanguage('hi');
    expect(component.selectedLanguage()).toBe('hi');
    expect(component.getQuestionText(bilingualQuestion)).toBe('हिंदी प्रश्न विवरण');
    expect(component.getOptionText(bilingualQuestion.options[0])).toBe('विकल्प A हिंदी');

    // 3. Graceful fallback when Hindi text is missing
    expect(component.getOptionText(bilingualQuestion.options[1])).toBe('Option B Fallback English');
  });

  it('should determine urgency timer class correctly', () => {
    component.remainingSeconds.set(600);
    expect(component.timerCssClass()).toBe('normal');

    component.remainingSeconds.set(200);
    expect(component.timerCssClass()).toBe('warning');

    component.remainingSeconds.set(30);
    expect(component.timerCssClass()).toBe('danger');
  });
});
