using System.Net;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Attempts.Dtos;
using PushpakClasses.Application.Features.Reattempts.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Infrastructure.Services;

public class QuizAttemptService : IQuizAttemptService
{
    private readonly IPushpakClassesDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRedisService _redisService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IScoringService? _scoringService;
    private readonly IEmailService? _emailService;

    public QuizAttemptService(
        IPushpakClassesDbContext context,
        ICurrentUserService currentUserService,
        IRedisService redisService,
        IBackgroundJobClient backgroundJobClient)
        : this(context, currentUserService, redisService, backgroundJobClient, null, null)
    {
    }

    public QuizAttemptService(
        IPushpakClassesDbContext context,
        ICurrentUserService currentUserService,
        IRedisService redisService,
        IBackgroundJobClient backgroundJobClient,
        IScoringService? scoringService)
        : this(context, currentUserService, redisService, backgroundJobClient, scoringService, null)
    {
    }

    public QuizAttemptService(
        IPushpakClassesDbContext context,
        ICurrentUserService currentUserService,
        IRedisService redisService,
        IBackgroundJobClient backgroundJobClient,
        IScoringService? scoringService,
        IEmailService? emailService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _redisService = redisService;
        _backgroundJobClient = backgroundJobClient;
        _scoringService = scoringService;
        _emailService = emailService;
    }

    public async Task<PagedResult<AvailableQuizDto>> GetAvailableQuizzesAsync(QuizQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? Guid.Empty;
        var now = DateTime.UtcNow;

        var cacheKey = $"user:{userId}:quizzes:available:{parameters.PageNumber}:{parameters.PageSize}:{parameters.CategoryId}:{parameters.SearchQuery}:{parameters.SortBy}:{parameters.SortDirection}:{parameters.Status}";
        var cached = await _redisService.GetAsync<PagedResult<AvailableQuizDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        // Base query: published, non-deleted
        var query = _context.Quizzes
            .AsNoTracking()
            .Include(q => q.Category)
            .Include(q => q.Questions.Where(qu => !qu.IsDeleted))
            .Include(q => q.Attempts.Where(a => a.UserId == userId))
            .Include(q => q.ReattemptRequests.Where(r => r.UserId == userId))
            .Where(q => q.IsPublished && !q.IsDeleted);

        // Filter by Category
        if (parameters.CategoryId.HasValue)
        {
            query = query.Where(q => q.CategoryId == parameters.CategoryId.Value);
        }

        // Search query
        if (!string.IsNullOrWhiteSpace(parameters.SearchQuery))
        {
            var search = parameters.SearchQuery.Trim().ToLower();
            query = query.Where(q => q.Title.ToLower().Contains(search) || (q.Description != null && q.Description.ToLower().Contains(search)));
        }

        var quizEntities = await query.ToListAsync(cancellationToken);

        var allDtos = quizEntities.Select(q =>
        {
            var userAttempts = q.Attempts.Where(a => a.UserId == userId).ToList();
            var userReattempts = q.ReattemptRequests.Where(r => r.UserId == userId).ToList();
            var approvedExtraAttempts = userReattempts.Count(r => r.Status == ReattemptRequestStatus.Approved);
            var allowedAttempts = q.MaxAttempts + approvedExtraAttempts;
            var attemptsUsed = userAttempts.Count;
            var attemptsRemaining = Math.Max(0, allowedAttempts - attemptsUsed);

            var latestReattempt = userReattempts.OrderByDescending(r => r.CreatedAtUtc).FirstOrDefault();
            var activeAttempt = userAttempts.FirstOrDefault(a => a.Status == QuizAttemptStatus.InProgress && a.EndsAtUtc.AddSeconds(10) >= now);

            string status;
            if (activeAttempt != null)
            {
                status = "Resume";
            }
            else if (now < q.StartAtUtc)
            {
                status = "Upcoming";
            }
            else if (now > q.EndAtUtc)
            {
                status = attemptsUsed > 0 ? "Attempted" : "Expired";
            }
            else
            {
                status = attemptsUsed >= allowedAttempts ? "Attempted" : "Open";
            }

            return new AvailableQuizDto
            {
                Id = q.Id,
                Title = q.Title,
                Description = q.Description,
                CategoryId = q.CategoryId,
                CategoryName = q.Category?.Name ?? string.Empty,
                DurationMinutes = q.DurationMinutes,
                StartAtUtc = q.StartAtUtc,
                EndAtUtc = q.EndAtUtc,
                PassingPercent = q.PassingPercent,
                MaxAttempts = q.MaxAttempts,
                AllowedAttempts = allowedAttempts,
                AttemptsUsed = attemptsUsed,
                AttemptsRemaining = attemptsRemaining,
                Status = status,
                ReattemptStatus = latestReattempt?.Status.ToString(),
                ReattemptRequestId = latestReattempt?.Id,
                HasActiveAttempt = activeAttempt != null,
                ActiveAttemptId = activeAttempt?.Id,
                QuestionCount = q.Questions.Count(qu => !qu.IsDeleted),
                TotalMarks = q.Questions.Where(qu => !qu.IsDeleted).Sum(qu => qu.Marks)
            };
        }).ToList();

        // Status filter (Open, Attempted, Expired, Upcoming, Resume)
        if (!string.IsNullOrWhiteSpace(parameters.Status) && !string.Equals(parameters.Status, "All", StringComparison.OrdinalIgnoreCase))
        {
            allDtos = allDtos.Where(d => string.Equals(d.Status, parameters.Status, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Sorting
        var isAscending = string.Equals(parameters.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        allDtos = (parameters.SortBy?.ToLowerInvariant() switch
        {
            "title" => isAscending ? allDtos.OrderBy(q => q.Title) : allDtos.OrderByDescending(q => q.Title),
            "durationminutes" => isAscending ? allDtos.OrderBy(q => q.DurationMinutes) : allDtos.OrderByDescending(q => q.DurationMinutes),
            "endatutc" => isAscending ? allDtos.OrderBy(q => q.EndAtUtc) : allDtos.OrderByDescending(q => q.EndAtUtc),
            _ => isAscending ? allDtos.OrderBy(q => q.EndAtUtc) : allDtos.OrderByDescending(q => q.EndAtUtc)
        }).ToList();

        var totalCount = allDtos.Count;
        var items = allDtos
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToList();

        var result = new PagedResult<AvailableQuizDto>(items, totalCount, parameters.PageNumber, parameters.PageSize);
        await _redisService.SetAsync(cacheKey, result, TimeSpan.FromSeconds(10), cancellationToken);
        return result;
    }

    public async Task<StartQuizAttemptResponseDto> StartQuizAttemptAsync(Guid quizId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? Guid.Empty;
        if (userId == Guid.Empty)
        {
            throw new AuthException("User must be authenticated.", HttpStatusCode.Unauthorized);
        }

        var now = DateTime.UtcNow;

        // 1. Fast Redis cache for Quiz metadata (eliminates 10k identical DB reads during spike)
        var quizCacheKey = $"quiz:{quizId}:meta";
        var quiz = await _redisService.GetAsync<CachedQuizMetaDto>(quizCacheKey, cancellationToken);
        if (quiz == null)
        {
            var dbQuiz = await _context.Quizzes
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == quizId && !q.IsDeleted, cancellationToken);

            if (dbQuiz == null)
            {
                throw new AuthException("Quiz not found.", HttpStatusCode.NotFound);
            }

            quiz = new CachedQuizMetaDto
            {
                Id = dbQuiz.Id,
                Title = dbQuiz.Title,
                IsPublished = dbQuiz.IsPublished,
                StartAtUtc = dbQuiz.StartAtUtc,
                EndAtUtc = dbQuiz.EndAtUtc,
                DurationMinutes = dbQuiz.DurationMinutes,
                MaxAttempts = dbQuiz.MaxAttempts,
                ShuffleQuestions = dbQuiz.ShuffleQuestions,
                ShuffleOptions = dbQuiz.ShuffleOptions
            };

            await _redisService.SetAsync(quizCacheKey, quiz, TimeSpan.FromMinutes(10), cancellationToken);
        }

        if (!quiz.IsPublished)
        {
            throw new AuthException("This quiz is not currently published.", HttpStatusCode.BadRequest);
        }

        // Schedule check
        if (now < quiz.StartAtUtc)
        {
            throw new AuthException($"Quiz has not started yet. It will open at {quiz.StartAtUtc:u}.", HttpStatusCode.BadRequest);
        }

        if (now > quiz.EndAtUtc)
        {
            throw new AuthException($"Quiz has ended and expired. The scheduled deadline passed at {quiz.EndAtUtc:u}.", HttpStatusCode.BadRequest);
        }

        // 2. Fast Redis check for active in-progress attempt
        var activeAttemptKey = $"user:{userId}:active_attempt";
        var activeAttemptId = await _redisService.GetAsync<Guid?>(activeAttemptKey, cancellationToken);
        if (activeAttemptId.HasValue && activeAttemptId.Value != Guid.Empty)
        {
            throw new AuthException("You already have an active quiz attempt in progress. Please finish it before starting a new one.", HttpStatusCode.BadRequest);
        }

        // User must have NO other active InProgress attempt in DB if cache was cold
        var existingInProgressAttempt = await _context.QuizAttempts
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Status == QuizAttemptStatus.InProgress, cancellationToken);

        if (existingInProgressAttempt != null)
        {
            if (existingInProgressAttempt.EndsAtUtc.AddSeconds(10) >= now)
            {
                await _redisService.SetAsync(activeAttemptKey, existingInProgressAttempt.Id, existingInProgressAttempt.EndsAtUtc.Subtract(now), cancellationToken);
                throw new AuthException("You already have an active quiz attempt in progress. Please finish it before starting a new one.", HttpStatusCode.BadRequest);
            }
            else
            {
                // Expired in-progress attempt: finalize as AutoSubmitted
                existingInProgressAttempt.Status = QuizAttemptStatus.AutoSubmitted;
                existingInProgressAttempt.SubmittedAtUtc = existingInProgressAttempt.EndsAtUtc;
                _backgroundJobClient.Enqueue<IScoringService>(s => s.ScoreAttemptAsync(existingInProgressAttempt.Id, CancellationToken.None));
            }
        }

        // MaxAttempts check for this quiz with approved reattempts
        var userAttemptCount = await _context.QuizAttempts
            .CountAsync(a => a.QuizId == quizId && a.UserId == userId, cancellationToken);

        var approvedReattempts = await _context.ReattemptRequests
            .CountAsync(r => r.QuizId == quizId && r.UserId == userId && r.Status == ReattemptRequestStatus.Approved, cancellationToken);

        var allowedAttempts = quiz.MaxAttempts + approvedReattempts;

        if (userAttemptCount >= allowedAttempts)
        {
            throw new AuthException($"You have reached the maximum allowed attempts ({allowedAttempts}) for this quiz.", HttpStatusCode.BadRequest);
        }

        // Server-authoritative timer: EndsAtUtc = now + DurationMinutes (capped by quiz EndAtUtc)
        var calculatedEnd = now.AddMinutes(quiz.DurationMinutes);
        var endsAtUtc = calculatedEnd < quiz.EndAtUtc ? calculatedEnd : quiz.EndAtUtc;

        // Fetch questions (Cached in Redis for 10 minutes)
        var questionsCacheKey = $"quiz:{quizId}:questions";
        var cachedQuestions = await _redisService.GetAsync<List<CachedQuestionDto>>(questionsCacheKey, cancellationToken);

        if (cachedQuestions == null || cachedQuestions.Count == 0)
        {
            var dbQuestions = await _context.Questions
                .AsNoTracking()
                .Include(q => q.Options)
                .Where(q => q.QuizId == quizId && !q.IsDeleted)
                .OrderBy(q => q.Order)
                .ToListAsync(cancellationToken);

            if (dbQuestions.Count == 0)
            {
                throw new AuthException("This quiz currently has no active questions.", HttpStatusCode.BadRequest);
            }

            cachedQuestions = dbQuestions.Select(q => new CachedQuestionDto
            {
                Id = q.Id,
                Text = q.Text,
                TextEn = q.Text,
                TextHi = q.TextHi,
                Type = (q.Type == 0 ? QuestionType.SingleChoice : q.Type).ToString(),
                Marks = q.Marks,
                Order = q.Order,
                Options = q.Options.Select(o => new CachedOptionDto
                {
                    Id = o.Id,
                    Text = o.Text,
                    TextEn = o.Text,
                    TextHi = o.TextHi,
                    IsCorrect = o.IsCorrect
                }).ToList()
            }).ToList();

            // Cache in Redis for 10 minutes
            await _redisService.SetAsync(questionsCacheKey, cachedQuestions, TimeSpan.FromMinutes(10), cancellationToken);
        }

        // Prepare randomized / shuffled questions for this attempt
        var attemptQuestions = cachedQuestions.Select(q => new CachedQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            TextEn = q.TextEn ?? q.Text,
            TextHi = q.TextHi,
            Type = q.Type,
            Marks = q.Marks,
            Order = q.Order,
            Options = q.Options.Select(o => new CachedOptionDto
            {
                Id = o.Id,
                Text = o.Text,
                TextEn = o.TextEn ?? o.Text,
                TextHi = o.TextHi,
                IsCorrect = o.IsCorrect
            }).ToList()
        }).ToList();

        if (quiz.ShuffleQuestions)
        {
            attemptQuestions = attemptQuestions.OrderBy(_ => Random.Shared.Next()).ToList();
        }

        if (quiz.ShuffleOptions)
        {
            foreach (var q in attemptQuestions)
            {
                // Note: Keep True/False options standard True then False
                if (!string.Equals(q.Type, "TrueFalse", StringComparison.OrdinalIgnoreCase))
                {
                    q.Options = q.Options.OrderBy(_ => Random.Shared.Next()).ToList();
                }
            }
        }

        // Assign attempt display order
        for (int i = 0; i < attemptQuestions.Count; i++)
        {
            attemptQuestions[i].Order = i + 1;
        }

        // Build question order tracking metadata
        var shuffleMetadata = attemptQuestions.Select(q => new
        {
            QuestionId = q.Id,
            Order = q.Order,
            OptionOrder = q.Options.Select(o => o.Id).ToList()
        }).ToList();

        var attempt = new QuizAttempt
        {
            Id = Guid.NewGuid(),
            QuizId = quiz.Id,
            UserId = userId,
            StartedAtUtc = now,
            EndsAtUtc = endsAtUtc,
            Status = QuizAttemptStatus.InProgress,
            Score = 0,
            TotalMarks = attemptQuestions.Sum(q => q.Marks),
            Percent = 0,
            Passed = false,
            AnswersJson = JsonSerializer.Serialize(new { ShuffleMap = shuffleMetadata, Draft = new List<QuestionAnswerDto>() }),
            TabSwitchCount = 0
        };

        await _context.QuizAttempts.AddAsync(attempt, cancellationToken);

        // Audit log
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "UserStartQuizAttempt",
            EntityName = nameof(QuizAttempt),
            EntityId = attempt.Id.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { quiz.Id, quiz.Title, attempt.StartedAtUtc, attempt.EndsAtUtc }),
            CreatedAtUtc = now
        };
        await _context.AuditLogs.AddAsync(audit, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        // Cache active attempt ID in Redis with TTL so subsequent checks are instant sub-millisecond in-memory
        var ttl = endsAtUtc > now ? endsAtUtc - now : TimeSpan.FromMinutes(quiz.DurationMinutes);
        await _redisService.SetAsync(activeAttemptKey, (Guid?)attempt.Id, ttl.Add(TimeSpan.FromSeconds(10)), cancellationToken);

        // Sanitize options: zero entity leak, strip IsCorrect
        var sanitizedQuestions = attemptQuestions.Select(q => new AttemptQuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            TextEn = q.TextEn ?? q.Text,
            TextHi = q.TextHi,
            Type = q.Type,
            Marks = q.Marks,
            Order = q.Order,
            Options = q.Options.Select(o => new AttemptOptionDto
            {
                Id = o.Id,
                Text = o.Text,
                TextEn = o.TextEn ?? o.Text,
                TextHi = o.TextHi
            }).ToList()
        }).ToList();

        return new StartQuizAttemptResponseDto
        {
            AttemptId = attempt.Id,
            QuizId = quiz.Id,
            QuizTitle = quiz.Title,
            DurationMinutes = quiz.DurationMinutes,
            StartedAtUtc = DateTime.SpecifyKind(attempt.StartedAtUtc, DateTimeKind.Utc),
            EndsAtUtc = DateTime.SpecifyKind(attempt.EndsAtUtc, DateTimeKind.Utc),
            ServerTimeUtc = DateTime.SpecifyKind(now, DateTimeKind.Utc),
            Questions = sanitizedQuestions
        };
    }

    public async Task<AuthMessageResponseDto> SaveDraftAnswersAsync(Guid attemptId, SaveAttemptAnswersRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? Guid.Empty;
        var now = DateTime.UtcNow;

        var attempt = await _context.QuizAttempts
            .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken);

        if (attempt == null)
        {
            throw new AuthException("Quiz attempt not found.", HttpStatusCode.NotFound);
        }

        if (attempt.UserId != userId)
        {
            throw new AuthException("You are not authorized to access this quiz attempt.", HttpStatusCode.Forbidden);
        }

        if (attempt.Status != QuizAttemptStatus.InProgress)
        {
            throw new AuthException("Quiz attempt has already been submitted or completed.", HttpStatusCode.BadRequest);
        }

        // Server-authoritative timer check: reject if now > EndsAtUtc + 5 seconds
        if (now > attempt.EndsAtUtc.AddSeconds(5))
        {
            throw new AuthException("Quiz attempt time has expired. Draft cannot be saved.", HttpStatusCode.BadRequest);
        }

        // Store draft answers in Redis for 24 hours
        var redisKey = $"attempt:{attemptId}:answers";
        await _redisService.SetAsync(redisKey, request, TimeSpan.FromHours(24), cancellationToken);

        return new AuthMessageResponseDto("Draft answers saved successfully.");
    }

    public async Task<SubmitAttemptResponseDto> SubmitQuizAttemptAsync(Guid attemptId, SaveAttemptAnswersRequestDto? directAnswers = null, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? Guid.Empty;
        var now = DateTime.UtcNow;

        var attempt = await _context.QuizAttempts
            .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken);

        if (attempt == null)
        {
            throw new AuthException("Quiz attempt not found.", HttpStatusCode.NotFound);
        }

        if (attempt.UserId != userId)
        {
            throw new AuthException("You are not authorized to submit this quiz attempt.", HttpStatusCode.Forbidden);
        }

        // Idempotency check: if already submitted or auto-submitted, return success receipt without duplicate scoring
        if (attempt.Status == QuizAttemptStatus.Submitted || attempt.Status == QuizAttemptStatus.AutoSubmitted)
        {
            return new SubmitAttemptResponseDto
            {
                AttemptId = attempt.Id,
                Status = attempt.Status.ToString(),
                SubmittedAtUtc = attempt.SubmittedAtUtc,
                Message = "Quiz attempt has already been submitted."
            };
        }

        // Flush answers from direct body or Redis
        var redisKey = $"attempt:{attemptId}:answers";
        SaveAttemptAnswersRequestDto? answersToSave = null;

        if (directAnswers?.Answers != null && directAnswers.Answers.Count > 0)
        {
            answersToSave = directAnswers;
        }
        else
        {
            answersToSave = await _redisService.GetAsync<SaveAttemptAnswersRequestDto>(redisKey, cancellationToken);
        }

        if (now > attempt.EndsAtUtc.AddSeconds(15))
        {
            attempt.Status = QuizAttemptStatus.AutoSubmitted;
            attempt.SubmittedAtUtc = attempt.EndsAtUtc;
        }
        else
        {
            attempt.Status = QuizAttemptStatus.Submitted;
            attempt.SubmittedAtUtc = now;
        }

        // Evict active attempt key from Redis so user is instantly free
        await _redisService.RemoveAsync($"user:{attempt.UserId}:active_attempt", cancellationToken);

        if (answersToSave != null && answersToSave.Answers != null && answersToSave.Answers.Count > 0)
        {
            attempt.AnswersJson = JsonSerializer.Serialize(answersToSave);
        }

        // Audit log
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "UserSubmitQuizAttempt",
            EntityName = nameof(QuizAttempt),
            EntityId = attempt.Id.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { attempt.Id, attempt.QuizId, attempt.SubmittedAtUtc }),
            CreatedAtUtc = now
        };
        await _context.AuditLogs.AddAsync(audit, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        // Immediate scoring for instant results & consistency with My Attempts
        if (_scoringService != null)
        {
            try
            {
                await _scoringService.ScoreAttemptAsync(attempt.Id, cancellationToken);
            }
            catch
            {
                // Fallback to background queue if instant scoring hits transient issue
                _backgroundJobClient.Enqueue<IScoringService>(s => s.ScoreAttemptAsync(attempt.Id, CancellationToken.None));
            }
        }
        else
        {
            _backgroundJobClient.Enqueue<IScoringService>(s => s.ScoreAttemptAsync(attempt.Id, CancellationToken.None));
        }

        return new SubmitAttemptResponseDto
        {
            AttemptId = attempt.Id,
            Status = attempt.Status.ToString(),
            SubmittedAtUtc = attempt.SubmittedAtUtc,
            Message = "Quiz submitted successfully."
        };
    }

    public async Task<TabSwitchResponseDto> RecordTabSwitchAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? Guid.Empty;

        var attempt = await _context.QuizAttempts
            .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken);

        if (attempt == null)
        {
            throw new AuthException("Quiz attempt not found.", HttpStatusCode.NotFound);
        }

        if (attempt.UserId != userId)
        {
            throw new AuthException("You are not authorized to modify this quiz attempt.", HttpStatusCode.Forbidden);
        }

        if (attempt.Status != QuizAttemptStatus.InProgress)
        {
            throw new AuthException("Cannot record tab switches on an inactive or submitted attempt.", HttpStatusCode.BadRequest);
        }

        attempt.TabSwitchCount++;
        await _context.SaveChangesAsync(cancellationToken);

        return new TabSwitchResponseDto
        {
            AttemptId = attempt.Id,
            TabSwitchCount = attempt.TabSwitchCount,
            Message = "Tab switch recorded."
        };
    }

    public async Task<AttemptResultDto> GetAttemptResultAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? Guid.Empty;

        var attempt = await _context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Quiz)
            .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken);

        if (attempt == null)
        {
            throw new AuthException("Quiz attempt not found.", HttpStatusCode.NotFound);
        }

        if (attempt.UserId != userId && _currentUserService.Role != "Admin")
        {
            throw new AuthException("You are not authorized to view this result.", HttpStatusCode.Forbidden);
        }

        return new AttemptResultDto
        {
            AttemptId = attempt.Id,
            QuizId = attempt.QuizId,
            QuizTitle = attempt.Quiz.Title,
            Status = attempt.Status.ToString(),
            Score = attempt.Score,
            TotalMarks = attempt.TotalMarks,
            Percent = attempt.Percent,
            Passed = attempt.Passed,
            TabSwitchCount = attempt.TabSwitchCount,
            StartedAtUtc = attempt.StartedAtUtc,
            SubmittedAtUtc = attempt.SubmittedAtUtc
        };
    }

    public async Task<ActiveAttemptDetailsDto> GetActiveAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? Guid.Empty;
        var now = DateTime.UtcNow;

        var attempt = await _context.QuizAttempts
            .Include(a => a.Quiz)
            .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken);

        if (attempt == null)
        {
            throw new AuthException("Quiz attempt not found.", HttpStatusCode.NotFound);
        }

        if (attempt.UserId != userId)
        {
            throw new AuthException("You are not authorized to access this quiz attempt.", HttpStatusCode.Forbidden);
        }

        // Auto-expire check: if expired beyond 10s grace, mark auto-submitted
        if (attempt.Status == QuizAttemptStatus.InProgress && attempt.EndsAtUtc.AddSeconds(10) < now)
        {
            attempt.Status = QuizAttemptStatus.AutoSubmitted;
            attempt.SubmittedAtUtc = attempt.EndsAtUtc;
            await _context.SaveChangesAsync(cancellationToken);
            _backgroundJobClient.Enqueue<IScoringService>(s => s.ScoreAttemptAsync(attempt.Id, CancellationToken.None));
        }

        // Load sanitized questions for the attempt
        var questionsCacheKey = $"quiz:{attempt.QuizId}:questions";
        var cachedQuestions = await _redisService.GetAsync<List<CachedQuestionDto>>(questionsCacheKey, cancellationToken);

        if (cachedQuestions == null || cachedQuestions.Count == 0)
        {
            var dbQuestions = await _context.Questions
                .AsNoTracking()
                .Include(q => q.Options)
                .Where(q => q.QuizId == attempt.QuizId && !q.IsDeleted)
                .OrderBy(q => q.Order)
                .ToListAsync(cancellationToken);

            cachedQuestions = dbQuestions.Select(q => new CachedQuestionDto
            {
                Id = q.Id,
                Text = q.Text,
                TextEn = q.Text,
                TextHi = q.TextHi,
                Type = (q.Type == 0 ? QuestionType.SingleChoice : q.Type).ToString(),
                Marks = q.Marks,
                Order = q.Order,
                Options = q.Options.Select(o => new CachedOptionDto
                {
                    Id = o.Id,
                    Text = o.Text,
                    TextEn = o.Text,
                    TextHi = o.TextHi,
                    IsCorrect = o.IsCorrect
                }).ToList()
            }).ToList();
        }

        // Map according to original shuffle map if present in AnswersJson
        var questionsList = new List<AttemptQuestionDto>();
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(attempt.AnswersJson) ? "{}" : attempt.AnswersJson);
            if (doc.RootElement.TryGetProperty("ShuffleMap", out var shuffleMapElement) && shuffleMapElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in shuffleMapElement.EnumerateArray())
                {
                    var qId = item.GetProperty("QuestionId").GetGuid();
                    var order = item.GetProperty("Order").GetInt32();
                    var cachedQ = cachedQuestions.FirstOrDefault(q => q.Id == qId);
                    if (cachedQ != null)
                    {
                        var optOrder = item.GetProperty("OptionOrder").EnumerateArray().Select(o => o.GetGuid()).ToList();
                        var sortedOptions = cachedQ.Options
                            .Where(o => optOrder.Contains(o.Id))
                            .OrderBy(o => optOrder.IndexOf(o.Id))
                            .Select(o => new AttemptOptionDto { Id = o.Id, Text = o.Text, TextEn = o.TextEn ?? o.Text, TextHi = o.TextHi })
                            .ToList();

                        if (sortedOptions.Count == 0)
                        {
                            sortedOptions = cachedQ.Options.Select(o => new AttemptOptionDto { Id = o.Id, Text = o.Text, TextEn = o.TextEn ?? o.Text, TextHi = o.TextHi }).ToList();
                        }

                        questionsList.Add(new AttemptQuestionDto
                        {
                            Id = cachedQ.Id,
                            Text = cachedQ.Text,
                            TextEn = cachedQ.TextEn ?? cachedQ.Text,
                            TextHi = cachedQ.TextHi,
                            Type = cachedQ.Type,
                            Marks = cachedQ.Marks,
                            Order = order,
                            Options = sortedOptions
                        });
                    }
                }
            }
        }
        catch
        {
            // Fallback if shuffle map parsing fails
        }

        if (questionsList.Count == 0)
        {
            questionsList = cachedQuestions.Select((q, idx) => new AttemptQuestionDto
            {
                Id = q.Id,
                Text = q.Text,
                TextEn = q.TextEn ?? q.Text,
                TextHi = q.TextHi,
                Type = q.Type,
                Marks = q.Marks,
                Order = idx + 1,
                Options = q.Options.Select(o => new AttemptOptionDto { Id = o.Id, Text = o.Text, TextEn = o.TextEn ?? o.Text, TextHi = o.TextHi }).ToList()
            }).ToList();
        }

        // Fetch draft answers from Redis
        var draftKey = $"attempt:{attempt.Id}:answers";
        var draftDto = await _redisService.GetAsync<SaveAttemptAnswersRequestDto>(draftKey, cancellationToken);
        var draftAnswers = draftDto?.Answers ?? new List<QuestionAnswerDto>();

        return new ActiveAttemptDetailsDto
        {
            AttemptId = attempt.Id,
            QuizId = attempt.QuizId,
            QuizTitle = attempt.Quiz.Title,
            DurationMinutes = attempt.Quiz.DurationMinutes,
            StartedAtUtc = DateTime.SpecifyKind(attempt.StartedAtUtc, DateTimeKind.Utc),
            EndsAtUtc = DateTime.SpecifyKind(attempt.EndsAtUtc, DateTimeKind.Utc),
            ServerTimeUtc = DateTime.SpecifyKind(now, DateTimeKind.Utc),
            Status = attempt.Status.ToString(),
            TabSwitchCount = attempt.TabSwitchCount,
            Questions = questionsList,
            DraftAnswers = draftAnswers
        };
    }

    public async Task<ReattemptRequestDto> CreateReattemptRequestAsync(Guid quizId, CreateReattemptRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? Guid.Empty;
        if (userId == Guid.Empty)
        {
            throw new AuthException("User must be authenticated.", HttpStatusCode.Unauthorized);
        }

        var quiz = await _context.Quizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quizId && !q.IsDeleted, cancellationToken);

        if (quiz == null)
        {
            throw new AuthException("Quiz not found.", HttpStatusCode.NotFound);
        }

        var attemptsUsed = await _context.QuizAttempts
            .CountAsync(a => a.QuizId == quizId && a.UserId == userId, cancellationToken);

        var approvedReattempts = await _context.ReattemptRequests
            .CountAsync(r => r.QuizId == quizId && r.UserId == userId && r.Status == ReattemptRequestStatus.Approved, cancellationToken);

        var allowedAttempts = quiz.MaxAttempts + approvedReattempts;

        if (attemptsUsed < allowedAttempts)
        {
            throw new AuthException("You still have remaining attempts for this quiz and do not need to request a reattempt.", HttpStatusCode.BadRequest);
        }

        var hasPending = await _context.ReattemptRequests
            .AnyAsync(r => r.QuizId == quizId && r.UserId == userId && r.Status == ReattemptRequestStatus.Pending, cancellationToken);

        if (hasPending)
        {
            throw new AuthException("You already have a pending reattempt request for this quiz. Please wait for an administrator to review it.", HttpStatusCode.BadRequest);
        }

        var reattempt = new ReattemptRequest
        {
            Id = Guid.NewGuid(),
            QuizId = quizId,
            UserId = userId,
            Reason = request.Reason.Trim(),
            Status = ReattemptRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _context.ReattemptRequests.AddAsync(reattempt, cancellationToken);

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "UserRequestReattempt",
            EntityName = nameof(ReattemptRequest),
            EntityId = reattempt.Id.ToString(),
            DetailsJson = JsonSerializer.Serialize(new { QuizId = quizId, QuizTitle = quiz.Title, request.Reason }),
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return new ReattemptRequestDto
        {
            Id = reattempt.Id,
            QuizId = reattempt.QuizId,
            QuizTitle = quiz.Title,
            UserId = reattempt.UserId,
            UserFullName = user?.FullName ?? string.Empty,
            UserEmail = user?.Email ?? string.Empty,
            Reason = reattempt.Reason,
            Status = reattempt.Status.ToString(),
            CreatedAtUtc = reattempt.CreatedAtUtc,
            ReviewedAtUtc = reattempt.ReviewedAtUtc,
            ReviewNotes = reattempt.ReviewNotes
        };
    }

    public async Task<PagedResult<ReattemptRequestDto>> GetReattemptRequestsAsync(ReattemptRequestQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = _context.ReattemptRequests
            .AsNoTracking()
            .Include(r => r.Quiz)
            .Include(r => r.User)
            .Include(r => r.ReviewedByUser)
            .AsQueryable();

        if (parameters.Status.HasValue)
        {
            query = query.Where(r => r.Status == parameters.Status.Value);
        }

        if (parameters.QuizId.HasValue)
        {
            query = query.Where(r => r.QuizId == parameters.QuizId.Value);
        }

        if (parameters.UserId.HasValue)
        {
            query = query.Where(r => r.UserId == parameters.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.SearchQuery))
        {
            var search = parameters.SearchQuery.Trim().ToLower();
            query = query.Where(r =>
                r.Quiz.Title.ToLower().Contains(search) ||
                r.User.FullName.ToLower().Contains(search) ||
                r.User.Email.ToLower().Contains(search) ||
                r.Reason.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var isAscending = string.Equals(parameters.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        query = parameters.SortBy?.ToLowerInvariant() switch
        {
            "quiztitle" => isAscending ? query.OrderBy(r => r.Quiz.Title) : query.OrderByDescending(r => r.Quiz.Title),
            "userfullname" => isAscending ? query.OrderBy(r => r.User.FullName) : query.OrderByDescending(r => r.User.FullName),
            "status" => isAscending ? query.OrderBy(r => r.Status) : query.OrderByDescending(r => r.Status),
            "createdatutc" => isAscending ? query.OrderBy(r => r.CreatedAtUtc) : query.OrderByDescending(r => r.CreatedAtUtc),
            _ => isAscending ? query.OrderBy(r => r.CreatedAtUtc) : query.OrderByDescending(r => r.CreatedAtUtc)
        };

        var items = await query
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(r => new ReattemptRequestDto
            {
                Id = r.Id,
                QuizId = r.QuizId,
                QuizTitle = r.Quiz.Title,
                UserId = r.UserId,
                UserFullName = r.User.FullName,
                UserEmail = r.User.Email,
                Reason = r.Reason,
                Status = r.Status.ToString(),
                ReviewedByUserId = r.ReviewedByUserId,
                ReviewedByUserName = r.ReviewedByUser != null ? r.ReviewedByUser.FullName : null,
                CreatedAtUtc = r.CreatedAtUtc,
                ReviewedAtUtc = r.ReviewedAtUtc,
                ReviewNotes = r.ReviewNotes
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ReattemptRequestDto>(items, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<ReattemptRequestDto> ReviewReattemptRequestAsync(Guid id, ReviewReattemptRequestDto request, CancellationToken cancellationToken = default)
    {
        var adminId = _currentUserService.UserId ?? Guid.Empty;

        var reattempt = await _context.ReattemptRequests
            .Include(r => r.Quiz)
            .Include(r => r.User)
            .Include(r => r.ReviewedByUser)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (reattempt == null)
        {
            throw new AuthException("Reattempt request not found.", HttpStatusCode.NotFound);
        }

        if (reattempt.Status != ReattemptRequestStatus.Pending)
        {
            throw new AuthException($"This reattempt request has already been reviewed ({reattempt.Status}).", HttpStatusCode.BadRequest);
        }

        if (request.Status != ReattemptRequestStatus.Approved && request.Status != ReattemptRequestStatus.Rejected)
        {
            throw new AuthException("Review status must be either Approved or Rejected.", HttpStatusCode.BadRequest);
        }

        reattempt.Status = request.Status;
        reattempt.ReviewNotes = request.ReviewNotes?.Trim();
        reattempt.ReviewedByUserId = adminId != Guid.Empty ? adminId : null;
        reattempt.ReviewedAtUtc = DateTime.UtcNow;

        await _context.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = adminId,
            Action = request.Status == ReattemptRequestStatus.Approved ? "AdminApproveReattemptRequest" : "AdminRejectReattemptRequest",
            EntityName = nameof(ReattemptRequest),
            EntityId = reattempt.Id.ToString(),
            DetailsJson = JsonSerializer.Serialize(new
            {
                reattempt.UserId,
                UserEmail = reattempt.User.Email,
                reattempt.QuizId,
                QuizTitle = reattempt.Quiz.Title,
                Status = reattempt.Status.ToString(),
                reattempt.ReviewNotes
            }),
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        if (_emailService != null && !string.IsNullOrWhiteSpace(reattempt.User.Email))
        {
            try
            {
                await _emailService.SendReattemptDecisionEmailAsync(
                    reattempt.User.Email,
                    reattempt.User.FullName,
                    reattempt.Quiz.Title,
                    request.Status == ReattemptRequestStatus.Approved,
                    request.ReviewNotes,
                    cancellationToken);
            }
            catch
            {
                // Logging email failure should not fail transaction
            }
        }

        var reviewerName = adminId != Guid.Empty
            ? await _context.Users.Where(u => u.Id == adminId).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new ReattemptRequestDto
        {
            Id = reattempt.Id,
            QuizId = reattempt.QuizId,
            QuizTitle = reattempt.Quiz.Title,
            UserId = reattempt.UserId,
            UserFullName = reattempt.User.FullName,
            UserEmail = reattempt.User.Email,
            Reason = reattempt.Reason,
            Status = reattempt.Status.ToString(),
            ReviewedByUserId = reattempt.ReviewedByUserId,
            ReviewedByUserName = reviewerName,
            CreatedAtUtc = reattempt.CreatedAtUtc,
            ReviewedAtUtc = reattempt.ReviewedAtUtc,
            ReviewNotes = reattempt.ReviewNotes
        };
    }
}

public class CachedQuestionDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public string? TextHi { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Marks { get; set; }
    public int Order { get; set; }
    public List<CachedOptionDto> Options { get; set; } = new();
}

public class CachedOptionDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public string? TextHi { get; set; }
    public bool IsCorrect { get; set; }
}
