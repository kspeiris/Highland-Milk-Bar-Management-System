using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

public sealed class ProductService(AppDbContext db) : IProductService
{
    private static string TypeName(ProductType type) => type switch
    {
        ProductType.Packaged => "Packaged",
        ProductType.Ingredient => "Ingredient",
        ProductType.PreparedItem => "Prepared item",
        _ => type.ToString()
    };

    public async Task<PagedResult<ProductDto>> SearchAsync(ProductFilter filter, CancellationToken cancellationToken = default)
    {
        filter ??= new ProductFilter();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 200 ? 20 : filter.PageSize;
        var search = filter.Search?.Trim();

        var query = db.Products.AsNoTracking().Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // EF translates Contains to a LIKE; escaped to keep user input literal.
            var like = $"%{EscapeLike(search)}%";
            query = query.Where(p => EF.Functions.Like(p.Name, like)
                || EF.Functions.Like(p.Sku, like)
                || (p.Barcode != null && EF.Functions.Like(p.Barcode, like))
                || (p.Category != null && EF.Functions.Like(p.Category.Name, like)));
        }

        if (filter.CategoryId is { } categoryId)
            query = query.Where(p => p.CategoryId == categoryId);

        if (filter.Type is { } type)
            query = query.Where(p => p.Type == type);

        if (filter.IsActive is { } isActive)
            query = query.Where(p => p.IsActive == isActive);

        query = filter.SortBy switch
        {
            "sku" => filter.SortDescending ? query.OrderByDescending(p => p.Sku) : query.OrderBy(p => p.Sku),
            "category" => filter.SortDescending ? query.OrderByDescending(p => p.Category!.Name) : query.OrderBy(p => p.Category!.Name),
            "sellingprice" => filter.SortDescending ? query.OrderByDescending(p => p.SellingPrice) : query.OrderBy(p => p.SellingPrice),
            "stock" => filter.SortDescending ? query.OrderByDescending(p => p.CurrentStock) : query.OrderBy(p => p.CurrentStock),
            _ => filter.SortDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name)
        };

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                Barcode = p.Barcode,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : "Uncategorised",
                Type = p.Type,
                Unit = p.Unit,
                CostPrice = p.CostPrice,
                SellingPrice = p.SellingPrice,
                CurrentStock = p.CurrentStock,
                ReorderLevel = p.ReorderLevel,
                TrackInventory = p.TrackInventory,
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items) item.TypeName = TypeName(item.Type);

        return new PagedResult<ProductDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto { Id = c.Id, Name = c.Name, Description = c.Description, IsActive = c.IsActive, SortOrder = c.SortOrder })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UnitDto>> GetUnitsAsync(CancellationToken cancellationToken = default)
    {
        return await db.Units.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.SortOrder).ThenBy(u => u.Name)
            .Select(u => new UnitDto { Id = u.Id, Name = u.Name, Symbol = u.Symbol, Kind = u.Kind })
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var total = await db.Products.CountAsync(cancellationToken);
        var active = await db.Products.CountAsync(p => p.IsActive, cancellationToken);
        var categories = await db.Categories.CountAsync(cancellationToken);
        return new ProductStatsDto
        {
            TotalProducts = total,
            ActiveProducts = active,
            InactiveProducts = total - active,
            Categories = categories
        };
    }

    public async Task<ProductDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var p = await db.Products.AsNoTracking().Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (p is null) return null;

        return new ProductDetailDto
        {
            Id = p.Id,
            Sku = p.Sku,
            Name = p.Name,
            Barcode = p.Barcode,
            CategoryId = p.CategoryId,
            CategoryName = p.Category != null ? p.Category.Name : "Uncategorised",
            Type = p.Type,
            TypeName = TypeName(p.Type),
            Unit = p.Unit,
            BaseUnitId = p.BaseUnitId,
            CostPrice = p.CostPrice,
            SellingPrice = p.SellingPrice,
            CurrentStock = p.CurrentStock,
            ReorderLevel = p.ReorderLevel,
            TrackInventory = p.TrackInventory,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var sku = request.Sku?.Trim() ?? string.Empty;
        var barcode = NormalizeBarcode(request.Barcode);

        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A product name is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("A product code (SKU) is required.", nameof(request));
        if (request.CostPrice < 0) throw new ArgumentException("Purchase price cannot be negative.", nameof(request));
        if (request.SellingPrice < 0) throw new ArgumentException("Selling price cannot be negative.", nameof(request));
        if (request.OpeningStock < 0) throw new ArgumentException("Opening stock cannot be negative.", nameof(request));
        if (request.ReorderLevel < 0) throw new ArgumentException("Reorder level cannot be negative.", nameof(request));
        if (request.TrackInventory && request.BaseUnitId is null)
            throw new ArgumentException("Select a valid unit for an inventory-tracked product.", nameof(request));

        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken))
            throw new ArgumentException("Select a valid active category.", nameof(request));
        if (await db.Products.AnyAsync(p => p.Sku == sku, cancellationToken))
            throw new ArgumentException("That product code (SKU) is already in use.", nameof(request));
        if (barcode is not null && await db.Products.AnyAsync(p => p.Barcode == barcode, cancellationToken))
            throw new ArgumentException("That barcode is already in use by another product.", nameof(request));

        var product = new Product
        {
            Sku = sku,
            Name = name,
            Barcode = barcode,
            CategoryId = request.CategoryId,
            Type = request.Type,
            Unit = string.IsNullOrWhiteSpace(request.Unit) && request.BaseUnitId is not null
                ? UnitName(request.BaseUnitId.Value)
                : (string.IsNullOrWhiteSpace(request.Unit) ? "Unit" : request.Unit.Trim()),
            BaseUnitId = request.BaseUnitId,
            CostPrice = request.CostPrice,
            SellingPrice = request.SellingPrice,
            CurrentStock = request.TrackInventory ? request.OpeningStock : 0m,
            ReorderLevel = request.ReorderLevel,
            TrackInventory = request.TrackInventory,
            IsActive = request.IsActive,
            CreatedAt = DateTime.Now
        };

        db.Products.Add(product);
        await SaveWithFriendlyErrorsAsync(cancellationToken);
        return await GetRequiredDtoAsync(product.Id, cancellationToken);
    }

    public async Task<ProductDto> UpdateAsync(UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("The product could not be found.");

        var name = request.Name?.Trim() ?? string.Empty;
        var sku = request.Sku?.Trim() ?? string.Empty;
        var barcode = NormalizeBarcode(request.Barcode);

        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A product name is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("A product code (SKU) is required.", nameof(request));
        if (request.CostPrice < 0) throw new ArgumentException("Purchase price cannot be negative.", nameof(request));
        if (request.SellingPrice < 0) throw new ArgumentException("Selling price cannot be negative.", nameof(request));
        if (request.ReorderLevel < 0) throw new ArgumentException("Reorder level cannot be negative.", nameof(request));
        if (request.TrackInventory && product.BaseUnitId is null && string.IsNullOrWhiteSpace(request.Unit))
            throw new ArgumentException("Select a valid unit for an inventory-tracked product.", nameof(request));

        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
            throw new ArgumentException("Select a valid category.", nameof(request));
        if (await db.Products.AnyAsync(p => p.Id != request.Id && p.Sku == sku, cancellationToken))
            throw new ArgumentException("That product code (SKU) is already in use by another product.", nameof(request));
        if (barcode is not null && await db.Products.AnyAsync(p => p.Id != request.Id && p.Barcode == barcode, cancellationToken))
            throw new ArgumentException("That barcode is already in use by another product.", nameof(request));

        product.Name = name;
        product.Sku = sku;
        product.Barcode = barcode;
        product.CategoryId = request.CategoryId;
        product.Type = request.Type;
        product.Unit = string.IsNullOrWhiteSpace(request.Unit)
            ? (product.BaseUnitId is { } baseId ? UnitName(baseId) : "Unit")
            : request.Unit.Trim();
        product.CostPrice = request.CostPrice;
        product.SellingPrice = request.SellingPrice;
        product.ReorderLevel = request.ReorderLevel;
        product.TrackInventory = request.TrackInventory;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.Now;

        await SaveWithFriendlyErrorsAsync(cancellationToken);
        return await GetRequiredDtoAsync(product.Id, cancellationToken);
    }

    public async Task<ProductDto> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("The product could not be found.");
        product.IsActive = isActive;
        product.UpdatedAt = DateTime.Now;
        await SaveWithFriendlyErrorsAsync(cancellationToken);
        return await GetRequiredDtoAsync(product.Id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("The product could not be found.");

        var referenced = await db.SaleItems.AnyAsync(i => i.ProductId == id, cancellationToken)
            || await db.PurchaseItems.AnyAsync(i => i.ProductId == id, cancellationToken)
            || await db.StockMovements.AnyAsync(m => m.ProductId == id, cancellationToken)
            || await db.RecipeItems.AnyAsync(i => i.IngredientProductId == id, cancellationToken)
            || await db.Recipes.AnyAsync(r => r.MenuProductId == id, cancellationToken)
            || await db.InventoryBatches.AnyAsync(b => b.ProductId == id, cancellationToken)
            || await db.StockAdjustmentItems.AnyAsync(i => i.ProductId == id, cancellationToken);
        if (referenced)
            throw new InvalidOperationException("This product is referenced by transactions or inventory history and cannot be deleted. Deactivate it instead.");

        db.Products.Remove(product);
        await SaveWithFriendlyErrorsAsync(cancellationToken);
    }

    private async Task<ProductDto> GetRequiredDtoAsync(int id, CancellationToken cancellationToken)
    {
        var dto = await GetByIdAsync(id, cancellationToken);
        return dto is null
            ? throw new InvalidOperationException("The product could not be found after saving.")
            : new ProductDto
            {
                Id = dto.Id,
                Sku = dto.Sku,
                Name = dto.Name,
                Barcode = dto.Barcode,
                CategoryId = dto.CategoryId,
                CategoryName = dto.CategoryName,
                Type = dto.Type,
                TypeName = dto.TypeName,
                Unit = dto.Unit,
                CostPrice = dto.CostPrice,
                SellingPrice = dto.SellingPrice,
                CurrentStock = dto.CurrentStock,
                ReorderLevel = dto.ReorderLevel,
                TrackInventory = dto.TrackInventory,
                IsActive = dto.IsActive
            };
    }

    private string UnitName(int unitId)
        => db.Units.Where(u => u.Id == unitId).Select(u => u.Name).FirstOrDefault() ?? "Unit";

    private async Task SaveWithFriendlyErrorsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ArgumentException("A product with the same code, barcode or category name already exists.", nameof(Product));
        }
    }

    private static string? NormalizeBarcode(string? barcode)
        => string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}