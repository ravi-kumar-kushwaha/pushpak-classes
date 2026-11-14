import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminQuizEditComponent } from './admin-quiz-edit.component';
import { AdminService } from '../../../../core/services/admin.service';
import { ToastService } from '../../../../core/services/toast.service';

describe('AdminQuizEditComponent - Question Builder', () => {
  let component: AdminQuizEditComponent;
  let fixture: ComponentFixture<AdminQuizEditComponent>;
  let adminServiceSpy: {
    getCategories: ReturnType<typeof vi.fn>;
    getQuizById: ReturnType<typeof vi.fn>;
    getQuestions: ReturnType<typeof vi.fn>;
    createQuestion: ReturnType<typeof vi.fn>;
    updateQuestion: ReturnType<typeof vi.fn>;
    deleteQuestion: ReturnType<typeof vi.fn>;
    reorderQuestions: ReturnType<typeof vi.fn>;
  };
  let toastServiceSpy: { showSuccess: ReturnType<typeof vi.fn>; showError: ReturnType<typeof vi.fn> };

  const mockQuizId = '550e8400-e29b-41d4-a716-446655440000';

  beforeEach(async () => {
    adminServiceSpy = {
      getCategories: vi.fn().mockReturnValue(of([])),
      getQuizById: vi.fn().mockReturnValue(of({
        id: mockQuizId,
        title: 'Mock Quiz',
        categoryId: '1',
        durationMinutes: 60,
        startAtUtc: new Date().toISOString(),
        endAtUtc: new Date(Date.now() + 86400000).toISOString(),
        passingPercent: 40,
        maxAttempts: 1,
        isPublished: false,
        attemptCount: 0
      })),
      getQuestions: vi.fn().mockReturnValue(of([])),
      createQuestion: vi.fn(),
      updateQuestion: vi.fn(),
      deleteQuestion: vi.fn(),
      reorderQuestions: vi.fn()
    };
    toastServiceSpy = {
      showSuccess: vi.fn(),
      showError: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [AdminQuizEditComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: {
                get: (key: string) => key === 'id' ? mockQuizId : null
              }
            },
            paramMap: of({
              get: (key: string) => key === 'id' ? mockQuizId : null
            })
          }
        },
        { provide: AdminService, useValue: adminServiceSpy },
        { provide: ToastService, useValue: toastServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminQuizEditComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize and open question modal with default 4 options', () => {
    expect(component).toBeTruthy();
    expect(component.showQuestionModal()).toBe(false);

    component.openAddQuestionModal();
    expect(component.showQuestionModal()).toBe(true);
    expect(component.questionForm.options.length).toBe(4);
    expect(component.questionForm.options[0].isCorrect).toBe(true);
  });

  it('should validate empty prompt and invalid marks before sending request', () => {
    component.openAddQuestionModal();
    component.questionForm.text = '   ';
    component.questionForm.marks = 0;

    component.saveQuestionModal();

    expect(component.fieldErrors()['text']).toBe('Question prompt text is required.');
    expect(component.fieldErrors()['marks']).toBe('Marks must be greater than 0.');
    expect(component.questionValidationError()).toBeTruthy();
    expect(adminServiceSpy.createQuestion).not.toHaveBeenCalled();
  });

  it('should validate single choice question has exactly 1 correct option', () => {
    component.openAddQuestionModal();
    component.questionForm.text = 'What is the capital of India?';
    component.questionForm.marks = 2;
    component.questionForm.type = 1; // Single Choice
    component.questionForm.options = [
      { text: 'Mumbai', isCorrect: false },
      { text: 'Delhi', isCorrect: false },
      { text: 'Kolkata', isCorrect: false }
    ];

    component.saveQuestionModal();

    expect(component.questionValidationError()).toBe('Single choice question must have exactly 1 correct option.');
    expect(adminServiceSpy.createQuestion).not.toHaveBeenCalled();
  });

  it('should validate that options do not have empty text', () => {
    component.openAddQuestionModal();
    component.questionForm.text = 'Valid prompt';
    component.questionForm.marks = 1;
    component.questionForm.options = [
      { text: 'Option A', isCorrect: true },
      { text: '   ', isCorrect: false }
    ];

    component.saveQuestionModal();

    expect(component.fieldErrors()['option_1']).toBe('Option B cannot be empty.');
    expect(adminServiceSpy.createQuestion).not.toHaveBeenCalled();
  });

  it('should create question when form is valid and close modal on success', () => {
    adminServiceSpy.createQuestion.mockReturnValue(of({
      id: 'q1',
      text: 'What is 2 + 2?',
      type: 'SingleChoice',
      marks: 1,
      order: 1,
      options: []
    }));

    component.openAddQuestionModal();
    component.questionForm.text = 'What is 2 + 2?';
    component.questionForm.marks = 1;
    component.questionForm.type = 1;
    component.questionForm.options = [
      { text: '3', isCorrect: false },
      { text: '4', isCorrect: true }
    ];

    component.saveQuestionModal();

    expect(adminServiceSpy.createQuestion).toHaveBeenCalled();
    expect(component.showQuestionModal()).toBe(false);
    expect(toastServiceSpy.showSuccess).toHaveBeenCalledWith('Question added to quiz.');
  });

  it('should handle backend validation errors and update signals accordingly', () => {
    adminServiceSpy.createQuestion.mockReturnValue(throwError(() => ({
      status: 400,
      error: {
        detail: 'Validation failed',
        errors: {
          Text: ['Question text is required.'],
          Options: ['At least 2 options are required.']
        }
      }
    })));

    component.openAddQuestionModal();
    component.questionForm.text = 'Test Question';
    component.questionForm.marks = 1;
    component.questionForm.options = [
      { text: 'Option 1', isCorrect: true },
      { text: 'Option 2', isCorrect: false }
    ];

    component.saveQuestionModal();

    expect(component.isSavingQuestion()).toBe(false);
    expect(component.questionValidationError()).toContain('Question text is required.');
    expect(component.fieldErrors()['text']).toBe('Question text is required.');
    expect(component.fieldErrors()['options']).toBe('At least 2 options are required.');
  });

  it('should normalize question type between strings and numbers correctly', () => {
    expect(component.normalizeType('SingleChoice')).toBe(1);
    expect(component.normalizeType('MultiChoice')).toBe(2);
    expect(component.normalizeType('TrueFalse')).toBe(3);
    expect(component.normalizeType(1)).toBe(1);

    expect(component.getTypeName('SingleChoice')).toBe('Single Choice');
    expect(component.getTypeName(2)).toBe('Multiple Choice');
  });

  it('should lock structural changes when quiz is published and has attempts', () => {
    component.quiz.set({
      id: mockQuizId,
      title: 'Mock Quiz with attempts',
      categoryId: '1',
      categoryName: 'General',
      durationMinutes: 60,
      startAtUtc: new Date().toISOString(),
      endAtUtc: new Date(Date.now() + 86400000).toISOString(),
      passingPercent: 40,
      maxAttempts: 1,
      shuffleQuestions: false,
      shuffleOptions: false,
      isPublished: true,
      questionCount: 1,
      totalMarks: 2,
      attemptCount: 3
    });

    expect(component.isQuizLockedWithAttempts()).toBe(true);
  });

  it('should allow typo edit and preserve option ids when updating existing question', () => {
    adminServiceSpy.updateQuestion.mockReturnValue(of({} as any));

    const existingQuestion = {
      id: 'q-123',
      quizId: mockQuizId,
      type: 1 as any,
      textEn: 'Original English text',
      textHi: 'मूल हिंदी पाठ',
      marks: 2,
      orderIndex: 0,
      options: [
        { id: 'opt-1', textEn: 'Choice A', textHi: 'विकल्प A', isCorrect: true, orderIndex: 0 },
        { id: 'opt-2', textEn: 'Choice B', textHi: 'विकल्प B', isCorrect: false, orderIndex: 1 }
      ]
    };

    component.editQuestion(existingQuestion as any);
    expect(component.editingQuestionId()).toBe('q-123');
    expect(component.questionForm.options[0].id).toBe('opt-1');
    expect(component.questionForm.options[1].id).toBe('opt-2');

    // Make typo fix
    component.questionForm.text = 'Fixed English text typo';
    component.saveQuestionModal();

    expect(adminServiceSpy.updateQuestion).toHaveBeenCalledWith(
      mockQuizId,
      'q-123',
      expect.objectContaining({
        text: 'Fixed English text typo',
        options: expect.arrayContaining([
          expect.objectContaining({ id: 'opt-1', text: 'Choice A' }),
          expect.objectContaining({ id: 'opt-2', text: 'Choice B' })
        ])
      })
    );
    expect(toastServiceSpy.showSuccess).toHaveBeenCalledWith('Question updated successfully.');
  });

  it('should handle 409 conflict when quiz already has attempts', () => {
    adminServiceSpy.updateQuestion.mockReturnValue(throwError(() => ({
      status: 409,
      error: { message: 'Quiz already has attempts, unpublish or duplicate it' }
    })));

    component.editingQuestionId.set('q-123');
    component.showQuestionModal.set(true);
    component.questionForm.text = 'New question text';
    component.questionForm.marks = 1;
    component.questionForm.options = [
      { text: 'A', isCorrect: true },
      { text: 'B', isCorrect: false }
    ];

    component.saveQuestionModal();

    expect(component.questionValidationError()).toBe('Quiz already has attempts, unpublish or duplicate it');
    expect(toastServiceSpy.showError).toHaveBeenCalledWith(
      'Quiz already has attempts, unpublish or duplicate it'
    );
  });
});
