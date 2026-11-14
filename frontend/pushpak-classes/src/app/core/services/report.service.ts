import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { UserDashboardDto, UserAttemptHistoryDto } from '../models/report.models';
import { PagedResult } from '../models/api.models';

@Injectable({
  providedIn: 'root'
})
export class ReportService {
  private http = inject(HttpClient);
  private meUrl = `${environment.apiUrl}/me`;

  getUserDashboard(): Observable<UserDashboardDto> {
    return this.http.get<UserDashboardDto>(`${this.meUrl}/dashboard`);
  }

  getUserAttempts(
    pageNumber: number = 1,
    pageSize: number = 10,
    search?: string,
    sortBy?: string,
    isDescending?: boolean
  ): Observable<PagedResult<UserAttemptHistoryDto>> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (search && search.trim()) {
      params = params.set('searchQuery', search.trim());
      params = params.set('search', search.trim());
    }
    if (sortBy) {
      params = params.set('sortBy', sortBy);
      if (isDescending !== undefined) {
        params = params.set('isDescending', isDescending.toString());
      }
    }

    return this.http.get<PagedResult<UserAttemptHistoryDto>>(`${this.meUrl}/attempts`, { params });
  }

  getMyAttempts(
    pageNumber: number = 1,
    pageSize: number = 10,
    search?: string,
    sortBy?: string,
    isDescending?: boolean
  ): Observable<PagedResult<UserAttemptHistoryDto>> {
    return this.getUserAttempts(pageNumber, pageSize, search, sortBy, isDescending);
  }
}
