using System.Net;
using Microsoft.EntityFrameworkCore;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PushpakClasses.Infrastructure.Services;

public class PdfCertificateService : IPdfCertificateService
{
    private readonly IPushpakClassesDbContext _context;

    public PdfCertificateService(IPushpakClassesDbContext context)
    {
        _context = context;
        // Set QuestPDF license
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<(byte[] FileBytes, string FileName)> GenerateCertificateAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var attempt = await _context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Quiz)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken);

        if (attempt == null)
        {
            throw new AuthException("Quiz attempt not found.", HttpStatusCode.NotFound);
        }

        if (!attempt.Passed)
        {
            throw new AuthException("Certificate is only available for passed quiz attempts.", HttpStatusCode.BadRequest);
        }

        var studentName = attempt.User?.FullName ?? "Student";
        var quizTitle = attempt.Quiz?.Title ?? "Quiz";
        var dateStr = (attempt.SubmittedAtUtc ?? attempt.EndsAtUtc).ToString("MMMM dd, yyyy");
        var certificateId = attempt.Id.ToString()[..8].ToUpperInvariant();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(35);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Lato"));

                page.Content().Border(3).BorderColor(Colors.Blue.Darken2).Padding(25).Column(col =>
                {
                    col.Item().AlignCenter().Text("PUSHPAKCLASSES")
                        .FontSize(24).Bold().FontColor(Colors.Blue.Darken3);

                    col.Item().AlignCenter().PaddingTop(5).Text("CERTIFICATE OF ACHIEVEMENT")
                        .FontSize(18).SemiBold().FontColor(Colors.Grey.Darken3);

                    col.Item().PaddingTop(20).AlignCenter().Text("This is proudly presented to")
                        .FontSize(14).Italic().FontColor(Colors.Grey.Darken1);

                    col.Item().AlignCenter().PaddingTop(10).Text(studentName)
                        .FontSize(28).Bold().FontColor(Colors.Blue.Darken4);

                    col.Item().AlignCenter().PaddingTop(10).Text("for successfully completing and passing the examination:")
                        .FontSize(14).FontColor(Colors.Grey.Darken2);

                    col.Item().AlignCenter().PaddingTop(8).Text(quizTitle)
                        .FontSize(20).SemiBold().FontColor(Colors.Black);

                    col.Item().AlignCenter().PaddingTop(15).Row(row =>
                    {
                        row.AutoItem().Text($"Final Score: {attempt.Score} / {attempt.TotalMarks}  |  Percentage: {attempt.Percent}%  |  Status: PASSED")
                            .FontSize(13).Bold().FontColor(Colors.Green.Darken2);
                    });

                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Issue Date: {dateStr}").FontSize(11).FontColor(Colors.Grey.Darken2);
                            c.Item().Text($"Certificate ID: PK-{certificateId}").FontSize(10).FontColor(Colors.Grey.Darken1);
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("PushpakClasses Academic Board").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                            c.Item().Text("Authorized Digital Credential").FontSize(10).Italic().FontColor(Colors.Grey.Darken2);
                        });
                    });
                });
            });
        });

        var pdfBytes = document.GeneratePdf();
        var sanitizedQuizTitle = string.Join("_", quizTitle.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        var fileName = $"Certificate_{sanitizedQuizTitle}_{certificateId}.pdf";

        return (pdfBytes, fileName);
    }
}
