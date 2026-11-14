using System.Net;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Quizzes.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Infrastructure.Services;

public class QuizService : IQuizService
{
    private readonly IPushpakClassesDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService? _emailService;
    private readonly IBackgroundJobClient? _backgroundJobClient;
    private readonly IRedisService? _redisService;

    public QuizService(
        IPushpakClassesDbContext context,
        ICurrentUserService currentUserService)
        : this(context, currentUserService, null, null, null)
    {
    }

    public QuizService(
        IPushpakClassesDbContext context,
        ICurrentUserService currentUserService,
        IEmailService? emailService,
        IBackgroundJobClient? backgroundJobClient,
        IRedisService? redisService = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _backgroundJobClient = backgroundJobClient;
        _redisService = redisService;
    }

    private async Task InvalidateQuizCacheAsync(Guid id, CancellationToken cancellationToken)
    {
        if (_redisService != null)
        {
            await _redisService.RemoveAsync($"quiz:{id}:meta", cancellationToken);
            await _redisService.RemoveAsync($"quiz:{id}:questions", cancellationToken);
        }
    }

    public async Task<PagedResult<QuizDto>> GetQuizzesAsync(QuizQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = _context.Quizzes
            .AsNoTracking()
            .Include(q => q.Category)
            .Include(q => q.Questions.Where(qu => !qu.IsDeleted))
            .Include(q => q.Attempts)
            .AsQueryable();

        // 1. Search
        if (!string.IsNullOrWhiteSpace(parameters.SearchQuery))
        {
            var search = parameters.SearchQuery.Trim().ToLower();
            query = query.Where(q => q.Title.ToLower().Contains(search) || (q.Description != null && q.Description.ToLower().Contains(search)));
        }

        // 2. Filter by Category
        if (parameters.CategoryId.HasValue)
        {
            query = query.Where(q => q.CategoryId == parameters.CategoryId.Value);
        }

        // 3. Filter by Publication Status
        if (parameters.IsPublished.HasValue)
        {
            query = query.Where(q => q.IsPublished == parameters.IsPublished.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // 4. Sorting
        var isAscending = string.Equals(parameters.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        query = parameters.SortBy?.ToLowerInvariant() switch
        {
            "title" => isAscending ? query.OrderBy(q => q.Title) : query.OrderByDescending(q => q.Title),
            "durationminutes" => isAscending ? query.OrderBy(q => q.DurationMinutes) : query.OrderByDescending(q => q.DurationMinutes),
            "startatutc" => isAscending ? query.OrderBy(q => q.StartAtUtc) : query.OrderByDescending(q => q.StartAtUtc),
            "endatutc" => isAscending ? query.OrderBy(q => q.EndAtUtc) : query.OrderByDescending(q => q.EndAtUtc),
            _ => isAscending ? query.OrderBy(q => q.Title) : query.OrderByDescending(q => q.Title)
        };

        // 5. Pagination
        var items = await query
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(q => new QuizDto
            {
                Id = q.Id,
                Title = q.Title,
                Description = q.Description,
                CategoryId = q.CategoryId,
                CategoryName = q.Category.Name,
                DurationMinutes = q.DurationMinutes,
                StartAtUtc = q.StartAtUtc,
                EndAtUtc = q.EndAtUtc,
                PassingPercent = q.PassingPercent,
                MaxAttempts = q.MaxAttempts,
                ShuffleQuestions = q.ShuffleQuestions,
                ShuffleOptions = q.ShuffleOptions,
                IsPublished = q.IsPublished,
                CreatedByUserId = q.CreatedByUserId,
                QuestionCount = q.Questions.Count(qu => !qu.IsDeleted),
                TotalMarks = q.Questions.Where(qu => !qu.IsDeleted).Sum(qu => qu.Marks),
                AttemptCount = q.Attempts.Count
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<QuizDto>(items, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<QuizDto> GetQuizByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quiz = await _context.Quizzes
            .AsNoTracking()
            .Include(q => q.Category)
            .Include(q => q.Questions.Where(qu => !qu.IsDeleted))
            .Include(q => q.Attempts)
            .Where(q => q.Id == id)
            .Select(q => new QuizDto
            {
                Id = q.Id,
                Title = q.Title,
                Description = q.Description,
                CategoryId = q.CategoryId,
                CategoryName = q.Category.Name,
                DurationMinutes = q.DurationMinutes,
                StartAtUtc = q.StartAtUtc,
                EndAtUtc = q.EndAtUtc,
                PassingPercent = q.PassingPercent,
                MaxAttempts = q.MaxAttempts,
                ShuffleQuestions = q.ShuffleQuestions,
                ShuffleOptions = q.ShuffleOptions,
                IsPublished = q.IsPublished,
                CreatedByUserId = q.CreatedByUserId,
                QuestionCount = q.Questions.Count(qu => !qu.IsDeleted),
                TotalMarks = q.Questions.Where(qu => !qu.IsDeleted).Sum(qu => qu.Marks),
                AttemptCount = q.Attempts.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (quiz == null)
        {
            throw new AuthException("Quiz not found.", HttpStatusCode.NotFound);
        }

        return quiz;
    }

    public async Task<QuizDto> CreateQuizAsync(CreateQuizRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.StartAtUtc >= request.EndAtUtc)
        {
            throw new AuthException("Quiz start time (StartAtUtc) must be earlier than end time (EndAtUtc).");
        }

        if (request.DurationMinutes <= 0)
        {
            throw new AuthException("Quiz duration must be greater than 0 minutes.");
        }

        var category = await _context.Categories.FindAsync(new object[] { request.CategoryId }, cancellationToken);
        if (category == null)
        {
            throw new AuthException("Selected category does not exist.", HttpStatusCode.NotFound);
        }

        var adminUserId = _currentUserService.UserId ?? Guid.Empty;

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            CategoryId = request.CategoryId,
            DurationMinutes = request.DurationMinutes,
            StartAtUtc = request.StartAtUtc.ToUniversalTime(),
            EndAtUtc = request.EndAtUtc.ToUniversalTime(),
            PassingPercent = request.PassingPercent,
            MaxAttempts = request.MaxAttempts,
            ShuffleQuestions = request.ShuffleQuestions,
            ShuffleOptions = request.ShuffleOptions,
            IsPublished = request.IsPublished,
            CreatedByUserId = adminUserId,
            IsDeleted = false
        };

        await _context.Quizzes.AddAsync(quiz, cancellationToken);

        await WriteAuditLogAsync("AdminCreateQuiz", quiz.Id, new { quiz.Title, quiz.CategoryId, quiz.DurationMinutes }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetQuizByIdAsync(quiz.Id, cancellationToken);
    }

    public async Task<QuizDto> UpdateQuizAsync(Guid id, UpdateQuizRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.StartAtUtc >= request.EndAtUtc)
        {
            throw new AuthException("Quiz start time (StartAtUtc) must be earlier than end time (EndAtUtc).");
        }

        if (request.DurationMinutes <= 0)
        {
            throw new AuthException("Quiz duration must be greater than 0 minutes.");
        }

        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (quiz == null)
        {
            throw new AuthException("Quiz not found.", HttpStatusCode.NotFound);
        }

        var category = await _context.Categories.FindAsync(new object[] { request.CategoryId }, cancellationToken);
        if (category == null)
        {
            throw new AuthException("Selected category does not exist.", HttpStatusCode.NotFound);
        }

        quiz.Title = request.Title.Trim();
        quiz.Description = request.Description?.Trim();
        quiz.CategoryId = request.CategoryId;
        quiz.DurationMinutes = request.DurationMinutes;
        quiz.StartAtUtc = request.StartAtUtc.ToUniversalTime();
        quiz.EndAtUtc = request.EndAtUtc.ToUniversalTime();
        quiz.PassingPercent = request.PassingPercent;
        quiz.MaxAttempts = request.MaxAttempts;
        quiz.ShuffleQuestions = request.ShuffleQuestions;
        quiz.ShuffleOptions = request.ShuffleOptions;

        await WriteAuditLogAsync("AdminUpdateQuiz", quiz.Id, new { quiz.Title, quiz.DurationMinutes, quiz.PassingPercent }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateQuizCacheAsync(quiz.Id, cancellationToken);

        return await GetQuizByIdAsync(quiz.Id, cancellationToken);
    }

    public async Task<AuthMessageResponseDto> DeleteQuizAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (quiz == null)
        {
            throw new AuthException("Quiz not found.", HttpStatusCode.NotFound);
        }

        // Soft delete (Rule 6 of AGENT_RULES.md)
        quiz.IsDeleted = true;

        await WriteAuditLogAsync("AdminDeleteQuiz", quiz.Id, new { quiz.Title, DeletedAtUtc = DateTime.UtcNow }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateQuizCacheAsync(quiz.Id, cancellationToken);

        return new AuthMessageResponseDto("Quiz deleted successfully.");
    }

    public async Task<QuizDto> SetPublishStatusAsync(Guid id, bool isPublished, CancellationToken cancellationToken = default)
    {
        var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (quiz == null)
        {
            throw new AuthException("Quiz not found.", HttpStatusCode.NotFound);
        }

        if (isPublished)
        {
            var activeQuestionCount = await _context.Questions.CountAsync(q => q.QuizId == id && !q.IsDeleted, cancellationToken);
            if (activeQuestionCount == 0)
            {
                throw new AuthException("Cannot publish a quiz that has 0 questions. Please add questions first.", HttpStatusCode.BadRequest);
            }

            if (quiz.StartAtUtc >= quiz.EndAtUtc)
            {
                throw new AuthException("Cannot publish a quiz where StartAtUtc is not earlier than EndAtUtc.", HttpStatusCode.BadRequest);
            }
        }

        quiz.IsPublished = isPublished;

        await WriteAuditLogAsync(
            action: isPublished ? "AdminPublishQuiz" : "AdminUnpublishQuiz",
            entityId: quiz.Id,
            details: new { quiz.Title, IsPublished = isPublished },
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await InvalidateQuizCacheAsync(quiz.Id, cancellationToken);

        // Notify active students when a quiz is published
        if (isPublished && _backgroundJobClient != null)
        {
            var category = await _context.Categories.FindAsync(new object[] { quiz.CategoryId }, cancellationToken);
            var categoryName = category?.Name ?? "General";

            var activeStudents = await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted && u.IsActive && u.Role == UserRole.User)
                .Select(u => new { u.Email, u.FullName })
                .ToListAsync(cancellationToken);

            foreach (var student in activeStudents)
            {
                _backgroundJobClient.Enqueue<IEmailService>(e => e.SendNewQuizPublishedEmailAsync(
                    student.Email,
                    student.FullName,
                    quiz.Title,
                    categoryName,
                    quiz.DurationMinutes,
                    quiz.PassingPercent,
                    quiz.StartAtUtc,
                    quiz.EndAtUtc,
                    quiz.Id,
                    CancellationToken.None));
            }
        }

        return await GetQuizByIdAsync(quiz.Id, cancellationToken);
    }

    public async Task<QuizDto> DuplicateQuizAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var original = await _context.Quizzes
            .AsNoTracking()
            .Include(q => q.Questions.Where(qu => !qu.IsDeleted))
                .ThenInclude(qu => qu.Options)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (original == null)
        {
            throw new AuthException("Original quiz not found.", HttpStatusCode.NotFound);
        }

        var adminUserId = _currentUserService.UserId ?? Guid.Empty;

        var duplicatedQuiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = $"{original.Title} (Copy)",
            Description = original.Description,
            CategoryId = original.CategoryId,
            DurationMinutes = original.DurationMinutes,
            StartAtUtc = original.StartAtUtc,
            EndAtUtc = original.EndAtUtc,
            PassingPercent = original.PassingPercent,
            MaxAttempts = original.MaxAttempts,
            ShuffleQuestions = original.ShuffleQuestions,
            ShuffleOptions = original.ShuffleOptions,
            IsPublished = false, // Duplicated quiz defaults to un集published
            CreatedByUserId = adminUserId,
            IsDeleted = false
        };

        // Deep copy questions and options with brand new IDs
        foreach (var originalQuestion in original.Questions)
        {
            var duplicatedQuestion = new Question
            {
                Id = Guid.NewGuid(),
                QuizId = duplicatedQuiz.Id,
                Text = originalQuestion.Text,
                Type = originalQuestion.Type,
                Marks = originalQuestion.Marks,
                Order = originalQuestion.Order,
                IsDeleted = false
            };

            foreach (var originalOption in originalQuestion.Options)
            {
                duplicatedQuestion.Options.Add(new Option
                {
                    Id = Guid.NewGuid(),
                    QuestionId = duplicatedQuestion.Id,
                    Text = originalOption.Text,
                    IsCorrect = originalOption.IsCorrect
                });
            }

            duplicatedQuiz.Questions.Add(duplicatedQuestion);
        }

        await _context.Quizzes.AddAsync(duplicatedQuiz, cancellationToken);

        await WriteAuditLogAsync("AdminDuplicateQuiz", duplicatedQuiz.Id, new { SourceQuizId = original.Id, NewQuizTitle = duplicatedQuiz.Title, ClonedQuestions = duplicatedQuiz.Questions.Count }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetQuizByIdAsync(duplicatedQuiz.Id, cancellationToken);
    }

    private async Task WriteAuditLogAsync(string action, Guid entityId, object details, CancellationToken cancellationToken)
    {
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUserService.UserId,
            Action = action,
            EntityName = nameof(Quiz),
            EntityId = entityId.ToString(),
            DetailsJson = JsonSerializer.Serialize(details),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _context.AuditLogs.AddAsync(audit, cancellationToken);
    }
}
