namespace PushpakClasses.Application.Common.Interfaces;

public interface IScoringService
{
    Task ScoreAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default);
}
