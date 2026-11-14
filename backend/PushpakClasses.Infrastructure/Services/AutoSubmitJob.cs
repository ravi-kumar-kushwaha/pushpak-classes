using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Features.Attempts.Dtos;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Infrastructure.Services;

public class AutoSubmitJob : IAutoSubmitJob
{
    private readonly IPushpakClassesDbContext _context;
    private readonly IRedisService _redisService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<AutoSubmitJob> _logger;

    public AutoSubmitJob(
        IPushpakClassesDbContext context,
        IRedisService redisService,
        IBackgroundJobClient backgroundJobClient,
        ILogger<AutoSubmitJob> logger)
    {
        _context = context;
        _redisService = redisService;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task ProcessExpiredAttemptsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        _logger.LogInformation("Checking for expired in-progress quiz attempts at {Now} UTC.", now);

        // Find all attempts where Status == InProgress and EndsAtUtc + 10s < now
        var expiredAttempts = await _context.QuizAttempts
            .Where(a => a.Status == QuizAttemptStatus.InProgress && a.EndsAtUtc.AddSeconds(10) < now)
            .ToListAsync(cancellationToken);

        if (expiredAttempts.Count == 0)
        {
            _logger.LogInformation("No expired quiz attempts found.");
            return;
        }

        _logger.LogInformation("Found {Count} expired quiz attempt(s) to auto-submit.", expiredAttempts.Count);

        foreach (var attempt in expiredAttempts)
        {
            try
            {
                // Pull draft answers from Redis if present
                var redisKey = $"attempt:{attempt.Id}:answers";
                var draftAnswers = await _redisService.GetAsync<SaveAttemptAnswersRequestDto>(redisKey, cancellationToken);

                attempt.Status = QuizAttemptStatus.AutoSubmitted;
                attempt.SubmittedAtUtc = attempt.EndsAtUtc;

                if (draftAnswers != null && draftAnswers.Answers != null && draftAnswers.Answers.Count > 0)
                {
                    attempt.AnswersJson = JsonSerializer.Serialize(draftAnswers);
                }

                // Enqueue background scoring job
                _backgroundJobClient.Enqueue<IScoringService>(s => s.ScoreAttemptAsync(attempt.Id, CancellationToken.None));

                _logger.LogInformation("Auto-submitted attempt '{AttemptId}'. Enqueued scoring job.", attempt.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-submit attempt '{AttemptId}'.", attempt.Id);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
