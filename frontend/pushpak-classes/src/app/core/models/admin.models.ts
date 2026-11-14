export interface DailyAttemptStatDto {
  dateUtc: string;
  attemptCount: number;
  averageScore: number;
}

export interface AdminDashboardDto {
  totalUsers: number;
  totalQuizzes: number;
  totalAttempts: number;
  averageScore: number;
  trendLast30Days: DailyAttemptStatDto[];
}

export interface AdminCategoryDto {
  id: string;
  name: string;
  quizCount: number;
}

export interface AdminQuizDto {
  id: string;
  title: string;
  description?: string;
  categoryId: string;
  categoryName: string;
  durationMinutes: number;
  startAtUtc: string;
  endAtUtc: string;
  passingPercent: number;
  maxAttempts: number;
  shuffleQuestions: boolean;
  shuffleOptions: boolean;
  isPublished: boolean;
  questionCount: number;
  totalMarks: number;
  attemptCount?: number;
  createdAtUtc?: string;
}

export interface CreateQuizRequestDto {
  title: string;
  description?: string;
  categoryId: string;
  durationMinutes: number;
  startAtUtc: string;
  endAtUtc: string;
  passingPercent: number;
  maxAttempts: number;
  shuffleQuestions: boolean;
  shuffleOptions: boolean;
  isPublished?: boolean;
}

export interface UpdateQuizRequestDto {
  title: string;
  description?: string;
  categoryId: string;
  durationMinutes: number;
  startAtUtc: string;
  endAtUtc: string;
  passingPercent: number;
  maxAttempts: number;
  shuffleQuestions: boolean;
  shuffleOptions: boolean;
}

export enum AdminQuestionType {
  SingleChoice = 1,
  MultiChoice = 2,
  TrueFalse = 3
}

export interface AdminOptionDto {
  id?: string;
  text: string;
  textEn?: string;
  textHi?: string;
  isCorrect: boolean;
}

export interface AdminQuestionDto {
  id: string;
  quizId: string;
  text: string;
  textEn?: string;
  textHi?: string;
  type: AdminQuestionType;
  marks: number;
  order: number;
  options: AdminOptionDto[];
}

export interface CreateQuestionRequestDto {
  text: string;
  textEn?: string;
  textHi?: string;
  type: AdminQuestionType;
  marks: number;
  order: number;
  options: AdminOptionDto[];
}

export interface UpdateQuestionRequestDto {
  text: string;
  textEn?: string;
  textHi?: string;
  type: AdminQuestionType;
  marks: number;
  order: number;
  options: AdminOptionDto[];
}

export interface AdminUserDto {
  id: string;
  fullName: string;
  email: string;
  phoneNumber?: string;
  profileImageUrl?: string;
  role: string;
  isActive: boolean;
  isEmailVerified: boolean;
  createdAtUtc: string;
}

export interface AdminAttemptDto {
  attemptId: string;
  quizId: string;
  quizTitle: string;
  userId: string;
  userFullName: string;
  userEmail: string;
  startedAtUtc: string;
  submittedAtUtc?: string;
  status: string;
  score: number;
  totalMarks: number;
  percent: number;
  passed: boolean;
  tabSwitchCount: number;
}
