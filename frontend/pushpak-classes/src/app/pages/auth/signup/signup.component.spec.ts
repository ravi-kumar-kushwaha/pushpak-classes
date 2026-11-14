import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { SignupComponent } from './signup.component';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';

describe('SignupComponent', () => {
  let component: SignupComponent;
  let fixture: ComponentFixture<SignupComponent>;
  let authServiceSpy: { signup: ReturnType<typeof vi.fn> };
  let toastServiceSpy: { showSuccess: ReturnType<typeof vi.fn>; showError: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    authServiceSpy = {
      signup: vi.fn()
    };
    toastServiceSpy = {
      showSuccess: vi.fn(),
      showError: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [SignupComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: authServiceSpy },
        { provide: ToastService, useValue: toastServiceSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(SignupComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with an invalid empty form', () => {
    expect(component).toBeTruthy();
    expect(component.signupForm.valid).toBe(false);
    expect(component.signupForm.get('fullName')?.valid).toBe(false);
    expect(component.signupForm.get('email')?.valid).toBe(false);
    expect(component.signupForm.get('password')?.valid).toBe(false);
    expect(component.signupForm.get('confirmPassword')?.valid).toBe(false);
  });

  it('should invalidate weak passwords and validate strong passwords', () => {
    const passwordCtrl = component.signupForm.get('password');

    // Missing uppercase and special
    passwordCtrl?.setValue('weakpassword123');
    expect(passwordCtrl?.hasError('passwordPolicy')).toBe(true);

    // Missing digit
    passwordCtrl?.setValue('WeakPassword!');
    expect(passwordCtrl?.hasError('passwordPolicy')).toBe(true);

    // Less than 8 characters
    passwordCtrl?.setValue('Wp1!');
    expect(passwordCtrl?.hasError('passwordPolicy')).toBe(true);

    // Valid strong password
    passwordCtrl?.setValue('Password@123');
    expect(passwordCtrl?.valid).toBe(true);
  });

  it('should detect password mismatch between password and confirmPassword', () => {
    component.signupForm.patchValue({
      password: 'Password@123',
      confirmPassword: 'MismatchPassword@123'
    });

    expect(component.signupForm.hasError('passwordMismatch')).toBe(true);

    component.signupForm.patchValue({
      confirmPassword: 'Password@123'
    });

    expect(component.signupForm.hasError('passwordMismatch')).toBe(false);
  });

  it('should send complete payload including confirmPassword on valid submit', () => {
    authServiceSpy.signup.mockReturnValue(of({ message: 'Registration successful!' }));

    component.signupForm.setValue({
      fullName: 'Alice Walker',
      email: 'alice@pushpakclasses.com',
      phoneNumber: '+919876543210',
      password: 'Password@123',
      confirmPassword: 'Password@123'
    });

    expect(component.signupForm.valid).toBe(true);

    component.onSubmit();

    expect(authServiceSpy.signup).toHaveBeenCalledWith({
      fullName: 'Alice Walker',
      email: 'alice@pushpakclasses.com',
      phoneNumber: '+919876543210',
      password: 'Password@123',
      confirmPassword: 'Password@123'
    });

    expect(component.registrationSuccess()).toBe(true);
    expect(component.registeredEmail()).toBe('alice@pushpakclasses.com');
  });

  it('should handle 409 Conflict duplicate email error message appropriately', () => {
    const conflictError = {
      status: 409,
      error: {
        detail: 'An account with this email address already exists.'
      }
    };
    authServiceSpy.signup.mockReturnValue(throwError(() => conflictError));

    component.signupForm.setValue({
      fullName: 'Duplicate User',
      email: 'existing@pushpakclasses.com',
      phoneNumber: '',
      password: 'Password@123',
      confirmPassword: 'Password@123'
    });

    component.onSubmit();

    expect(component.registrationSuccess()).toBe(false);
    expect(component.errorMessage()).toContain('already exists');
  });
});
