export type UserRole = 'Admin' | 'User';

export interface UserDto {
  id: string;
  fullName: string;
  email: string;
  phoneNumber?: string;
  profileImageUrl?: string | null;
  role: UserRole;
  isActive: boolean;
  isEmailVerified: boolean;
  createdAtUtc: string;
}
