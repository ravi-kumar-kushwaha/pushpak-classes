using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using PushpakClasses.Application.Common.Exceptions;
using PushpakClasses.Application.Common.Interfaces;
using PushpakClasses.Application.Features.Quizzes.Dtos;
using PushpakClasses.Domain.Entities;
using PushpakClasses.Infrastructure.Persistence;
using PushpakClasses.Infrastructure.Services;
using Xunit;

namespace PushpakClasses.UnitTests.Features.Quizzes;

public class CategoryServiceTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Guid _adminUserId = Guid.NewGuid();

    public CategoryServiceTests()
    {
        _currentUserMock.Setup(u => u.UserId).Returns(_adminUserId);
        _currentUserMock.Setup(u => u.Role).Returns("Admin");
    }

    private PushpakClassesDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PushpakClassesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PushpakClassesDbContext(options);
    }

    [Fact]
    public async Task GetAllCategoriesAsync_ReturnsAlphabeticallyOrderedCategoriesWithQuizCount()
    {
        // Arrange
        using var context = CreateDbContext();
        var catB = new Category { Id = Guid.NewGuid(), Name = "Biology" };
        var catA = new Category { Id = Guid.NewGuid(), Name = "Algebra" };
        await context.Categories.AddRangeAsync(catB, catA);

        await context.Quizzes.AddAsync(new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Math 101",
            CategoryId = catA.Id,
            DurationMinutes = 30,
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(2),
            IsDeleted = false
        });

        await context.SaveChangesAsync();

        var service = new CategoryService(context, _currentUserMock.Object);

        // Act
        var result = await service.GetAllCategoriesAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Algebra");
        result[0].QuizCount.Should().Be(1);
        result[1].Name.Should().Be("Biology");
        result[1].QuizCount.Should().Be(0);
    }

    [Fact]
    public async Task GetCategoryByIdAsync_ExistingCategory_ReturnsDto()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat = new Category { Id = Guid.NewGuid(), Name = "History" };
        await context.Categories.AddAsync(cat);
        await context.SaveChangesAsync();

        var service = new CategoryService(context, _currentUserMock.Object);

        // Act
        var result = await service.GetCategoryByIdAsync(cat.Id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(cat.Id);
        result.Name.Should().Be("History");
    }

    [Fact]
    public async Task GetCategoryByIdAsync_NonExistingCategory_ThrowsNotFound()
    {
        // Arrange
        using var context = CreateDbContext();
        var service = new CategoryService(context, _currentUserMock.Object);

        // Act & Assert
        var act = () => service.GetCategoryByIdAsync(Guid.NewGuid());
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateCategoryAsync_UniqueName_CreatesCategoryAndAuditLog()
    {
        // Arrange
        using var context = CreateDbContext();
        var service = new CategoryService(context, _currentUserMock.Object);

        // Act
        var result = await service.CreateCategoryAsync(new CreateCategoryRequestDto { Name = "Science" });

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Science");

        var dbCategory = await context.Categories.FirstOrDefaultAsync(c => c.Id == result.Id);
        dbCategory.Should().NotBeNull();
        dbCategory!.Name.Should().Be("Science");

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == result.Id.ToString());
        audit.Should().NotBeNull();
        audit!.Action.Should().Be("AdminCreateCategory");
        audit.UserId.Should().Be(_adminUserId);
    }

    [Fact]
    public async Task CreateCategoryAsync_DuplicateName_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        await context.Categories.AddAsync(new Category { Id = Guid.NewGuid(), Name = "Chemistry" });
        await context.SaveChangesAsync();

        var service = new CategoryService(context, _currentUserMock.Object);

        // Act & Assert
        var act = () => service.CreateCategoryAsync(new CreateCategoryRequestDto { Name = "chemistry" });
        await act.Should().ThrowAsync<AuthException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateCategoryAsync_UpdatesNameAndAuditLog()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat = new Category { Id = Guid.NewGuid(), Name = "Old Name" };
        await context.Categories.AddAsync(cat);
        await context.SaveChangesAsync();

        var service = new CategoryService(context, _currentUserMock.Object);

        // Act
        var result = await service.UpdateCategoryAsync(cat.Id, new UpdateCategoryRequestDto { Name = "New Name" });

        // Assert
        result.Name.Should().Be("New Name");

        var updatedInDb = await context.Categories.FindAsync(cat.Id);
        updatedInDb!.Name.Should().Be("New Name");

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == cat.Id.ToString() && a.Action == "AdminUpdateCategory");
        audit.Should().NotBeNull();
        audit!.DetailsJson.Should().Contain("Old Name").And.Contain("New Name");
    }

    [Fact]
    public async Task UpdateCategoryAsync_DuplicateName_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat1 = new Category { Id = Guid.NewGuid(), Name = "Physics" };
        var cat2 = new Category { Id = Guid.NewGuid(), Name = "Math" };
        await context.Categories.AddRangeAsync(cat1, cat2);
        await context.SaveChangesAsync();

        var service = new CategoryService(context, _currentUserMock.Object);

        // Act & Assert
        var act = () => service.UpdateCategoryAsync(cat2.Id, new UpdateCategoryRequestDto { Name = "physics" });
        await act.Should().ThrowAsync<AuthException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task DeleteCategoryAsync_WithActiveQuizzes_ThrowsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat = new Category { Id = Guid.NewGuid(), Name = "Geology" };
        await context.Categories.AddAsync(cat);

        await context.Quizzes.AddAsync(new Quiz
        {
            Id = Guid.NewGuid(),
            Title = "Rocks and Minerals",
            CategoryId = cat.Id,
            DurationMinutes = 20,
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(1),
            IsDeleted = false
        });
        await context.SaveChangesAsync();

        var service = new CategoryService(context, _currentUserMock.Object);

        // Act & Assert
        var act = () => service.DeleteCategoryAsync(cat.Id);
        var ex = await act.Should().ThrowAsync<AuthException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.Which.Message.Should().Contain("active quiz");
    }

    [Fact]
    public async Task DeleteCategoryAsync_WithoutActiveQuizzes_DeletesSuccessfully()
    {
        // Arrange
        using var context = CreateDbContext();
        var cat = new Category { Id = Guid.NewGuid(), Name = "Empty Category" };
        await context.Categories.AddAsync(cat);
        await context.SaveChangesAsync();

        var service = new CategoryService(context, _currentUserMock.Object);

        // Act
        var result = await service.DeleteCategoryAsync(cat.Id);

        // Assert
        result.Message.Should().Contain("deleted successfully");
        var dbCategory = await context.Categories.FindAsync(cat.Id);
        dbCategory.Should().BeNull();

        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == cat.Id.ToString() && a.Action == "AdminDeleteCategory");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCategoriesPagedAsync_ReturnsPagedResultsWithSearchAndSorting()
    {
        // Arrange
        using var context = CreateDbContext();
        for (int i = 1; i <= 25; i++)
        {
            await context.Categories.AddAsync(new Category
            {
                Id = Guid.NewGuid(),
                Name = $"Category {i:D2}"
            });
        }
        await context.SaveChangesAsync();

        var service = new CategoryService(context, _currentUserMock.Object);

        // Act - Page 1 with pageSize 10
        var page1 = await service.GetCategoriesPagedAsync(new PushpakClasses.Application.Common.Models.CategoryQueryParameters
        {
            Page = 1,
            PageSize = 10,
            SortBy = "Name",
            SortDir = "asc"
        });

        // Assert
        page1.TotalCount.Should().Be(25);
        page1.Items.Should().HaveCount(10);
        page1.Page.Should().Be(1);
        page1.PageNumber.Should().Be(1);
        page1.TotalPages.Should().Be(3);
        page1.HasNextPage.Should().BeTrue();
        page1.HasPreviousPage.Should().BeFalse();

        // Act - Search
        var searchResult = await service.GetCategoriesPagedAsync(new PushpakClasses.Application.Common.Models.CategoryQueryParameters
        {
            Search = "05",
            Page = 1,
            PageSize = 10
        });

        searchResult.TotalCount.Should().Be(1);
        searchResult.Items[0].Name.Should().Be("Category 05");
    }
}
