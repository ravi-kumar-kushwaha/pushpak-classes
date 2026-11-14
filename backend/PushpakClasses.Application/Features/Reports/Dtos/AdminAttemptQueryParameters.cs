using PushpakClasses.Application.Common.Models;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Application.Features.Reports.Dtos;

public class AdminAttemptQueryParameters : UserQueryParameters
{
    public Guid? QuizId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? FromDateUtc { get; set; }
    public DateTime? ToDateUtc { get; set; }
    public QuizAttemptStatus? Status { get; set; }
}
