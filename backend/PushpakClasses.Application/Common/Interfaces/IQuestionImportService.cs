using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Quizzes.Dtos;

namespace PushpakClasses.Application.Common.Interfaces;

public interface IQuestionImportService
{
    Task<(List<CreateQuestionRequestDto> Questions, List<string> Errors)> ParseFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}
