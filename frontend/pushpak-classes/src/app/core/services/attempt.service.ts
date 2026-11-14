import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ActiveAttemptDetailsDto,
  QuestionAnswerDto,
  SubmitAttemptResponseDto,
  TabSwitchResponseDto
} from '../models/attempt.models';
import { AttemptReviewDto } from '../models/report.models';
import { AuthMessageResponseDto } from '../models/auth.models';

@Injectable({
  providedIn: 'root'
})
export class AttemptService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/attempts`;

  getActiveAttempt(attemptId: string): Observable<ActiveAttemptDetailsDto> {
    return this.http.get<ActiveAttemptDetailsDto>(`${this.apiUrl}/${attemptId}`);
  }

  saveDraft(attemptId: string, answers: QuestionAnswerDto[]): Observable<AuthMessageResponseDto> {
    return this.http.post<AuthMessageResponseDto>(`${this.apiUrl}/${attemptId}/save`, { answers }, {
      headers: { 'X-Skip-Loader': 'true' }
    });
  }

  submitAttempt(attemptId: string, answers?: QuestionAnswerDto[]): Observable<SubmitAttemptResponseDto> {
    const body = answers && answers.length > 0 ? { answers } : {};
    return this.http.post<SubmitAttemptResponseDto>(`${this.apiUrl}/${attemptId}/submit`, body);
  }

  recordTabSwitch(attemptId: string): Observable<TabSwitchResponseDto> {
    return this.http.post<TabSwitchResponseDto>(`${this.apiUrl}/${attemptId}/tab-switch`, {}, {
      headers: { 'X-Skip-Loader': 'true' }
    });
  }

  getReview(attemptId: string): Observable<AttemptReviewDto> {
    return this.http.get<AttemptReviewDto>(`${this.apiUrl}/${attemptId}/review`);
  }

  downloadCertificate(attemptId: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${attemptId}/certificate`, {
      responseType: 'blob'
    });
  }
}
