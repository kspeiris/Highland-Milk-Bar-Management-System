using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

public sealed class CategoryService(AppDbContext db) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(bool includeInactive = true, string? search = null, CancellationToken cancellationToken = default)
    {
        var query = db.Categories.AsNoTracking().AsQueryable();
        if (!includeInactive) query = query.Where(c => c.IsActive);

        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var like = $"%{EscapeLike(term)}%";
            query = query.Where(c => EF.Functions.Like(c.Name, like)
                || (c.Description != null && EF.Functions.Like(c.Description, like)));
        }

        return await query
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive,
                ProductCount = c.Products.Count,
                SortOrder = c.SortOrder
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                IsActive = c.IsActive,
                ProductCount = c.Products.Count,
                SortOrder = c.SortOrder
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A category name is required.", nameof(request));
        if (await NameExistsAsync(name, excludeId: null, cancellationToken))
            throw new ArgumentException("A category with that name already exists.", nameof(request));

        var maxSort = await db.Categories.MaxAsync(c => (int?)c.SortOrder, cancellationToken) ?? 0;
        var category = new Category
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SortOrder = maxSort + 1,
            IsActive = true
        };
        db.Categories.Add(category);
        await SaveWithFriendlyErrorsAsync(cancellationToken);
        return await GetRequiredDtoAsync(category.Id, cancellationToken);
    }

    public async Task<CategoryDto> UpdateAsync(UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("The category could not be found.");

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A category name is required.", nameof(request));
        if (await NameExistsAsync(name, excludeId: request.Id, cancellationToken))
            throw new ArgumentException("A category with that name already exists.", nameof(request));

        category.Name = name;
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        await SaveWithFriendlyErrorsAsync(cancellationToken);
        return await GetRequiredDtoAsync(category.Id, cancellationToken);
    }

    public async Task<CategoryDto> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("The category could not be found.");
        // Deliberately does not touch products: deactivating a category must not deactivate them.
        category.IsActive = isActive;
        await SaveWithFriendlyErrorsAsync(cancellationToken);
        return await GetRequiredDtoAsync(category.Id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("The category could not be found.");
        if (await db.Products.AnyAsync(p => p.CategoryId == id, cancellationToken))
            throw new InvalidOperationException("This category contains products and cannot be deleted. Deactivate it instead.");
        db.Categories.Remove(category);
        await SaveWithFriendlyErrorsAsync(cancellationToken);
    }

    private async Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken cancellationToken)
    {
        // Case-insensitive check so "Fresh Milk" and "fresh milk" are treated as duplicates.
        var lowered = name.ToLowerInvariant();
        return await db.Categories.AnyAsync(
            c => c.Id != (excludeId ?? 0) && c.Name.ToLower() == lowered,
            cancellationToken);
    }

    private async Task<CategoryDto> GetRequiredDtoAsync(int id, CancellationToken cancellationToken)
        => await GetByIdAsync(id, cancellationToken)
           ?? throw new InvalidOperationException("The category could not be found after saving.");

    private async Task SaveWithFriendlyErrorsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ArgumentException("A category with that name already exists.", nameof(Category));
        }
    }

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}