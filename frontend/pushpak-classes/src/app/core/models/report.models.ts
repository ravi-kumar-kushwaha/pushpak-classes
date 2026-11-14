export interface UserAttemptHistoryDto {
  id: string;
  quizId: string;
  quizTitle: string;
  categoryName: string;
  startedAtUtc: string;
  submittedAtUtc?: string;
  status: string;
  score?: number | null;
  totalMarks: number;
  percent?: number | null;
  passed?: boolean | null;
  tabSwitchCount: number;
}

export interface UserDashboardDto {
  totalAttempts: number;
  averageScore: number;
  bestScore: number;
  passRate: number;
  totalQuizzesAttempted: number;
  recentAttempts: UserAttemptHistoryDto[];
}

export interface OptionReviewDto {
  optionId: string;
  text: string;
  textEn?: string;
  textHi?: string;
  isCorrect: boolean;
  isSelected?: boolean;
  isSelectedByStudent?: boolean;
}

export interface QuestionReviewDto {
  questionId: string;
  text: string;
  textEn?: string;
  textHi?: string;
  type: string;
  marks: number;
  marksAwarded: number;
  isCorrect: boolean;
  options: OptionReviewDto[];
}

export interface AttemptReviewDto {
  attemptId: string;
  quizId: string;
  quizTitle: string;
  userId?: string;
  userFullName?: string;
  userEmail?: string;
  status: string;
  score?: number | null;
  totalMarks: number;
  percent?: number | null;
  passed?: boolean | null;
  startedAtUtc: string;
  submittedAtUtc?: string;
  tabSwitchCount: number;
  isScoringPending?: boolean;
  questions: QuestionReviewDto[];
}
