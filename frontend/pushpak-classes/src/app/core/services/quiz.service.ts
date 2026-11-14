import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AvailableQuizDto } from '../models/quiz.models';
import { StartQuizAttemptResponseDto } from '../models/attempt.models';
import { PagedResult } from '../models/api.models';

export interface CategoryOptionDto {
  id: string;
  name: string;
}

@Injectable({
  providedIn: 'root'
})
export class QuizService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/quizzes`;
  private categoriesUrl = `${environment.apiUrl}/categories`;

  getAvailableQuizzes(
    pageNumber: number = 1,
    pageSize: number = 9,
    searchQuery?: string,
    categoryId?: string,
    sortBy?: string,
    sortDirection?: string,
    status?: string
  ): Observable<PagedResult<AvailableQuizDto>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (searchQuery && searchQuery.trim()) {
      params = params.set('searchQuery', searchQuery.trim());
      params = params.set('search', searchQuery.trim());
    }
    if (categoryId && categoryId.trim()) {
      params = params.set('categoryId', categoryId.trim());
    }
    if (sortBy) {
      params = params.set('sortBy', sortBy);
    }
    if (sortDirection) {
      params = params.set('sortDirection', sortDirection);
    }
    if (status && status.trim() && status.toLowerCase() !== 'all') {
      params = params.set('status', status.trim());
    }

    return this.http.get<PagedResult<AvailableQuizDto>>(`${this.apiUrl}/available`, { params });
  }

  getCategories(): Observable<CategoryOptionDto[]> {
    return this.http.get<CategoryOptionDto[]>(this.categoriesUrl);
  }

  startQuiz(quizId: string): Observable<StartQuizAttemptResponseDto> {
    return this.http.post<StartQuizAttemptResponseDto>(`${this.apiUrl}/${quizId}/start`, {});
  }

  requestReattempt(quizId: string, reason: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/${quizId}/reattempt-request`, { reason });
  }
}
