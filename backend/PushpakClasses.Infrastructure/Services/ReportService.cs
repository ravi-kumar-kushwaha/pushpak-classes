using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Attempts.Dtos;
using PushpakClasses.Application.Features.Reports.Dtos;
using PushpakClasses.Domain.Enums;

namespace PushpakClasses.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly IPushpakClassesDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IScoringService? _scoringService;

    public ReportService(
        IPushpakClassesDbContext context,
        ICurrentUserService currentUserService)
        : this(context, currentUserService, null)
    {
    }

    public ReportService(
        IPushpakClassesDbContext context,
        ICurrentUserService currentUserService,
        IScoringService? scoringService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _scoringService = scoringService;
    }

    public async Task<PagedResult<UserAttemptHistoryDto>> GetUserAttemptsAsync(QuizQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? Guid.Empty;

        var query = _context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Quiz)
                .ThenInclude(q => q.Category)
            .Where(a => a.UserId == userId && (a.Status == QuizAttemptStatus.Submitted || a.Status == QuizAttemptStatus.AutoSubmitted || a.Status == QuizAttemptStatus.InProgress));

        if (!string.IsNullOrWhiteSpace(parameters.SearchQuery))
        {
            var search = parameters.SearchQuery.Trim().ToLower();
            query = query.Where(a => a.Quiz.Title.ToLower().Contains(search) || (a.Quiz.Category.Name.ToLower().Contains(search)));
        }

        if (parameters.CategoryId.HasValue)
        {
            query = query.Where(a => a.Quiz.CategoryId == parameters.CategoryId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var isAscending = string.Equals(parameters.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        query = parameters.SortBy?.ToLowerInvariant() switch
        {
            "quiztitle" => isAscending ? query.OrderBy(a => a.Quiz.Title) : query.OrderByDescending(a => a.Quiz.Title),
            "score" => isAscending ? query.OrderBy(a => a.Score) : query.OrderByDescending(a => a.Score),
            "percent" => isAscending ? query.OrderBy(a => a.Percent) : query.OrderByDescending(a => a.Percent),
            "startedatutc" => isAscending ? query.OrderBy(a => a.StartedAtUtc) : query.OrderByDescending(a => a.StartedAtUtc),
            _ => isAscending ? query.OrderBy(a => a.StartedAtUtc) : query.OrderByDescending(a => a.StartedAtUtc)
        };

        var items = await query
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(a => new UserAttemptHistoryDto
            {
                Id = a.Id,
                QuizId = a.QuizId,
                QuizTitle = a.Quiz.Title,
                CategoryName = a.Quiz.Category.Name,
                StartedAtUtc = a.StartedAtUtc,
                SubmittedAtUtc = a.SubmittedAtUtc,
                Status = a.Status.ToString(),
                Score = a.Score,
                TotalMarks = a.TotalMarks,
                Percent = a.Percent,
                Passed = a.Passed,
                TabSwitchCount = a.TabSwitchCount
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<UserAttemptHistoryDto>(items, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<AttemptReviewDto> GetAttemptReviewAsync(Guid attemptId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? Guid.Empty;

        var attempt = await _context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Quiz)
            .Include(a => a.AttemptAnswers)
            .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken);

        if (attempt == null)
        {
            throw new AuthException("Quiz attempt not found.", HttpStatusCode.NotFound);
        }

        var candidateUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == attempt.UserId, cancellationToken);

        if (attempt.UserId != userId && _currentUserService.Role != "Admin")
        {
            throw new AuthException("You are not authorized to review this quiz attempt.", HttpStatusCode.Forbidden);
        }

        // Security Guard: Quiz review is strictly prohibited while attempt is InProgress
        if (attempt.Status == QuizAttemptStatus.InProgress)
        {
            throw new AuthException("Quiz attempt review is only accessible after submission.", HttpStatusCode.BadRequest);
        }

        // On-demand scoring if attempt is submitted but answers not yet graded, or if score is 0 with un-awarded answers
        var shouldRescore = (attempt.AttemptAnswers.Count == 0 || (attempt.Score == 0 && attempt.AttemptAnswers.All(a => !a.IsCorrect)));
        if (shouldRescore && _scoringService != null)
        {
            try
            {
                await _scoringService.ScoreAttemptAsync(attempt.Id, cancellationToken);
                attempt = await _context.QuizAttempts
                    .AsNoTracking()
                    .Include(a => a.Quiz)
                    .Include(a => a.AttemptAnswers)
                    .FirstOrDefaultAsync(a => a.Id == attemptId, cancellationToken) ?? attempt;
            }
            catch
            {
                // Proceed with fallback if scoring service encounters transient error
            }
        }

        // Load quiz questions and options
        var questions = await _context.Questions
            .AsNoTracking()
            .Include(q => q.Options)
            .Where(q => q.QuizId == attempt.QuizId && !q.IsDeleted)
            .OrderBy(q => q.Order)
            .ToListAsync(cancellationToken);

        // Map attempt answers
        var attemptAnswersMap = attempt.AttemptAnswers.ToDictionary(a => a.QuestionId);

        // Fallback to answersJson if attempt answers not yet written
        var jsonAnswersMap = new Dictionary<Guid, List<Guid>>();
        if (attemptAnswersMap.Count == 0 && !string.IsNullOrWhiteSpace(attempt.AnswersJson))
        {
            try
            {
                var parsedDto = JsonSerializer.Deserialize<SaveAttemptAnswersRequestDto>(attempt.AnswersJson);
                if (parsedDto?.Answers != null && parsedDto.Answers.Count > 0)
                {
                    foreach (var ans in parsedDto.Answers)
                    {
                        jsonAnswersMap[ans.QuestionId] = ans.SelectedOptionIds ?? new List<Guid>();
                    }
                }
            }
            catch { }

            if (jsonAnswersMap.Count == 0)
            {
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
                                jsonAnswersMap[ans.QuestionId] = ans.SelectedOptionIds ?? new List<Guid>();
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
                                jsonAnswersMap[ans.QuestionId] = ans.SelectedOptionIds ?? new List<Guid>();
                            }
                        }
                    }
                }
                catch { }
            }
        }

        var questionReviews = new List<QuestionReviewDto>();

        foreach (var q in questions)
        {
            var selectedOptionIds = new HashSet<Guid>();
            decimal marksAwarded = 0;
            bool isCorrect = false;

            if (attemptAnswersMap.TryGetValue(q.Id, out var answerRecord))
            {
                marksAwarded = answerRecord.MarksAwarded;
                isCorrect = answerRecord.IsCorrect;

                if (!string.IsNullOrWhiteSpace(answerRecord.SelectedOptionIds))
                {
                    var ids = answerRecord.SelectedOptionIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var idStr in ids)
                    {
                        if (Guid.TryParse(idStr.Trim(), out var parsedGuid))
                        {
                            selectedOptionIds.Add(parsedGuid);
                        }
                    }
                }
            }
            else if (jsonAnswersMap.TryGetValue(q.Id, out var jsonIds))
            {
                selectedOptionIds = jsonIds.ToHashSet();
            }

            var optionReviews = q.Options.Select(opt => new OptionReviewDto
            {
                OptionId = opt.Id,
                Text = opt.Text,
                TextEn = opt.Text,
                TextHi = opt.TextHi,
                IsSelectedByStudent = selectedOptionIds.Contains(opt.Id),
                IsCorrect = opt.IsCorrect
            }).ToList();

            questionReviews.Add(new QuestionReviewDto
            {
                QuestionId = q.Id,
                Text = q.Text,
                TextEn = q.Text,
                TextHi = q.TextHi,
                Type = (q.Type == 0 ? QuestionType.SingleChoice : q.Type).ToString(),
                Marks = q.Marks,
                MarksAwarded = marksAwarded,
                IsCorrect = isCorrect,
                Options = optionReviews
            });
        }

        var isScoringPending = attempt.Status != QuizAttemptStatus.InProgress && attempt.AttemptAnswers.Count == 0;

        return new AttemptReviewDto
        {
            AttemptId = attempt.Id,
            QuizId = attempt.QuizId,
            QuizTitle = attempt.Quiz.Title,
            UserId = attempt.UserId,
            UserFullName = candidateUser?.FullName ?? string.Empty,
            UserEmail = candidateUser?.Email ?? string.Empty,
            Status = attempt.Status.ToString(),
            Score = attempt.Score,
            TotalMarks = attempt.TotalMarks,
            Percent = attempt.Percent,
            Passed = attempt.Passed,
            StartedAtUtc = attempt.StartedAtUtc,
            SubmittedAtUtc = attempt.SubmittedAtUtc,
            TabSwitchCount = attempt.TabSwitchCount,
            IsScoringPending = isScoringPending,
            Questions = questionReviews
        };
    }

    public async Task<UserDashboardDto> GetUserDashboardAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId ?? Guid.Empty;

        var attempts = await _context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Quiz)
                .ThenInclude(q => q.Category)
            .Where(a => a.UserId == userId && a.Status != QuizAttemptStatus.InProgress)
            .OrderByDescending(a => a.StartedAtUtc)
            .ToListAsync(cancellationToken);

        var totalAttempts = attempts.Count;
        var avgScore = totalAttempts > 0 ? Math.Round(attempts.Average(a => a.Percent), 2) : 0;
        var bestScore = totalAttempts > 0 ? attempts.Max(a => a.Percent) : 0;
        var passRate = totalAttempts > 0 ? Math.Round((decimal)attempts.Count(a => a.Passed) / totalAttempts * 100m, 2) : 0;
        var uniqueQuizzes = attempts.Select(a => a.QuizId).Distinct().Count();

        var recent = attempts.Take(5).Select(a => new UserAttemptHistoryDto
        {
            Id = a.Id,
            QuizId = a.QuizId,
            QuizTitle = a.Quiz.Title,
            CategoryName = a.Quiz.Category.Name,
            StartedAtUtc = a.StartedAtUtc,
            SubmittedAtUtc = a.SubmittedAtUtc,
            Status = a.Status.ToString(),
            Score = a.Score,
            TotalMarks = a.TotalMarks,
            Percent = a.Percent,
            Passed = a.Passed,
            TabSwitchCount = a.TabSwitchCount
        }).ToList();

        return new UserDashboardDto
        {
            TotalAttempts = totalAttempts,
            AverageScore = avgScore,
            BestScore = bestScore,
            PassRate = passRate,
            TotalQuizzesAttempted = uniqueQuizzes,
            RecentAttempts = recent
        };
    }

    public async Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _context.Users
            .CountAsync(u => !u.IsDeleted && u.Role == UserRole.User, cancellationToken);

        var totalQuizzes = await _context.Quizzes
            .CountAsync(q => !q.IsDeleted, cancellationToken);

        var completedAttemptsQuery = _context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.Status != QuizAttemptStatus.InProgress);

        var totalAttempts = await completedAttemptsQuery.CountAsync(cancellationToken);

        var averageScore = totalAttempts > 0
            ? Math.Round(await completedAttemptsQuery.AverageAsync(a => a.Percent, cancellationToken), 2)
            : 0;

        // 30-Day trend
        var thirtyDaysAgo = DateTime.UtcNow.Date.AddDays(-29);
        var recentAttempts = await completedAttemptsQuery
            .Where(a => a.StartedAtUtc >= thirtyDaysAgo)
            .Select(a => new { Date = a.StartedAtUtc.Date, a.Percent })
            .ToListAsync(cancellationToken);

        var trendList = new List<DailyAttemptStatDto>();
        for (int i = 0; i < 30; i++)
        {
            var date = thirtyDaysAgo.AddDays(i);
            var dayAttempts = recentAttempts.Where(a => a.Date == date).ToList();
            trendList.Add(new DailyAttemptStatDto
            {
                DateUtc = date,
                AttemptCount = dayAttempts.Count,
                AverageScore = dayAttempts.Count > 0 ? Math.Round(dayAttempts.Average(a => a.Percent), 2) : 0
            });
        }

        return new AdminDashboardDto
        {
            TotalUsers = totalUsers,
            TotalQuizzes = totalQuizzes,
            TotalAttempts = totalAttempts,
            AverageScore = averageScore,
            TrendLast30Days = trendList
        };
    }

    public async Task<PagedResult<AdminAttemptDto>> GetAdminAttemptsAsync(AdminAttemptQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = _context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.Quiz)
            .Include(a => a.User)
            .AsQueryable();

        if (parameters.QuizId.HasValue)
        {
            query = query.Where(a => a.QuizId == parameters.QuizId.Value);
        }

        if (parameters.UserId.HasValue)
        {
            query = query.Where(a => a.UserId == parameters.UserId.Value);
        }

        if (parameters.Status.HasValue)
        {
            query = query.Where(a => a.Status == parameters.Status.Value);
        }

        if (parameters.FromDateUtc.HasValue)
        {
            query = query.Where(a => a.StartedAtUtc >= parameters.FromDateUtc.Value);
        }

        if (parameters.ToDateUtc.HasValue)
        {
            query = query.Where(a => a.StartedAtUtc <= parameters.ToDateUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.SearchQuery))
        {
            var search = parameters.SearchQuery.Trim().ToLower();
            query = query.Where(a => a.Quiz.Title.ToLower().Contains(search)
                || a.User.FullName.ToLower().Contains(search)
                || a.User.Email.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var isAscending = string.Equals(parameters.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        query = parameters.SortBy?.ToLowerInvariant() switch
        {
            "quiztitle" => isAscending ? query.OrderBy(a => a.Quiz.Title) : query.OrderByDescending(a => a.Quiz.Title),
            "userfullname" => isAscending ? query.OrderBy(a => a.User.FullName) : query.OrderByDescending(a => a.User.FullName),
            "score" => isAscending ? query.OrderBy(a => a.Score) : query.OrderByDescending(a => a.Score),
            "percent" => isAscending ? query.OrderBy(a => a.Percent) : query.OrderByDescending(a => a.Percent),
            "startedatutc" => isAscending ? query.OrderBy(a => a.StartedAtUtc) : query.OrderByDescending(a => a.StartedAtUtc),
            _ => isAscending ? query.OrderBy(a => a.StartedAtUtc) : query.OrderByDescending(a => a.StartedAtUtc)
        };

        var items = await query
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(a => new AdminAttemptDto
            {
                AttemptId = a.Id,
                QuizId = a.QuizId,
                QuizTitle = a.Quiz.Title,
                UserId = a.UserId,
                UserFullName = a.User.FullName,
                UserEmail = a.User.Email,
                StartedAtUtc = a.StartedAtUtc,
                SubmittedAtUtc = a.SubmittedAtUtc,
                Status = a.Status.ToString(),
                Score = a.Score,
                TotalMarks = a.TotalMarks,
                Percent = a.Percent,
                Passed = a.Passed,
                TabSwitchCount = a.TabSwitchCount
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminAttemptDto>(items, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<QuizLeaderboardDto> GetQuizLeaderboardAsync(Guid quizId, CancellationToken cancellationToken = default)
    {
        var quiz = await _context.Quizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quizId && !q.IsDeleted, cancellationToken);

        if (quiz == null)
        {
            throw new AuthException("Quiz not found.", HttpStatusCode.NotFound);
        }

        var completedAttempts = await _context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.QuizId == quizId && a.Status != QuizAttemptStatus.InProgress)
            .ToListAsync(cancellationToken);

        // Group by user so only each student's best attempt is ranked, or rank all completed attempts
        // Best score first, tie-break by shortest completion time
        var rankedList = completedAttempts
            .Select(a =>
            {
                var durationTaken = (a.SubmittedAtUtc.HasValue && a.SubmittedAtUtc > a.StartedAtUtc)
                    ? (a.SubmittedAtUtc.Value - a.StartedAtUtc).TotalMinutes
                    : (a.EndsAtUtc - a.StartedAtUtc).TotalMinutes;

                return new
                {
                    Attempt = a,
                    Duration = Math.Round(durationTaken, 2)
                };
            })
            .OrderByDescending(x => x.Attempt.Score)
            .ThenBy(x => x.Duration)
            .ToList();

        var leaderboardEntries = new List<LeaderboardEntryDto>();
        for (int i = 0; i < rankedList.Count; i++)
        {
            var item = rankedList[i];
            leaderboardEntries.Add(new LeaderboardEntryDto
            {
                Rank = i + 1,
                UserId = item.Attempt.UserId,
                FullName = item.Attempt.User.FullName,
                ProfileImageUrl = item.Attempt.User.ProfileImageUrl,
                Score = item.Attempt.Score,
                TotalMarks = item.Attempt.TotalMarks,
                Percent = item.Attempt.Percent,
                Passed = item.Attempt.Passed,
                SubmittedAtUtc = item.Attempt.SubmittedAtUtc,
                DurationMinutesTaken = item.Duration
            });
        }

        return new QuizLeaderboardDto
        {
            QuizId = quiz.Id,
            QuizTitle = quiz.Title,
            TotalParticipants = leaderboardEntries.Count,
            Leaderboard = leaderboardEntries
        };
    }

    public async Task<(byte[] FileBytes, string FileName)> ExportQuizResultsToExcelAsync(Guid quizId, CancellationToken cancellationToken = default)
    {
        var quiz = await _context.Quizzes
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quizId && !q.IsDeleted, cancellationToken);

        if (quiz == null)
        {
            throw new AuthException("Quiz not found.", HttpStatusCode.NotFound);
        }

        var attempts = await _context.QuizAttempts
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.QuizId == quizId && a.Status != QuizAttemptStatus.InProgress)
            .OrderByDescending(a => a.Score)
            .ThenBy(a => a.SubmittedAtUtc)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Results");

        // Title row
        worksheet.Cell(1, 1).Value = $"PushpakClasses Quiz Results: {quiz.Title}";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Row(1).Height = 25;

        // Header row
        string[] headers =
        {
            "Rank", "Student Name", "Email", "Score", "Total Marks",
            "Percentage", "Passed", "Tab Switches", "Status", "Submitted At (UTC)"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(3, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(25, 45, 95); // Deep navy blue
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Data rows
        int row = 4;
        for (int i = 0; i < attempts.Count; i++)
        {
            var a = attempts[i];
            worksheet.Cell(row, 1).Value = i + 1;
            worksheet.Cell(row, 2).Value = a.User.FullName;
            worksheet.Cell(row, 3).Value = a.User.Email;
            worksheet.Cell(row, 4).Value = a.Score;
            worksheet.Cell(row, 5).Value = a.TotalMarks;
            worksheet.Cell(row, 6).Value = $"{a.Percent}%";
            worksheet.Cell(row, 7).Value = a.Passed ? "PASSED" : "FAILED";
            worksheet.Cell(row, 8).Value = a.TabSwitchCount;
            worksheet.Cell(row, 9).Value = a.Status.ToString();
            worksheet.Cell(row, 10).Value = (a.SubmittedAtUtc ?? a.EndsAtUtc).ToString("yyyy-MM-dd HH:mm:ss");

            // Format pass/fail
            worksheet.Cell(row, 7).Style.Font.Bold = true;
            worksheet.Cell(row, 7).Style.Font.FontColor = a.Passed ? XLColor.Green : XLColor.Red;

            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var sanitizedQuizTitle = string.Join("_", quiz.Title.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
        var fileName = $"{sanitizedQuizTitle}_Results_{DateTime.UtcNow:yyyyMMdd}.xlsx";

        return (stream.ToArray(), fileName);
    }
}
