namespace PushpakClasses.Application.Common.Interfaces;

public interface IAutoSubmitJob
{
    Task ProcessExpiredAttemptsAsync(CancellationToken cancellationToken = default);
}
