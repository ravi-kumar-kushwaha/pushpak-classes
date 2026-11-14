using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Quizzes.Dtos;

namespace PushpakClasses.Application.Common.Interfaces;

public interface IQuizService
{
    Task<PagedResult<QuizDto>> GetQuizzesAsync(QuizQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<QuizDto> GetQuizByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QuizDto> CreateQuizAsync(CreateQuizRequestDto request, CancellationToken cancellationToken = default);
    Task<QuizDto> UpdateQuizAsync(Guid id, UpdateQuizRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthMessageResponseDto> DeleteQuizAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QuizDto> SetPublishStatusAsync(Guid id, bool isPublished, CancellationToken cancellationToken = default);
    Task<QuizDto> DuplicateQuizAsync(Guid id, CancellationToken cancellationToken = default);
}
