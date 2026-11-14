using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;

namespace PushpakClasses.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailSettings> emailSettings, ILogger<SmtpEmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            message.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = bodyHtml
            };

            using var client = new SmtpClient();
            var secureSocketOptions = _emailSettings.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_emailSettings.SmtpUserName) && !string.IsNullOrWhiteSpace(_emailSettings.SmtpPassword))
            {
                await client.AuthenticateAsync(_emailSettings.SmtpUserName, _emailSettings.SmtpPassword, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {ToEmail} with subject '{Subject}'.", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail} via SMTP. Fallback to console log output.", toEmail);
            _logger.LogWarning(
                "\n==================== [FALLBACK LOG EMAIL] ====================\n" +
                "TO: {ToEmail}\n" +
                "SUBJECT: {Subject}\n" +
                "BODY:\n{BodyHtml}\n" +
                "=============================================================",
                toEmail, subject, bodyHtml);
        }
    }

    public Task SendQuizResultEmailAsync(
        string toEmail,
        string userName,
        string quizTitle,
        decimal score,
        decimal totalMarks,
        decimal percent,
        bool passed,
        Guid attemptId,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Your Quiz Result: {quizTitle} - PushpakClasses";
        var outcome = passed ? "PASSED 🎉" : "FAILED";
        var bodyHtml = $@"
            <h2>PushpakClasses Quiz Result</h2>
            <p>Dear {userName},</p>
            <p>Your results for <strong>{quizTitle}</strong> are ready:</p>
            <ul>
                <li><strong>Status:</strong> {outcome}</li>
                <li><strong>Score:</strong> {score} / {totalMarks}</li>
                <li><strong>Percentage:</strong> {percent}%</li>
            </ul>
            <p>Review your questions and answers at: <a href=""{_emailSettings.AppBaseUrl}/attempts/{attemptId}/review"">View Review</a></p>
        ";

        return SendEmailAsync(toEmail, subject, bodyHtml, cancellationToken);
    }

    public Task SendNewQuizPublishedEmailAsync(
        string toEmail,
        string userName,
        string quizTitle,
        string categoryName,
        int durationMinutes,
        decimal passingPercent,
        DateTime startAtUtc,
        DateTime endAtUtc,
        Guid quizId,
        CancellationToken cancellationToken = default)
    {
        var subject = $"New Quiz Available: {quizTitle} - PushpakClasses";
        var bodyHtml = $@"
            <h2>New Quiz Published on PushpakClasses</h2>
            <p>Dear {userName},</p>
            <p>A new quiz <strong>{quizTitle}</strong> in category <em>{categoryName}</em> is now open for attempts:</p>
            <ul>
                <li><strong>Duration:</strong> {durationMinutes} minutes</li>
                <li><strong>Passing Score:</strong> {passingPercent}%</li>
                <li><strong>Available From:</strong> {startAtUtc:yyyy-MM-dd HH:mm} UTC</li>
                <li><strong>Closes At:</strong> {endAtUtc:yyyy-MM-dd HH:mm} UTC</li>
            </ul>
            <p>Attempt the quiz now: <a href=""{_emailSettings.AppBaseUrl}/quizzes"">Take Quiz</a></p>
        ";

        return SendEmailAsync(toEmail, subject, bodyHtml, cancellationToken);
    }

    public Task SendReattemptDecisionEmailAsync(
        string toEmail,
        string userName,
        string quizTitle,
        bool approved,
        string? reviewNotes,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Reattempt Request {(approved ? "Approved" : "Rejected")}: {quizTitle} - PushpakClasses";
        var decision = approved ? "APPROVED 🎉" : "REJECTED";
        var bodyHtml = $@"
            <h2>PushpakClasses Quiz Reattempt Decision</h2>
            <p>Dear {userName},</p>
            <p>Your request to reattempt <strong>{quizTitle}</strong> has been <strong>{decision}</strong>.</p>
            {(string.IsNullOrWhiteSpace(reviewNotes) ? "" : $"<p><strong>Admin Notes:</strong> {reviewNotes}</p>")}
            {(approved ? "<p>You have been granted 1 extra attempt. You may now return to PushpakClasses to take the quiz!</p>" : "<p>If you have any questions, please contact your administrator.</p>")}
        ";

        return SendEmailAsync(toEmail, subject, bodyHtml, cancellationToken);
    }
}
