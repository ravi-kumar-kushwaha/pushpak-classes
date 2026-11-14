import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { HttpEventType } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { AuthService } from '../../../core/services/auth.service';
import { ProfileService } from '../../../core/services/profile.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { ToastService } from '../../../core/services/toast.service';
import { UserDto } from '../../../core/models/user.models';

function passwordPolicyValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (!value) return null;

  const hasUpperCase = /[A-Z]/.test(value);
  const hasLowerCase = /[a-z]/.test(value);
  const hasNumeric = /[0-9]/.test(value);
  const hasSpecial = /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(value);
  const isLengthValid = value.length >= 8;

  const valid = hasUpperCase && hasLowerCase && hasNumeric && hasSpecial && isLengthValid;
  return !valid ? { passwordPolicy: true } : null;
}

function passwordMatchValidator(group: AbstractControl): ValidationErrors | null {
  const password = group.get('newPassword')?.value;
  const confirmPassword = group.get('confirmPassword')?.value;
  return password === confirmPassword ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="container-fluid">
      <!-- Title -->
      <div class="mb-4">
        <h2 class="fw-bold mb-1">My Profile</h2>
        <p class="text-muted mb-0">Manage your PushpakClasses account settings and credentials</p>
      </div>

      <div class="row g-4">
        <!-- Left Column: Avatar & Summary Card -->
        <div class="col-lg-4">
          <div class="card border-0 shadow-sm text-center p-4">
            <div class="position-relative d-inline-block mx-auto mb-3">
              <div 
                class="avatar-large rounded-circle border shadow-sm mx-auto d-flex align-items-center justify-content-center bg-primary text-white overflow-hidden" 
                style="width: 120px; height: 120px;">
                @if (avatarUrl()) {
                  <img 
                    [src]="avatarUrl()" 
                    alt="Profile Photo" 
                    class="w-100 h-100 object-fit-cover" 
                    (error)="onAvatarError($event)" />
                } @else {
                  <span class="fs-1 fw-bold">{{ userInitials() }}</span>
                }
              </div>
            </div>

            <h5 class="fw-bold mb-1">{{ userProfile()?.fullName }}</h5>
            <p class="text-muted small mb-2">{{ userProfile()?.email }}</p>
            <div>
              <span class="badge bg-primary-subtle text-primary px-3 py-1">
                {{ userProfile()?.role || 'Student' }}
              </span>
            </div>

            <!-- Remove Photo Button -->
            @if (userProfile()?.profileImageUrl) {
              <div class="mt-3">
                <button 
                  type="button" 
                  class="btn btn-sm btn-outline-danger" 
                  [disabled]="isRemovingPhoto()"
                  (click)="onRemovePhoto()">
                  @if (isRemovingPhoto()) {
                    <span class="spinner-border spinner-border-sm me-1" role="status"></span>
                  } @else {
                    <i class="bi bi-trash3 me-1"></i>
                  }
                  <span>Remove Current Photo</span>
                </button>
              </div>
            }

            <hr class="my-3">

            <!-- Photo Upload Control -->
            <div class="text-start">
              <label class="form-label small fw-semibold">Upload New Photo</label>
              <input 
                type="file" 
                #fileInput
                class="form-control form-control-sm mb-2" 
                accept="image/png, image/jpeg, image/jpg" 
                (change)="onFileSelected($event)" />
              <div class="text-muted small mb-2" style="font-size: 0.75rem;">
                Max file size: 2 MB. JPG and PNG formats only.
              </div>

              <!-- Upload Progress Bar -->
              @if (uploadProgress() !== null) {
                <div class="progress mb-2" style="height: 6px;">
                  <div 
                    class="progress-bar progress-bar-striped progress-bar-animated bg-success" 
                    role="progressbar" 
                    [style.width.%]="uploadProgress()">
                  </div>
                </div>
                <div class="text-muted small text-center mb-2" style="font-size: 0.75rem;">
                  Uploading: {{ uploadProgress() }}%
                </div>
              }

              @if (selectedFile()) {
                <div class="d-flex gap-2">
                  <button 
                    type="button" 
                    class="btn btn-sm btn-primary flex-grow-1" 
                    [disabled]="isUploadingPhoto()" 
                    (click)="onUploadPhoto(fileInput)">
                    @if (isUploadingPhoto()) {
                      <span class="spinner-border spinner-border-sm me-1"></span> Uploading...
                    } @else {
                      <i class="bi bi-cloud-arrow-up me-1"></i> Save Photo
                    }
                  </button>
                  <button 
                    type="button" 
                    class="btn btn-sm btn-outline-secondary" 
                    [disabled]="isUploadingPhoto()"
                    (click)="cancelPhotoSelection(fileInput)">
                    Cancel
                  </button>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Right Column: Personal Details & Password Change -->
        <div class="col-lg-8">
          <!-- Personal Details Card -->
          <div class="card border-0 shadow-sm mb-4">
            <div class="card-header bg-white py-3 border-bottom">
              <h5 class="fw-bold mb-0">Personal Information</h5>
            </div>
            <div class="card-body p-4">
              <form [formGroup]="detailsForm" (ngSubmit)="onUpdateDetails()">
                <!-- Full Name -->
                <div class="mb-3">
                  <label for="fullName" class="form-label small fw-semibold">Full Name</label>
                  <input 
                    type="text" 
                    id="fullName" 
                    formControlName="fullName" 
                    class="form-control" 
                    [class.is-invalid]="df['fullName'].touched && df['fullName'].invalid" />
                  @if (df['fullName'].touched && df['fullName'].invalid) {
                    <div class="text-danger small mt-1">Full name is required (min 2 characters).</div>
                  }
                </div>

                <!-- Email (Readonly) -->
                <div class="mb-3">
                  <label for="email" class="form-label small fw-semibold">Email Address</label>
                  <input 
                    type="email" 
                    id="email" 
                    [value]="userProfile()?.email" 
                    disabled 
                    class="form-control bg-light" />
                  <div class="text-muted small mt-1" style="font-size: 0.75rem;">
                    Email address cannot be changed directly.
                  </div>
                </div>

                <!-- Phone Number -->
                <div class="mb-3">
                  <label for="phoneNumber" class="form-label small fw-semibold">Phone Number</label>
                  <input 
                    type="tel" 
                    id="phoneNumber" 
                    formControlName="phoneNumber" 
                    class="form-control" 
                    placeholder="+91 9876543210" />
                </div>

                <div class="text-end">
                  <button 
                    type="submit" 
                    class="btn btn-primary px-4" 
                    [disabled]="detailsForm.invalid || isUpdatingDetails()">
                    @if (isUpdatingDetails()) {
                      <span class="spinner-border spinner-border-sm me-1"></span> Saving...
                    } @else {
                      <i class="bi bi-check-lg me-1"></i> Save Changes
                    }
                  </button>
                </div>
              </form>
            </div>
          </div>

          <!-- Change Password Card -->
          <div class="card border-0 shadow-sm">
            <div class="card-header bg-white py-3 border-bottom">
              <h5 class="fw-bold mb-0">Security & Password</h5>
            </div>
            <div class="card-body p-4">
              <form [formGroup]="passwordForm" (ngSubmit)="onChangePassword()">
                <!-- Current Password -->
                <div class="mb-3">
                  <label for="currentPassword" class="form-label small fw-semibold">Current Password</label>
                  <input 
                    type="password" 
                    id="currentPassword" 
                    formControlName="currentPassword" 
                    class="form-control" 
                    [class.is-invalid]="pf['currentPassword'].touched && pf['currentPassword'].invalid" />
                  @if (pf['currentPassword'].touched && pf['currentPassword'].invalid) {
                    <div class="text-danger small mt-1">Current password is required.</div>
                  }
                </div>

                <!-- New Password -->
                <div class="mb-3">
                  <label for="newPassword" class="form-label small fw-semibold">New Password</label>
                  <input 
                    type="password" 
                    id="newPassword" 
                    formControlName="newPassword" 
                    class="form-control" 
                    [class.is-invalid]="pf['newPassword'].touched && pf['newPassword'].invalid" 
                    placeholder="Min 8 chars with upper, lower, digit & special" />
                  @if (pf['newPassword'].touched && pf['newPassword'].invalid) {
                    <div class="text-danger small mt-1">
                      Password must be at least 8 characters and contain uppercase, lowercase, digit, and special character.
                    </div>
                  }
                </div>

                <!-- Confirm Password -->
                <div class="mb-3">
                  <label for="confirmPassword" class="form-label small fw-semibold">Confirm New Password</label>
                  <input 
                    type="password" 
                    id="confirmPassword" 
                    formControlName="confirmPassword" 
                    class="form-control" 
                    [class.is-invalid]="(pf['confirmPassword'].touched && pf['confirmPassword'].invalid) || (passwordForm.errors?.['passwordMismatch'] && pf['confirmPassword'].touched)" />
                  @if (passwordForm.errors?.['passwordMismatch'] && pf['confirmPassword'].touched) {
                    <div class="text-danger small mt-1">Passwords do not match.</div>
                  }
                </div>

                <div class="text-end">
                  <button 
                    type="submit" 
                    class="btn btn-warning px-4 text-dark fw-semibold" 
                    [disabled]="passwordForm.invalid || isChangingPassword()">
                    @if (isChangingPassword()) {
                      <span class="spinner-border spinner-border-sm me-1"></span> Updating...
                    } @else {
                      <i class="bi bi-shield-lock me-1"></i> Update Password
                    }
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class ProfileComponent implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private profileService = inject(ProfileService);
  private confirmService = inject(ConfirmDialogService);
  private toastService = inject(ToastService);

  // Reactive signals for zoneless change detection
  userProfile = signal<UserDto | null>(null);
  selectedFile = signal<File | null>(null);
  previewUrl = signal<string | null>(null);
  uploadProgress = signal<number | null>(null);

  isUpdatingDetails = signal(false);
  isUploadingPhoto = signal(false);
  isRemovingPhoto = signal(false);
  isChangingPassword = signal(false);

  userInitials = computed(() => {
    const name = this.userProfile()?.fullName || 'User';
    return name.split(' ').map(n => n[0]).join('').substring(0, 2).toUpperCase();
  });

  avatarUrl = computed(() => {
    const preview = this.previewUrl();
    if (preview) return preview;
    const url = this.userProfile()?.profileImageUrl;
    if (!url) return null;
    if (url.startsWith('http://') || url.startsWith('https://') || url.startsWith('data:')) return url;
    const backendBase = environment.apiUrl.replace(/\/api$/, '');
    return `${backendBase}${url.startsWith('/') ? '' : '/'}${url}`;
  });

  detailsForm: FormGroup = this.fb.group({
    fullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(200)]],
    phoneNumber: ['']
  });

  passwordForm: FormGroup = this.fb.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, passwordPolicyValidator]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: passwordMatchValidator });

  get df() { return this.detailsForm.controls; }
  get pf() { return this.passwordForm.controls; }

  ngOnInit(): void {
    this.loadProfile();
  }

  loadProfile(): void {
    this.profileService.getProfile().subscribe({
      next: (profile) => {
        this.userProfile.set(profile);
        this.detailsForm.patchValue({
          fullName: profile.fullName,
          phoneNumber: profile.phoneNumber || ''
        });
      },
      error: (err) => {
        this.toastService.showError(err.error?.message || 'Unable to load profile data.', 'Error');
      }
    });
  }

  onAvatarError(event: Event): void {
    const target = event.target as HTMLImageElement;
    target.onerror = null;
    target.src = 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="120" height="120" viewBox="0 0 24 24" fill="%236c757d"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>';
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];

      // Validate format
      const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();
      const isValidExt = ['.jpg', '.jpeg', '.png'].includes(ext);
      const isValidType = ['image/jpeg', 'image/png', 'image/jpg'].includes(file.type) || (file.type === '' && isValidExt);

      if (!isValidType) {
        this.toastService.showError('Only JPG and PNG images are supported.', 'Invalid File');
        input.value = '';
        this.selectedFile.set(null);
        this.previewUrl.set(null);
        return;
      }

      // Validate size (max 2MB)
      if (file.size > 2 * 1024 * 1024) {
        this.toastService.showError('File size exceeds the 2 MB limit.', 'File Too Large');
        input.value = '';
        this.selectedFile.set(null);
        this.previewUrl.set(null);
        return;
      }

      this.selectedFile.set(file);

      // Instant preview
      const reader = new FileReader();
      reader.onload = () => {
        this.previewUrl.set(reader.result as string);
      };
      reader.readAsDataURL(file);
    }
  }

  cancelPhotoSelection(input: HTMLInputElement): void {
    this.selectedFile.set(null);
    this.previewUrl.set(null);
    this.uploadProgress.set(null);
    input.value = '';
  }

  onUploadPhoto(input: HTMLInputElement): void {
    const file = this.selectedFile();
    if (!file || this.isUploadingPhoto()) return;

    this.isUploadingPhoto.set(true);
    this.uploadProgress.set(0);

    this.profileService.uploadPhotoWithProgress(file).subscribe({
      next: (event) => {
        if (event.type === HttpEventType.UploadProgress && event.total) {
          const percentDone = Math.round((100 * event.loaded) / event.total);
          this.uploadProgress.set(percentDone);
        } else if (event.type === HttpEventType.Response && event.body) {
          this.isUploadingPhoto.set(false);
          this.uploadProgress.set(null);
          this.selectedFile.set(null);
          input.value = '';

          const timestamp = Date.now();
          const rawUrl = event.body.profileImageUrl;
          const newImageUrl = rawUrl ? `${rawUrl}${rawUrl.includes('?') ? '&' : '?'}v=${timestamp}` : rawUrl;
          this.userProfile.update(u => u ? { ...u, profileImageUrl: newImageUrl } : null);
          this.previewUrl.set(null);

          this.authService.updateCurrentUser({ profileImageUrl: newImageUrl });

          this.toastService.showSuccess('Profile photo updated successfully!', 'Saved');
        }
      },
      error: (err) => {
        this.isUploadingPhoto.set(false);
        this.uploadProgress.set(null);
        this.toastService.showError(err.error?.message || 'Unable to upload profile photo.', 'Upload Failed');
      }
    });
  }

  async onRemovePhoto(): Promise<void> {
    if (this.isRemovingPhoto()) return;

    const confirmed = await this.confirmService.confirm({
      title: 'Remove Profile Photo',
      message: 'Are you sure you want to delete your profile photo? Your avatar will revert to initials.',
      confirmText: 'Remove Photo',
      cancelText: 'Cancel'
    });

    if (confirmed) {
      this.isRemovingPhoto.set(true);
      this.profileService.deletePhoto().subscribe({
        next: () => {
          this.isRemovingPhoto.set(false);
          this.userProfile.update(u => u ? { ...u, profileImageUrl: null } : null);
          this.previewUrl.set(null);

          this.authService.updateCurrentUser({ profileImageUrl: null });

          this.toastService.showSuccess('Profile photo removed successfully.', 'Removed');
        },
        error: (err) => {
          this.isRemovingPhoto.set(false);
          this.toastService.showError(err.error?.message || 'Unable to remove profile photo.', 'Error');
        }
      });
    }
  }

  onUpdateDetails(): void {
    if (this.detailsForm.invalid || this.isUpdatingDetails()) {
      this.detailsForm.markAllAsTouched();
      return;
    }

    this.isUpdatingDetails.set(true);
    const { fullName, phoneNumber } = this.detailsForm.value;

    this.profileService.updateProfile({ fullName, phoneNumber }).subscribe({
      next: (updated) => {
        this.isUpdatingDetails.set(false);
        this.userProfile.set(updated);
        // Patch form with server response
        this.detailsForm.patchValue({
          fullName: updated.fullName,
          phoneNumber: updated.phoneNumber || ''
        });
        // Update user state in AuthService as well
        this.authService.currentUser.set(updated);
        this.toastService.showSuccess('Personal details updated successfully!', 'Saved');
      },
      error: (err) => {
        this.isUpdatingDetails.set(false);
        this.toastService.showError(err.error?.message || 'Failed to update personal details.', 'Error');
      }
    });
  }

  onChangePassword(): void {
    if (this.passwordForm.invalid || this.isChangingPassword()) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    this.isChangingPassword.set(true);
    const { currentPassword, newPassword, confirmPassword } = this.passwordForm.value;

    this.profileService.changePassword({
      currentPassword,
      newPassword,
      confirmNewPassword: confirmPassword
    }).subscribe({
      next: () => {
        this.isChangingPassword.set(false);
        this.passwordForm.reset();
        this.toastService.showSuccess('Password has been changed successfully!', 'Security Updated');
      },
      error: (err) => {
        this.isChangingPassword.set(false);
        this.toastService.showError(err.error?.message || 'Unable to update password. Check your current password.', 'Error');
      }
    });
  }
}
