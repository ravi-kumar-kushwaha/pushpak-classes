using PushpakClasses.Application.Common.Models;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Application.Features.Reattempts.Dtos;

public class ReattemptRequestQueryParameters : QuizQueryParameters
{
    public new ReattemptRequestStatus? Status { get; set; }
    public Guid? QuizId { get; set; }
    public Guid? UserId { get; set; }
}
