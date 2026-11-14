import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AttemptHistoryComponent } from './attempt-history.component';
import { ReportService } from '../../../core/services/report.service';
import { AttemptService } from '../../../core/services/attempt.service';
import { ToastService } from '../../../core/services/toast.service';

describe('AttemptHistoryComponent', () => {
  let component: AttemptHistoryComponent;
  let fixture: ComponentFixture<AttemptHistoryComponent>;
  let reportServiceSpy: { getUserAttempts: ReturnType<typeof vi.fn> };
  let attemptServiceSpy: { downloadCertificate: ReturnType<typeof vi.fn> };
  let toastServiceSpy: { showSuccess: ReturnType<typeof vi.fn>; showError: ReturnType<typeof vi.fn> };

  const mockPagedResult = {
    items: [
      {
        id: 'attempt-1',
        quizId: 'quiz-1',
        quizTitle: 'Science Mock Test',
        categoryName: 'Science',
        startedAtUtc: new Date().toISOString(),
        status: 'Submitted',
        score: 8,
        totalMarks: 10,
        percent: 80,
        passed: true,
        tabSwitchCount: 0
      }
    ],
    totalCount: 35,
    pageNumber: 1,
    pageSize: 15,
    totalPages: 3,
    hasPreviousPage: false,
    hasNextPage: true
  };

  beforeEach(async () => {
    reportServiceSpy = {
      getUserAttempts: vi.fn().mockReturnValue(of(mockPagedResult))
    };
    attemptServiceSpy = {
      downloadCertificate: vi.fn().mockReturnValue(of(new Blob()))
    };
    toastServiceSpy = {
      showSuccess: vi.fn(),
      showError: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [AttemptHistoryComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ReportService, useValue: reportServiceSpy },
        { provide: AttemptService, useValue: attemptServiceSpy },
        { provide: ToastService, useValue: toastServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AttemptHistoryComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with default pageSize = 15', () => {
    expect(component.pageSize()).toBe(15);
    expect(component.pageNumber()).toBe(1);
    expect(reportServiceSpy.getUserAttempts).toHaveBeenCalledWith(1, 15, '');
  });

  it('should compute totalPages, startItem, and endItem correctly', () => {
    component.totalCount.set(35);
    component.pageSize.set(15);
    component.pageNumber.set(1);

    expect(component.totalPages()).toBe(3);
    expect(component.startItem()).toBe(1);
    expect(component.endItem()).toBe(15);

    // Page 2
    component.pageNumber.set(2);
    expect(component.startItem()).toBe(16);
    expect(component.endItem()).toBe(30);

    // Page 3
    component.pageNumber.set(3);
    expect(component.startItem()).toBe(31);
    expect(component.endItem()).toBe(35);
  });

  it('should update pageSize and reset pageNumber to 1 when page size changes', () => {
    component.onPageSizeChange(25);

    expect(component.pageSize()).toBe(25);
    expect(component.pageNumber()).toBe(1);
    expect(reportServiceSpy.getUserAttempts).toHaveBeenCalledWith(1, 25, '');
  });

  it('should navigate to valid page on pageChange', () => {
    component.totalCount.set(35);
    component.onPageChange(2);

    expect(component.pageNumber()).toBe(2);
    expect(reportServiceSpy.getUserAttempts).toHaveBeenCalledWith(2, 15, '');
  });
});
