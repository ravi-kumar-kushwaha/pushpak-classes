namespace PushpakClasses.Application.Features.Quizzes.Dtos;

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int QuizCount { get; set; }
}
