using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

public interface IProductService
{
    /// <summary>Returns a filtered, sorted, paged product list plus paging metadata.</summary>
    Task<PagedResult<ProductDto>> SearchAsync(ProductFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Returns all active categories (for dropdowns).</summary>
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all active measurement units (for dropdowns).</summary>
    Task<IReadOnlyList<UnitDto>> GetUnitsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns summary counters for the product screen.</summary>
    Task<ProductStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a single product, or null when it does not exist.</summary>
    Task<ProductDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    Task<ProductDto> UpdateAsync(UpdateProductRequest request, CancellationToken cancellationToken = default);

    /// <summary>Activates or deactivates a product. Deactivation is preferred over deletion.</summary>
    Task<ProductDto> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard-deletes a product only when it is not referenced by any transaction,
    /// stock movement, batch or recipe. Throws when the product is referenced.
    /// </summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}