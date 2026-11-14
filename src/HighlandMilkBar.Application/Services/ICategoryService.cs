using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

public interface ICategoryService
{
    /// <summary>Returns categories with their product counts, optionally filtered and searched.</summary>
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeInactive = true, string? search = null, CancellationToken cancellationToken = default);

    Task<CategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryDto> UpdateAsync(UpdateCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Activates or deactivates a category without touching its products.</summary>
    Task<CategoryDto> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Deletes a category only when no products reference it; throws otherwise.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}