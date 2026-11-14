namespace PushpakClasses.Application.Features.Attempts.Dtos;

public class SaveAttemptAnswersRequestDto
{
    public List<QuestionAnswerDto> Answers { get; set; } = new();
}
