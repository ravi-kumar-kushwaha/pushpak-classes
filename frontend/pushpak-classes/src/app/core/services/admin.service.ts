import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/api.models';
import { AttemptReviewDto } from '../models/report.models';
import { ReattemptRequestDto } from '../models/quiz.models';
import {
  AdminDashboardDto,
  AdminCategoryDto,
  AdminQuizDto,
  AdminUserDto,
  AdminAttemptDto,
  CreateQuizRequestDto,
  UpdateQuizRequestDto,
  AdminQuestionDto,
  CreateQuestionRequestDto,
  UpdateQuestionRequestDto
} from '../models/admin.models';

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private http = inject(HttpClient);
  private adminUrl = `${environment.apiUrl}/admin`;

  // --- Dashboard ---
  getDashboard(): Observable<AdminDashboardDto> {
    return this.http.get<AdminDashboardDto>(`${this.adminUrl}/dashboard`);
  }

  // --- Categories ---
  getCategories(): Observable<AdminCategoryDto[]> {
    return this.http.get<PagedResult<AdminCategoryDto>>(`${this.adminUrl}/categories?pageSize=100`).pipe(
      map(res => res.items || [])
    );
  }

  getCategoriesPaged(
    pageNumber: number = 1,
    pageSize: number = 10,
    search?: string,
    sortBy: string = 'Name',
    sortDir: string = 'asc'
  ): Observable<PagedResult<AdminCategoryDto>> {
    let params = new HttpParams()
      .set('page', pageNumber.toString())
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString())
      .set('sortBy', sortBy)
      .set('sortDir', sortDir);

    if (search && search.trim()) {
      params = params.set('search', search.trim()).set('searchQuery', search.trim());
    }

    return this.http.get<PagedResult<AdminCategoryDto>>(`${this.adminUrl}/categories`, { params });
  }

  createCategory(name: string): Observable<AdminCategoryDto> {
    return this.http.post<AdminCategoryDto>(`${this.adminUrl}/categories`, { name });
  }

  // --- Quizzes ---
  getQuizzes(
    pageNumber: number = 1,
    pageSize: number = 10,
    search?: string,
    categoryId?: string,
    isPublished?: boolean
  ): Observable<PagedResult<AdminQuizDto>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (search && search.trim()) {
      params = params.set('search', search.trim());
    }
    if (categoryId) {
      params = params.set('categoryId', categoryId);
    }
    if (isPublished !== undefined && isPublished !== null) {
      params = params.set('isPublished', isPublished.toString());
    }

    return this.http.get<PagedResult<AdminQuizDto>>(`${this.adminUrl}/quizzes`, { params });
  }

  getQuizById(id: string): Observable<AdminQuizDto> {
    return this.http.get<AdminQuizDto>(`${this.adminUrl}/quizzes/${id}`);
  }

  createQuiz(request: CreateQuizRequestDto): Observable<AdminQuizDto> {
    return this.http.post<AdminQuizDto>(`${this.adminUrl}/quizzes`, request);
  }

  updateQuiz(id: string, request: UpdateQuizRequestDto): Observable<AdminQuizDto> {
    return this.http.put<AdminQuizDto>(`${this.adminUrl}/quizzes/${id}`, request);
  }

  deleteQuiz(id: string): Observable<void> {
    return this.http.delete<void>(`${this.adminUrl}/quizzes/${id}`);
  }

  toggleQuizPublish(quizId: string, isPublished: boolean): Observable<AdminQuizDto> {
    return this.http.patch<AdminQuizDto>(`${this.adminUrl}/quizzes/${quizId}/publish`, { isPublished });
  }

  // --- Questions ---
  getQuestions(quizId: string): Observable<AdminQuestionDto[]> {
    return this.http.get<AdminQuestionDto[]>(`${this.adminUrl}/quizzes/${quizId}/questions`);
  }

  createQuestion(quizId: string, request: CreateQuestionRequestDto): Observable<AdminQuestionDto> {
    return this.http.post<AdminQuestionDto>(`${this.adminUrl}/quizzes/${quizId}/questions`, request);
  }

  updateQuestion(quizId: string, questionId: string, request: UpdateQuestionRequestDto): Observable<AdminQuestionDto> {
    return this.http.put<AdminQuestionDto>(`${this.adminUrl}/quizzes/${quizId}/questions/${questionId}`, request);
  }

  deleteQuestion(quizId: string, questionId: string): Observable<void> {
    return this.http.delete<void>(`${this.adminUrl}/quizzes/${quizId}/questions/${questionId}`);
  }

  reorderQuestions(quizId: string, questionIds: string[]): Observable<void> {
    return this.http.put<void>(`${this.adminUrl}/quizzes/${quizId}/questions/reorder`, { questionIds });
  }

  // --- Users ---
  getUsers(
    pageNumber: number = 1,
    pageSize: number = 10,
    search?: string,
    role?: string,
    isActive?: boolean
  ): Observable<PagedResult<AdminUserDto>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (search && search.trim()) {
      params = params.set('search', search.trim());
    }
    if (role) {
      params = params.set('role', role);
    }
    if (isActive !== undefined && isActive !== null) {
      params = params.set('isActive', isActive.toString());
    }

    return this.http.get<PagedResult<AdminUserDto>>(`${this.adminUrl}/users`, { params });
  }

  toggleUserStatus(userId: string, isActive: boolean): Observable<void> {
    return this.http.patch<void>(`${this.adminUrl}/users/${userId}/status`, { isActive });
  }

  // --- Attempts ---
  getAttempts(
    pageNumber: number = 1,
    pageSize: number = 10,
    search?: string,
    quizId?: string,
    userId?: string,
    status?: string,
    fromDateUtc?: string,
    toDateUtc?: string
  ): Observable<PagedResult<AdminAttemptDto>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (search && search.trim()) {
      params = params.set('searchQuery', search.trim());
    }
    if (quizId) {
      params = params.set('quizId', quizId);
    }
    if (userId) {
      params = params.set('userId', userId);
    }
    if (status) {
      params = params.set('status', status);
    }
    if (fromDateUtc) {
      params = params.set('fromDateUtc', fromDateUtc);
    }
    if (toDateUtc) {
      params = params.set('toDateUtc', toDateUtc);
    }

    return this.http.get<PagedResult<AdminAttemptDto>>(`${this.adminUrl}/attempts`, { params });
  }

  getAttemptReview(attemptId: string): Observable<AttemptReviewDto> {
    return this.http.get<AttemptReviewDto>(`${this.adminUrl}/attempts/${attemptId}`);
  }

  // --- Reattempt Requests ---
  getReattemptRequests(
    pageNumber: number = 1,
    pageSize: number = 10,
    search?: string,
    status?: string,
    quizId?: string,
    userId?: string
  ): Observable<PagedResult<ReattemptRequestDto>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (search && search.trim()) {
      params = params.set('searchQuery', search.trim());
    }
    if (status && status.trim() && status.toLowerCase() !== 'all') {
      params = params.set('status', status.trim());
    }
    if (quizId) {
      params = params.set('quizId', quizId);
    }
    if (userId) {
      params = params.set('userId', userId);
    }

    return this.http.get<PagedResult<ReattemptRequestDto>>(`${this.adminUrl}/reattempt-requests`, { params });
  }

  reviewReattemptRequest(id: string, status: 'Approved' | 'Rejected', reviewNotes?: string): Observable<ReattemptRequestDto> {
    return this.http.patch<ReattemptRequestDto>(`${this.adminUrl}/reattempt-requests/${id}`, {
      status,
      reviewNotes
    });
  }
}
