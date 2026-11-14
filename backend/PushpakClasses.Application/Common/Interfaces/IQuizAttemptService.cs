using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Attempts.Dtos;
using PushpakClasses.Application.Features.Reattempts.Dtos;

namespace PushpakClasses.Application.Common.Interfaces;

public interface IQuizAttemptService
{
    Task<PagedResult<AvailableQuizDto>> GetAvailableQuizzesAsync(QuizQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<StartQuizAttemptResponseDto> StartQuizAttemptAsync(Guid quizId, CancellationToken cancellationToken = default);
    Task<AuthMessageResponseDto> SaveDraftAnswersAsync(Guid attemptId, SaveAttemptAnswersRequestDto request, CancellationToken cancellationToken = default);
    Task<SubmitAttemptResponseDto> SubmitQuizAttemptAsync(Guid attemptId, SaveAttemptAnswersRequestDto? directAnswers = null, CancellationToken cancellationToken = default);
    Task<TabSwitchResponseDto> RecordTabSwitchAsync(Guid attemptId, CancellationToken cancellationToken = default);
    Task<AttemptResultDto> GetAttemptResultAsync(Guid attemptId, CancellationToken cancellationToken = default);
    Task<ActiveAttemptDetailsDto> GetActiveAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default);
    Task<ReattemptRequestDto> CreateReattemptRequestAsync(Guid quizId, CreateReattemptRequestDto request, CancellationToken cancellationToken = default);
    Task<PagedResult<ReattemptRequestDto>> GetReattemptRequestsAsync(ReattemptRequestQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<ReattemptRequestDto> ReviewReattemptRequestAsync(Guid id, ReviewReattemptRequestDto request, CancellationToken cancellationToken = default);
}
