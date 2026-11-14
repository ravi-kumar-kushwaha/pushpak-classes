export interface AvailableQuizDto {
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
  allowedAttempts: number;
  attemptsUsed: number;
  attemptsRemaining: number;
  attemptsLeft?: number;
  status: 'Upcoming' | 'Open' | 'Resume' | 'Attempted' | 'Expired' | string;
  reattemptStatus?: 'Pending' | 'Approved' | 'Rejected' | string | null;
  reattemptRequestId?: string | null;
  hasActiveAttempt: boolean;
  activeAttemptId?: string;
  questionCount: number;
  totalMarks: number;
}

export interface ReattemptRequestDto {
  id: string;
  quizId: string;
  quizTitle: string;
  userId: string;
  userFullName: string;
  userEmail: string;
  reason: string;
  status: 'Pending' | 'Approved' | 'Rejected' | string;
  reviewedByUserId?: string | null;
  reviewedByUserName?: string | null;
  createdAtUtc: string;
  reviewedAtUtc?: string | null;
  reviewNotes?: string | null;
}
