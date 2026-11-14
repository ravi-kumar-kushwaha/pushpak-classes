using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Quizzes.Dtos;
using PushpakClasses.Domain.Entities;

namespace PushpakClasses.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly IPushpakClassesDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CategoryService(
        IPushpakClassesDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<CategoryDto>> GetAllCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AsNoTracking()
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                QuizCount = c.Quizzes.Count(q => !q.IsDeleted)
            })
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<CategoryDto>> GetCategoriesPagedAsync(CategoryQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = _context.Categories.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.SearchQuery))
        {
            var search = parameters.SearchQuery.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var isAscending = string.Equals(parameters.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        query = parameters.SortBy?.ToLower() switch
        {
            "quizcount" => isAscending
                ? query.OrderBy(c => c.Quizzes.Count(q => !q.IsDeleted))
                : query.OrderByDescending(c => c.Quizzes.Count(q => !q.IsDeleted)),
            _ => isAscending
                ? query.OrderBy(c => c.Name)
                : query.OrderByDescending(c => c.Name)
        };

        var items = await query
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                QuizCount = c.Quizzes.Count(q => !q.IsDeleted)
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<CategoryDto>(items, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<CategoryDto> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                QuizCount = c.Quizzes.Count(q => !q.IsDeleted)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (category == null)
        {
            throw new AuthException("Category not found.", HttpStatusCode.NotFound);
        }

        return category;
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequestDto request, CancellationToken cancellationToken = default)
    {
        var trimmedName = request.Name.Trim();

        var exists = await _context.Categories
            .AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (exists)
        {
            throw new AuthException($"A category named '{trimmedName}' already exists.");
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = trimmedName
        };

        await _context.Categories.AddAsync(category, cancellationToken);

        await WriteAuditLogAsync("AdminCreateCategory", category.Id, new { category.Name }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            QuizCount = 0
        };
    }

    public async Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequestDto request, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category == null)
        {
            throw new AuthException("Category not found.", HttpStatusCode.NotFound);
        }

        var trimmedName = request.Name.Trim();

        var exists = await _context.Categories
            .AnyAsync(c => c.Id != id && c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (exists)
        {
            throw new AuthException($"A category named '{trimmedName}' already exists.");
        }

        var oldName = category.Name;
        category.Name = trimmedName;

        await WriteAuditLogAsync("AdminUpdateCategory", category.Id, new { OldName = oldName, NewName = trimmedName }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var quizCount = await _context.Quizzes.CountAsync(q => q.CategoryId == id && !q.IsDeleted, cancellationToken);

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            QuizCount = quizCount
        };
    }

    public async Task<AuthMessageResponseDto> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .Include(c => c.Quizzes)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null)
        {
            throw new AuthException("Category not found.", HttpStatusCode.NotFound);
        }

        var activeQuizCount = category.Quizzes.Count(q => !q.IsDeleted);
        if (activeQuizCount > 0)
        {
            throw new AuthException($"Cannot delete category '{category.Name}' because it contains {activeQuizCount} active quiz(zes). Please reassign or delete quizzes first.", HttpStatusCode.BadRequest);
        }

        _context.Categories.Remove(category);

        await WriteAuditLogAsync("AdminDeleteCategory", category.Id, new { category.Name }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new AuthMessageResponseDto("Category deleted successfully.");
    }

    private async Task WriteAuditLogAsync(string action, Guid entityId, object details, CancellationToken cancellationToken)
    {
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUserService.UserId,
            Action = action,
            EntityName = nameof(Category),
            EntityId = entityId.ToString(),
            DetailsJson = JsonSerializer.Serialize(details),
            CreatedAtUtc = DateTime.UtcNow
        };

        await _context.AuditLogs.AddAsync(audit, cancellationToken);
    }
}
