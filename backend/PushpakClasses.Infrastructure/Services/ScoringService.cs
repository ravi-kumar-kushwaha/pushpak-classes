using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Features.Attempts.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Infrastructure.Services;

public class ScoringService : IScoringService
{
    private readonly IPushpakClassesDbContext _context;
    private readonly IRedisService _redisService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ScoringService> _logger;

    public ScoringService(
        IPushpakClassesDbContext context,
        IRedisService redisService,
        ILogger<ScoringService> logger)
        : this(context, redisService, new LoggingEmailService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LoggingEmailService>.Instance), logger)
    {
    }

    public ScoringService(
        IPushpakClassesDbContext context,
        IRedisService redisService,
        IEmailService emailService,
        ILogger<ScoringService> logger)
    {
        _context = context;
        _redisService = redisService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task ScoreAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting scoring for attempt '{AttemptId}'.", attemptId);

        var attempt = await _context.QuizAttempts
            .Include(a => a.Quiz)
            .Include(a => a.User)
            .Include(a => a.AttemptAnswers)
            .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken);

        if (attempt == null)
        {
            _logger.LogWarning("Attempt '{AttemptId}' not found during scoring execution.", attemptId);
            return;
        }

        // Fetch all active questions with their options for this quiz
        var questions = await _context.Questions
            .AsNoTracking()
            .Include(q => q.Options)
            .Where(q => q.QuizId == attempt.QuizId && !q.IsDeleted)
            .OrderBy(q => q.Order)
            .ToListAsync(cancellationToken);

        // Parse student's submitted answers
        var answersMap = new Dictionary<Guid, List<Guid>>();

        if (!string.IsNullOrWhiteSpace(attempt.AnswersJson))
        {
            try
            {
                var parsedDto = JsonSerializer.Deserialize<SaveAttemptAnswersRequestDto>(attempt.AnswersJson);
                if (parsedDto?.Answers != null && parsedDto.Answers.Count > 0)
                {
                    foreach (var ans in parsedDto.Answers)
                    {
                        answersMap[ans.QuestionId] = ans.SelectedOptionIds ?? new List<Guid>();
                    }
                }
            }
            catch
            {
                // Fall through to JSON document inspection
            }

            if (answersMap.Count == 0)
            {
                // In case AnswersJson has custom envelope or draft format
                try
                {
                    using var doc = JsonDocument.Parse(attempt.AnswersJson);
                    if (doc.RootElement.TryGetProperty("Draft", out var draftElement) ||
                        doc.RootElement.TryGetProperty("draft", out draftElement))
                    {
                        var draftList = JsonSerializer.Deserialize<List<QuestionAnswerDto>>(draftElement.GetRawText());
                        if (draftList != null)
                        {
                            foreach (var ans in draftList)
                            {
                                answersMap[ans.QuestionId] = ans.SelectedOptionIds ?? new List<Guid>();
                            }
                        }
                    }
                    else if (doc.RootElement.TryGetProperty("Answers", out var answersElement) ||
                             doc.RootElement.TryGetProperty("answers", out answersElement))
                    {
                        var ansList = JsonSerializer.Deserialize<List<QuestionAnswerDto>>(answersElement.GetRawText());
                        if (ansList != null)
                        {
                            foreach (var ans in ansList)
                            {
                                answersMap[ans.QuestionId] = ans.SelectedOptionIds ?? new List<Guid>();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse submitted answers for attempt '{AttemptId}'.", attemptId);
                }
            }
        }

        // Fallback to Redis if answersMap is still empty (answers not flushed yet)
        if (answersMap.Count == 0)
        {
            try
            {
                var redisDraft = await _redisService.GetAsync<SaveAttemptAnswersRequestDto>($"attempt:{attemptId}:answers", cancellationToken);
                if (redisDraft?.Answers != null && redisDraft.Answers.Count > 0)
                {
                    foreach (var ans in redisDraft.Answers)
                    {
                        answersMap[ans.QuestionId] = ans.SelectedOptionIds ?? new List<Guid>();
                    }
                    attempt.AnswersJson = JsonSerializer.Serialize(redisDraft);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve fallback Redis draft for attempt '{AttemptId}'.", attemptId);
            }
        }

        // Remove existing attempt answers if re-scoring
        if (attempt.AttemptAnswers.Any())
        {
            _context.AttemptAnswers.RemoveRange(attempt.AttemptAnswers);
            attempt.AttemptAnswers.Clear();
        }

        decimal totalScore = 0;
        decimal maxMarks = 0;

        foreach (var question in questions)
        {
            maxMarks += question.Marks;

            answersMap.TryGetValue(question.Id, out var selectedOptionIds);
            selectedOptionIds ??= new List<Guid>();

            var correctOptionIds = question.Options
                .Where(o => o.IsCorrect)
                .Select(o => o.Id)
                .ToHashSet();

            bool isCorrect = false;

            switch (question.Type)
            {
                case QuestionType.MultiChoice:
                    // Selected options must match the exact set of correct options
                    var selectedSet = selectedOptionIds.ToHashSet();
                    isCorrect = selectedSet.Count > 0 && selectedSet.SetEquals(correctOptionIds);
                    break;

                case QuestionType.TrueFalse:
                case QuestionType.SingleChoice:
                default:
                    // Exactly 1 option selected and it is correct (SingleChoice fallback for Type 0 / default)
                    isCorrect = selectedOptionIds.Count == 1 && correctOptionIds.Contains(selectedOptionIds[0]);
                    break;
            }

            var marksAwarded = isCorrect ? question.Marks : 0;
            totalScore += marksAwarded;

            var attemptAnswer = new AttemptAnswer
            {
                Id = Guid.NewGuid(),
                AttemptId = attempt.Id,
                QuestionId = question.Id,
                SelectedOptionIds = string.Join(",", selectedOptionIds),
                IsCorrect = isCorrect,
                MarksAwarded = marksAwarded
            };

            await _context.AttemptAnswers.AddAsync(attemptAnswer, cancellationToken);
        }

        attempt.Score = totalScore;
        attempt.TotalMarks = maxMarks;
        attempt.Percent = maxMarks > 0 ? Math.Round((totalScore / maxMarks) * 100m, 2) : 0;
        attempt.Passed = attempt.Percent >= attempt.Quiz.PassingPercent;

        // Clean up Redis draft key
        await _redisService.RemoveAsync($"attempt:{attemptId}:answers", cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Scoring completed for attempt '{AttemptId}': Score {Score}/{TotalMarks} ({Percent}%), Passed: {Passed}.",
            attemptId, attempt.Score, attempt.TotalMarks, attempt.Percent, attempt.Passed);

        if (attempt.User != null && !string.IsNullOrWhiteSpace(attempt.User.Email))
        {
            try
            {
                await _emailService.SendQuizResultEmailAsync(
                    toEmail: attempt.User.Email,
                    userName: attempt.User.FullName,
                    quizTitle: attempt.Quiz.Title,
                    score: attempt.Score,
                    totalMarks: attempt.TotalMarks,
                    percent: attempt.Percent,
                    passed: attempt.Passed,
                    attemptId: attempt.Id,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send quiz result email to '{Email}'.", attempt.User.Email);
            }
        }
    }
}
