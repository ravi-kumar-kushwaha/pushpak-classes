import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ProfileService } from './profile.service';
import { environment } from '../../../environments/environment';
import { UserDto } from '../models/user.models';

describe('ProfileService', () => {
  let service: ProfileService;
  let httpTesting: HttpTestingController;

  const mockUser: UserDto = {
    id: 'user-1',
    fullName: 'Jane Doe',
    email: 'jane@pushpakclasses.com',
    phoneNumber: '+919876543210',
    role: 'User',
    isActive: true,
    isEmailVerified: true,
    createdAtUtc: new Date().toISOString()
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ProfileService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(ProfileService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should get current profile', () => {
    service.getProfile().subscribe(user => {
      expect(user.fullName).toBe('Jane Doe');
      expect(user.email).toBe('jane@pushpakclasses.com');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/me`);
    expect(req.request.method).toBe('GET');
    req.flush(mockUser);
  });

  it('should update profile details', () => {
    service.updateProfile({ fullName: 'Jane Smith', phoneNumber: '+919999999999' }).subscribe(user => {
      expect(user.fullName).toBe('Jane Smith');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/me`);
    expect(req.request.method).toBe('PUT');
    req.flush({ ...mockUser, fullName: 'Jane Smith', phoneNumber: '+919999999999' });
  });

  it('should change password', () => {
    service.changePassword({ currentPassword: 'Old@12345', newPassword: 'New@12345' }).subscribe(res => {
      expect(res.message).toBe('Password changed.');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/me/change-password`);
    expect(req.request.method).toBe('POST');
    req.flush({ message: 'Password changed.' });
  });
});
