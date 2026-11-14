import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { UserDto } from '../models/user.models';
import { AuthMessageResponseDto } from '../models/auth.models';

export interface UpdateProfileRequestDto {
  fullName: string;
  phoneNumber?: string;
}

export interface ChangePasswordRequestDto {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword?: string;
  confirmPassword?: string;
}

export interface ProfileImageResponseDto {
  profileImageUrl: string;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class ProfileService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.apiUrl}/me`;

  getProfile(): Observable<UserDto> {
    return this.http.get<UserDto>(this.apiUrl);
  }

  updateProfile(request: UpdateProfileRequestDto): Observable<UserDto> {
    return this.http.put<UserDto>(this.apiUrl, request);
  }

  uploadPhoto(file: File): Observable<ProfileImageResponseDto> {
    const formData = new FormData();
    formData.append('photo', file, file.name);
    return this.http.post<ProfileImageResponseDto>(`${this.apiUrl}/photo`, formData);
  }

  uploadPhotoWithProgress(file: File): Observable<HttpEvent<ProfileImageResponseDto>> {
    const formData = new FormData();
    formData.append('photo', file, file.name);
    return this.http.post<ProfileImageResponseDto>(`${this.apiUrl}/photo`, formData, {
      reportProgress: true,
      observe: 'events'
    });
  }

  deletePhoto(): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/photo`);
  }

  changePassword(request: ChangePasswordRequestDto): Observable<AuthMessageResponseDto> {
    return this.http.post<AuthMessageResponseDto>(`${this.apiUrl}/change-password`, {
      currentPassword: request.currentPassword,
      newPassword: request.newPassword,
      confirmNewPassword: request.confirmNewPassword || request.confirmPassword
    });
  }
}
