using PushpakClasses.Application.Common.Models;
using PushpakClasses.Application.Features.Auth.Dtos;
using PushpakClasses.Application.Features.Quizzes.Dtos;

namespace PushpakClasses.Application.Common.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllCategoriesAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<CategoryDto>> GetCategoriesPagedAsync(CategoryQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<CategoryDto> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequestDto request, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthMessageResponseDto> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);
}
