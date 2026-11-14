namespace PushpakClasses.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken cancellationToken = default);
    Task SendQuizResultEmailAsync(string toEmail, string userName, string quizTitle, decimal score, decimal totalMarks, decimal percent, bool passed, Guid attemptId, CancellationToken cancellationToken = default);
    Task SendNewQuizPublishedEmailAsync(string toEmail, string userName, string quizTitle, string categoryName, int durationMinutes, decimal passingPercent, DateTime startAtUtc, DateTime endAtUtc, Guid quizId, CancellationToken cancellationToken = default);
    Task SendReattemptDecisionEmailAsync(string toEmail, string userName, string quizTitle, bool approved, string? reviewNotes, CancellationToken cancellationToken = default);
}
