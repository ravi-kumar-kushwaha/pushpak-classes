using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Reports.Dtos;

namespace PushpakClasses.Application.Common.Interfaces;

public interface IReportService
{
    Task<PagedResult<UserAttemptHistoryDto>> GetUserAttemptsAsync(QuizQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<AttemptReviewDto> GetAttemptReviewAsync(Guid attemptId, CancellationToken cancellationToken = default);
    Task<UserDashboardDto> GetUserDashboardAsync(CancellationToken cancellationToken = default);
    Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<AdminAttemptDto>> GetAdminAttemptsAsync(AdminAttemptQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<QuizLeaderboardDto> GetQuizLeaderboardAsync(Guid quizId, CancellationToken cancellationToken = default);
    Task<(byte[] FileBytes, string FileName)> ExportQuizResultsToExcelAsync(Guid quizId, CancellationToken cancellationToken = default);
}
