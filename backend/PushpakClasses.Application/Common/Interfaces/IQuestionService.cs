using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Quizzes.Dtos;

namespace PushpakClasses.Application.Common.Interfaces;

public interface IQuestionService
{
    Task<List<QuestionDto>> GetQuestionsByQuizIdAsync(Guid quizId, CancellationToken cancellationToken = default);
    Task<QuestionDto> GetQuestionByIdAsync(Guid quizId, Guid questionId, CancellationToken cancellationToken = default);
    Task<QuestionDto> CreateQuestionAsync(Guid quizId, CreateQuestionRequestDto request, CancellationToken cancellationToken = default);
    Task<QuestionDto> UpdateQuestionAsync(Guid quizId, Guid questionId, UpdateQuestionRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthMessageResponseDto> DeleteQuestionAsync(Guid quizId, Guid questionId, CancellationToken cancellationToken = default);
    Task<AuthMessageResponseDto> ReorderQuestionsAsync(Guid quizId, ReorderQuestionsRequestDto request, CancellationToken cancellationToken = default);
    Task<BulkImportResultDto> BulkImportQuestionsAsync(Guid quizId, Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}
