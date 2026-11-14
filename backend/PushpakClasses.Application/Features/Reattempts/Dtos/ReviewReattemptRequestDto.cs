using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Application.Features.Reattempts.Dtos;

public class ReviewReattemptRequestDto
{
    public ReattemptRequestStatus Status { get; set; }
    public string? ReviewNotes { get; set; }
}
