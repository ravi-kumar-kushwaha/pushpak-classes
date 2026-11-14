import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { QuizService } from './quiz.service';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/api.models';
import { AvailableQuizDto } from '../models/quiz.models';
import { StartQuizAttemptResponseDto } from '../models/attempt.models';

describe('QuizService', () => {
  let service: QuizService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        QuizService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(QuizService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should fetch available quizzes with pagination and search', () => {
    const mockPagedResult: PagedResult<AvailableQuizDto> = {
      items: [
        {
          id: 'quiz-1',
          title: 'Mathematics Final',
          categoryId: 'cat-1',
          categoryName: 'Math',
          durationMinutes: 30,
          startAtUtc: new Date().toISOString(),
          endAtUtc: new Date().toISOString(),
          passingPercent: 60,
          maxAttempts: 2,
          allowedAttempts: 2,
          attemptsUsed: 0,
          attemptsRemaining: 2,
          status: 'Open',
          hasActiveAttempt: false,
          questionCount: 10,
          totalMarks: 20
        }
      ],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 10,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false
    };

    service.getAvailableQuizzes(1, 10, 'Math').subscribe(res => {
      expect(res.items.length).toBe(1);
      expect(res.items[0].title).toBe('Mathematics Final');
      expect(res.items[0].attemptsRemaining).toBe(2);
    });

    const req = httpTesting.expectOne(req => req.url === `${environment.apiUrl}/quizzes/available` && req.params.get('search') === 'Math');
    expect(req.request.method).toBe('GET');
    req.flush(mockPagedResult);
  });

  it('should start a quiz and return attempt details with questions', () => {
    const mockStartResponse: StartQuizAttemptResponseDto = {
      attemptId: 'att-123',
      quizId: 'quiz-1',
      quizTitle: 'Mathematics Final',
      durationMinutes: 30,
      startedAtUtc: new Date().toISOString(),
      endsAtUtc: new Date(Date.now() + 1800000).toISOString(),
      serverTimeUtc: new Date().toISOString(),
      questions: []
    };

    service.startQuiz('quiz-1').subscribe(res => {
      expect(res.attemptId).toBe('att-123');
      expect(res.quizTitle).toBe('Mathematics Final');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/quizzes/quiz-1/start`);
    expect(req.request.method).toBe('POST');
    req.flush(mockStartResponse);
  });
});
