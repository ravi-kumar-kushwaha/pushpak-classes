import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AttemptService } from './attempt.service';
import { environment } from '../../../environments/environment';
import { ActiveAttemptDetailsDto, SubmitAttemptResponseDto } from '../models/attempt.models';

describe('AttemptService', () => {
  let service: AttemptService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AttemptService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(AttemptService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should get active attempt details for browser reload recovery', () => {
    const mockActiveAttempt: ActiveAttemptDetailsDto = {
      attemptId: 'att-100',
      quizId: 'quiz-5',
      quizTitle: 'Physics Exam',
      durationMinutes: 45,
      startedAtUtc: new Date().toISOString(),
      endsAtUtc: new Date(Date.now() + 2700000).toISOString(),
      serverTimeUtc: new Date().toISOString(),
      status: 'InProgress',
      tabSwitchCount: 0,
      questions: [],
      draftAnswers: [{ questionId: 'q-1', selectedOptionIds: ['opt-1'] }]
    };

    service.getActiveAttempt('att-100').subscribe(res => {
      expect(res.attemptId).toBe('att-100');
      expect(res.status).toBe('InProgress');
      expect(res.draftAnswers.length).toBe(1);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/attempts/att-100`);
    expect(req.request.method).toBe('GET');
    req.flush(mockActiveAttempt);
  });

  it('should save draft answers to Redis', () => {
    service.saveDraft('att-100', [{ questionId: 'q-1', selectedOptionIds: ['opt-1'] }]).subscribe(res => {
      expect(res.message).toBe('Draft answers saved successfully.');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/attempts/att-100/save`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.answers.length).toBe(1);
    req.flush({ message: 'Draft answers saved successfully.' });
  });

  it('should submit attempt', () => {
    const mockSubmitRes: SubmitAttemptResponseDto = {
      attemptId: 'att-100',
      status: 'Submitted',
      submittedAtUtc: new Date().toISOString(),
      message: 'Attempt submitted.'
    };

    service.submitAttempt('att-100').subscribe(res => {
      expect(res.status).toBe('Submitted');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/attempts/att-100/submit`);
    expect(req.request.method).toBe('POST');
    req.flush(mockSubmitRes);
  });

  it('should record tab switch anti-cheating event', () => {
    service.recordTabSwitch('att-100').subscribe(res => {
      expect(res.tabSwitchCount).toBe(1);
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/attempts/att-100/tab-switch`);
    expect(req.request.method).toBe('POST');
    req.flush({ attemptId: 'att-100', tabSwitchCount: 1, warningMessage: 'Tab switch recorded' });
  });

  it('should download certificate as blob', () => {
    const mockBlob = new Blob(['%PDF-1.4 test'], { type: 'application/pdf' });

    service.downloadCertificate('att-100').subscribe(blob => {
      expect(blob.type).toBe('application/pdf');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/attempts/att-100/certificate`);
    expect(req.request.method).toBe('GET');
    req.flush(mockBlob);
  });
});
