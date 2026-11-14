import { UserDto } from './user.models';

export interface LoginRequestDto {
  email: string;
  password: string;
}

export interface SignupRequestDto {
  fullName: string;
  email: string;
  phoneNumber?: string;
  password: string;
  confirmPassword: string;
}

export interface RefreshTokenRequestDto {
  accessToken: string;
  refreshToken: string;
}

export interface LogoutRequestDto {
  refreshToken: string;
}

export interface ForgotPasswordRequestDto {
  email: string;
}

export interface ResetPasswordRequestDto {
  token: string;
  newPassword: string;
  email?: string;
  confirmNewPassword?: string;
}

export interface VerifyEmailRequestDto {
  token: string;
  email?: string;
}

export interface AuthResponseDto {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string;
  user: UserDto;
}

export interface AuthMessageResponseDto {
  message: string;
  success?: boolean;
}
