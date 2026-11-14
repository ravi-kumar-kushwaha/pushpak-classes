namespace PushpakClasses.Application.Features.Quizzes.Dtos;

public class ReorderQuestionsRequestDto
{
    public List<QuestionOrderItemDto> Orders { get; set; } = new();
}

public class QuestionOrderItemDto
{
    public Guid QuestionId { get; set; }
    public int Order { get; set; }
}
