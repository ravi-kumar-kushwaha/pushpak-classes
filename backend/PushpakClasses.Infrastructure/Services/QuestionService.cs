using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Quizzes.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Infrastructure.Services;

public class QuestionService : IQuestionService
{
    private readonly IPushpakClassesDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IQuestionImportService _importService;
    private readonly IRedisService? _redisService;

    public QuestionService(
        IPushpakClassesDbContext context,
        ICurrentUserService currentUserService,
        IQuestionImportService importService,
        IRedisService? redisService = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _importService = importService;
        _redisService = redisService;
    }

    private async Task InvalidateQuestionsCacheAsync(Guid quizId, CancellationToken cancellationToken)
    {
        if (_redisService != null)
        {
            await _redisService.RemoveAsync($"quiz:{quizId}:questions", cancellationToken);
        }
    }

    public async Task<List<QuestionDto>> GetQuestionsByQuizIdAsync(Guid quizId, CancellationToken cancellationToken = default)
    {
        var quizExists = await _context.Quizzes.AnyAsync(q => q.Id == quizId, cancellationToken);
        if (!quizExists)
        {
            throw new AuthException("Quiz not found.", HttpStatusCode.NotFound);
        }

        return await _context.Questions
            .AsNoTracking()
            .Include(q => q.Options)
            .Where(q => q.QuizId == quizId && !q.IsDeleted)
            .OrderBy(q => q.Order)
            .Select(q => MapToQuestionDto(q))
            .ToListAsync(cancellationToken);
    }

    public async Task<QuestionDto> GetQuestionByIdAsync(Guid quizId, Guid questionId, CancellationToken cancellationToken = default)
    {
        var question = await _context.Questions
            .AsNoTracking()
            .Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.QuizId == quizId && q.Id == questionId && !q.IsDeleted, cancellationToken);

        if (question == null)
        {
            throw new AuthException("Question not found.", HttpStatusCode.NotFound);
        }

        return MapToQuestionDto(question);
    }

    public async Task<QuestionDto> CreateQuestionAsync(Guid quizId, CreateQuestionRequestDto request, CancellationToken cancellationToken = default)
    {
        var effectiveType = request.Type == 0 ? QuestionType.SingleChoice : request.Type;
        await AssertQuizQuestionsCanBeModifiedAsync(quizId, cancellationToken);
        ValidateQuestionOptions(effectiveType, request.Options);

        var maxOrder = await _context.Questions
            .Where(q => q.QuizId == quizId && !q.IsDeleted)
            .Select(q => (int?)q.Order)
            .MaxAsync(cancellationToken) ?? 0;

        var qText = (!string.IsNullOrWhiteSpace(request.Text) ? request.Text : request.TextEn ?? string.Empty).Trim();
        var question = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = quizId,
            Text = qText,
            TextHi = request.TextHi?.Trim(),
            Type = effectiveType,
            Marks = request.Marks,
            Order = request.Order > 0 ? request.Order : maxOrder + 1,
            IsDeleted = false
        };

        foreach (var opt in request.Options)
        {
            var optText = (!string.IsNullOrWhiteSpace(opt.Text) ? opt.Text : opt.TextEn ?? string.Empty).Trim();
            question.Options.Add(new Option
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                Text = optText,
                TextHi = opt.TextHi?.Trim(),
                IsCorrect = opt.IsCorrect
            });
        }

        await _context.Questions.AddAsync(question, cancellationToken);

        await WriteAuditLogAsync("AdminCreateQuestion", question.Id, new { quizId, question.Text, question.Type, question.Marks }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateQuestionsCacheAsync(quizId, cancellationToken);

        return MapToQuestionDto(question);
    }

    public async Task<QuestionDto> UpdateQuestionAsync(Guid quizId, Guid questionId, UpdateQuestionRequestDto request, CancellationToken cancellationToken = default)
    {
        var effectiveType = request.Type == 0 ? QuestionType.SingleChoice : request.Type;
        ValidateQuestionOptions(effectiveType, request.Options);

        var quiz = await _context.Quizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quizId, cancellationToken);

        if (quiz == null)
        {
            throw new AuthException("Quiz not found.", HttpStatusCode.NotFound);
        }

        var question = await _context.Questions
            .Include(q => q.Options)
            .FirstOrDefaultAsync(q => q.QuizId == quizId && q.Id == questionId && !q.IsDeleted, cancellationToken);

        if (question == null)
        {
            throw new AuthException("Question not found.", HttpStatusCode.NotFound);
        }

        var hasAttempts = await _context.QuizAttempts
            .AnyAsync(a => a.QuizId == quizId, cancellationToken);

        // Check if quiz is published and already has student attempts
        if (quiz.IsPublished && hasAttempts)
        {
            // Structural changes are prohibited: Type, Marks, Option count, Option identities, or Correct answers
            var existingOptionIds = question.Options.Select(o => o.Id).ToHashSet();
            var incomingOptionIds = request.Options.Where(o => o.Id.HasValue).Select(o => o.Id!.Value).ToHashSet();

            bool isStructuralChange =
                question.Type != effectiveType ||
                question.Marks != request.Marks ||
                question.Options.Count != request.Options.Count ||
                !existingOptionIds.SetEquals(incomingOptionIds);

            if (!isStructuralChange)
            {
                // Verify that no option has changed its IsCorrect answer status
                foreach (var opt in request.Options)
                {
                    var existing = question.Options.FirstOrDefault(o => o.Id == opt.Id);
                    if (existing == null || existing.IsCorrect != opt.IsCorrect)
                    {
                        isStructuralChange = true;
                        break;
                    }
                }
            }

            if (isStructuralChange)
            {
                throw new AuthException("Quiz already has attempts, unpublish or duplicate it", HttpStatusCode.Conflict);
            }

            // Allowed: Text typo fixes only (supports English Text/TextEn and Hindi TextHi)
            question.Text = (!string.IsNullOrWhiteSpace(request.Text) ? request.Text : request.TextEn ?? string.Empty).Trim();
            if (request.TextHi != null)
            {
                question.TextHi = request.TextHi.Trim();
            }
            foreach (var opt in request.Options)
            {
                var existing = question.Options.FirstOrDefault(o => o.Id == opt.Id);
                if (existing != null)
                {
                    existing.Text = (!string.IsNullOrWhiteSpace(opt.Text) ? opt.Text : opt.TextEn ?? string.Empty).Trim();
                    if (opt.TextHi != null)
                    {
                        existing.TextHi = opt.TextHi.Trim();
                    }
                }
            }

            await WriteAuditLogAsync("AdminUpdateQuestionTypo", question.Id, new { quizId, question.Text }, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await InvalidateQuestionsCacheAsync(quizId, cancellationToken);
            return MapToQuestionDto(question);
        }

        // Quiz has no attempts (or is draft): full update allowed with in-place option tracking
        question.Text = (!string.IsNullOrWhiteSpace(request.Text) ? request.Text : request.TextEn ?? string.Empty).Trim();
        question.TextHi = request.TextHi?.Trim();
        question.Type = effectiveType;
        question.Marks = request.Marks;
        question.Order = request.Order;

        // In-place options update (do not wipe and recreate everything)
        var incomingIds = request.Options.Where(o => o.Id.HasValue).Select(o => o.Id!.Value).ToHashSet();

        // 1. Remove deleted options
        var toRemove = question.Options.Where(o => !incomingIds.Contains(o.Id)).ToList();
        foreach (var rem in toRemove)
        {
            _context.Options.Remove(rem);
        }

        // 2. Update existing options & add new options
        foreach (var opt in request.Options)
        {
            var optText = (!string.IsNullOrWhiteSpace(opt.Text) ? opt.Text : opt.TextEn ?? string.Empty).Trim();
            if (opt.Id.HasValue)
            {
                var existing = question.Options.FirstOrDefault(o => o.Id == opt.Id.Value);
                if (existing != null)
                {
                    existing.Text = optText;
                    existing.TextHi = opt.TextHi?.Trim();
                    existing.IsCorrect = opt.IsCorrect;
                    continue;
                }
            }

            // New option
            var newOpt = new Option
            {
                Id = opt.Id.HasValue && opt.Id.Value != Guid.Empty ? opt.Id.Value : Guid.NewGuid(),
                QuestionId = question.Id,
                Text = optText,
                TextHi = opt.TextHi?.Trim(),
                IsCorrect = opt.IsCorrect
            };
            _context.Options.Add(newOpt);
        }

        await WriteAuditLogAsync("AdminUpdateQuestion", question.Id, new { quizId, question.Text, question.Marks }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateQuestionsCacheAsync(quizId, cancellationToken);

        return MapToQuestionDto(question);
    }

    public async Task<AuthMessageResponseDto> DeleteQuestionAsync(Guid quizId, Guid questionId, CancellationToken cancellationToken = default)
    {
        await AssertQuizQuestionsCanBeModifiedAsync(quizId, cancellationToken);

        var question = await _context.Questions
            .FirstOrDefaultAsync(q => q.QuizId == quizId && q.Id == questionId && !q.IsDeleted, cancellationToken);

        if (question == null)
        {
            throw new AuthException("Question not found.", HttpStatusCode.NotFound);
        }

        // Soft delete (Rule 6 of AGENT_RULES.md)
        question.IsDeleted = true;

        await WriteAuditLogAsync("AdminDeleteQuestion", question.Id, new { quizId, question.Text, DeletedAtUtc = DateTime.UtcNow }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateQuestionsCacheAsync(quizId, cancellationToken);

        return new AuthMessageResponseDto("Question deleted successfully.");
    }

    public async Task<AuthMessageResponseDto> ReorderQuestionsAsync(Guid quizId, ReorderQuestionsRequestDto request, CancellationToken cancellationToken = default)
    {
        await AssertQuizQuestionsCanBeModifiedAsync(quizId, cancellationToken);

        var questions = await _context.Questions
            .Where(q => q.QuizId == quizId && !q.IsDeleted)
            .ToListAsync(cancellationToken);

        var orderMap = request.Orders.ToDictionary(o => o.QuestionId, o => o.Order);

        foreach (var question in questions)
        {
            if (orderMap.TryGetValue(question.Id, out var newOrder))
            {
                question.Order = newOrder;
            }
        }

        await WriteAuditLogAsync("AdminReorderQuestions", quizId, new { ReorderedCount = orderMap.Count }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateQuestionsCacheAsync(quizId, cancellationToken);

        return new AuthMessageResponseDto("Questions reordered successfully.");
    }

    public async Task<BulkImportResultDto> BulkImportQuestionsAsync(
        Guid quizId,
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await AssertQuizQuestionsCanBeModifiedAsync(quizId, cancellationToken);

        var (parsedQuestions, errors) = await _importService.ParseFileAsync(fileStream, fileName, cancellationToken);

        if (parsedQuestions.Count == 0)
        {
            return new BulkImportResultDto
            {
                TotalImported = 0,
                SkippedCount = 0,
                Errors = errors
            };
        }

        var currentMaxOrder = await _context.Questions
            .Where(q => q.QuizId == quizId && !q.IsDeleted)
            .Select(q => (int?)q.Order)
            .MaxAsync(cancellationToken) ?? 0;

        int importedCount = 0;
        foreach (var qDto in parsedQuestions)
        {
            currentMaxOrder++;
            var question = new Question
            {
                Id = Guid.NewGuid(),
                QuizId = quizId,
                Text = qDto.Text,
                Type = qDto.Type,
                Marks = qDto.Marks,
                Order = currentMaxOrder,
                IsDeleted = false
            };

            foreach (var opt in qDto.Options)
            {
                question.Options.Add(new Option
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
                    Text = opt.Text,
                    IsCorrect = opt.IsCorrect
                });
            }

            await _context.Questions.AddAsync(question, cancellationToken);
            importedCount++;
        }

        await WriteAuditLogAsync("AdminBulkImportQuestions", quizId, new { ImportedQuestionsCount = importedCount }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateQuestionsCacheAsync(quizId, cancellationToken);

        return new BulkImportResultDto
        {
            TotalImported = importedCount,
            SkippedCount = errors.Count,
            Errors = errors
        };
    }

    /// <summary>
    /// Critical Integrity Guard: A published quiz that already has attempts cannot have its questions edited.
    /// </summary>
    private async Task AssertQuizQuestionsCanBeModifiedAsync(Guid quizId, CancellationToken cancellationToken)
    {
        var quiz = await _context.Quizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quizId, cancellationToken);

        if (quiz == null)
        {
            throw new AuthException("Quiz not found.", HttpStatusCode.NotFound);
        }

        var hasAttempts = await _context.QuizAttempts
            .AnyAsync(a => a.QuizId == quizId, cancellationToken);

        if (quiz.IsPublished && hasAttempts)
        {
            throw new AuthException("Cannot modify questions for a published quiz that already has student attempts.", HttpStatusCode.BadRequest);
        }
    }

    private static void ValidateQuestionOptions(QuestionType type, List<CreateOptionDto> options)
    {
        if (options == null || options.Count < 2)
        {
            throw new AuthException("A question must contain at least 2 options.");
        }

        var effectiveType = type == 0 ? QuestionType.SingleChoice : type;
        var correctCount = options.Count(o => o.IsCorrect);

        switch (effectiveType)
        {
            case QuestionType.SingleChoice:
                if (correctCount != 1)
                {
                    throw new AuthException("SingleChoice question must have exactly 1 correct option.");
                }
                break;

            case QuestionType.MultiChoice:
                if (correctCount < 1)
                {
                    throw new AuthException("MultiChoice question must have at least 1 correct option.");
                }
                break;

            case QuestionType.TrueFalse:
                if (options.Count != 2)
                {
                    throw new AuthException("True/False question must contain exactly 2 options ('True' and 'False').");
                }
                if (correctCount != 1)
                {
                    throw new AuthException("True/False question must have exactly 1 correct option.");
                }
                break;
        }
    }

    private static QuestionDto MapToQuestionDto(Question question)
    {
        return new QuestionDto
        {
            Id = question.Id,
            QuizId = question.QuizId,
            Text = question.Text,
            TextEn = question.Text,
            TextHi = question.TextHi,
            Type = (question.Type == 0 ? QuestionType.SingleChoice : question.Type).ToString(),
            Marks = question.Marks,
            Order = question.Order,
            Options = question.Options.Select(o => new OptionDto
            {
                Id = o.Id,
                QuestionId = o.QuestionId,
                Text = o.Text,
                TextEn = o.Text,
                TextHi = o.TextHi,
                IsCorrect = o.IsCorrect
            }).ToList()
        };
    }

    private async Task WriteAuditLogAsync(string action, Guid entityId, object details, CancellationToken cancellationToken)
    {
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUserService.UserId,
            Action = action,
            EntityName = nameof(Question),
            EntityId = entityId.ToString(),
            DetailsJson = JsonSerializer.Serialize(details),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _context.AuditLogs.AddAsync(audit, cancellationToken);
    }
}
