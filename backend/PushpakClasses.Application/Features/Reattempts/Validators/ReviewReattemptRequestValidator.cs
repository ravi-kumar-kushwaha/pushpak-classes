using FluentValidation;
using PushpakClasses.Application.Features.Reattempts.Dtos;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Application.Features.Reattempts.Validators;

public class ReviewReattemptRequestValidator : AbstractValidator<ReviewReattemptRequestDto>
{
    public ReviewReattemptRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s == ReattemptRequestStatus.Approved || s == ReattemptRequestStatus.Rejected)
            .WithMessage("Status must be either Approved or Rejected.");

        RuleFor(x => x.ReviewNotes)
            .MaximumLength(1000).WithMessage("Review notes cannot exceed 1000 characters.");
    }
}
