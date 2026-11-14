export type QuestionType = 'SingleChoice' | 'MultiChoice' | 'TrueFalse';

export interface AttemptOptionDto {
  id: string;
  text: string;
  textEn?: string;
  textHi?: string;
}

export interface AttemptQuestionDto {
  id: string;
  text: string;
  textEn?: string;
  textHi?: string;
  type: QuestionType | string;
  marks: number;
  order: number;
  options: AttemptOptionDto[];
}

export interface QuestionAnswerDto {
  questionId: string;
  selectedOptionIds: string[];
}

export interface SaveAttemptAnswersRequestDto {
  answers: QuestionAnswerDto[];
}

export interface StartQuizAttemptResponseDto {
  attemptId: string;
  quizId: string;
  quizTitle: string;
  durationMinutes: number;
  startedAtUtc: string;
  endsAtUtc: string;
  serverTimeUtc: string;
  questions: AttemptQuestionDto[];
}

export interface ActiveAttemptDetailsDto {
  attemptId: string;
  quizId: string;
  quizTitle: string;
  durationMinutes: number;
  startedAtUtc: string;
  endsAtUtc: string;
  serverTimeUtc: string;
  status: string;
  tabSwitchCount: number;
  questions: AttemptQuestionDto[];
  draftAnswers: QuestionAnswerDto[];
}

export interface SubmitAttemptResponseDto {
  attemptId: string;
  status: string;
  submittedAtUtc?: string;
  message: string;
}

export interface TabSwitchResponseDto {
  attemptId: string;
  tabSwitchCount: number;
  warningMessage: string;
}
