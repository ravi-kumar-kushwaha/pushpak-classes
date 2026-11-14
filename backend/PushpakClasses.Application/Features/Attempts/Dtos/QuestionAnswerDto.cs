namespace PushpakClasses.Application.Features.Attempts.Dtos;

public class QuestionAnswerDto
{
    public Guid QuestionId { get; set; }
    public List<Guid> SelectedOptionIds { get; set; } = new();
}
