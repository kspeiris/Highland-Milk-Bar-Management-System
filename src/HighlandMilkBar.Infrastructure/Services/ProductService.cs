using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

public sealed class ProductService(AppDbContext db) : IProductService
{
    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : "Uncategorised",
                Type = p.Type.ToString(),
                Unit = p.Unit,
                CostPrice = p.CostPrice,
                SellingPrice = p.SellingPrice,
                CurrentStock = p.CurrentStock,
                ReorderLevel = p.ReorderLevel,
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryDto { Id = c.Id, Name = c.Name })
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var sku = request.Sku.Trim();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(sku)) throw new ArgumentException("A product code (SKU) is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A product name is required.");
        if (request.SellingPrice < 0 || request.CostPrice < 0 || request.OpeningStock < 0 || request.ReorderLevel < 0)
            throw new ArgumentException("Prices and stock quantities cannot be negative.");
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken))
            throw new ArgumentException("Please select a valid active category.");
        if (await db.Products.AnyAsync(p => p.Sku == sku, cancellationToken))
            throw new ArgumentException("That SKU is already in use. Enter a unique product code.");

        var product = new Product
        {
            Sku = sku,
            Name = name,
            CategoryId = request.CategoryId,
            Type = request.Type,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "Unit" : request.Unit.Trim(),
            CostPrice = request.CostPrice,
            SellingPrice = request.SellingPrice,
            CurrentStock = request.TrackInventory ? request.OpeningStock : 0,
            ReorderLevel = request.ReorderLevel,
            TrackInventory = request.TrackInventory,
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return new ProductDto
        {
            Id = product.Id,
            Sku = product.Sku,
            Name = product.Name,
            CategoryId = product.CategoryId,
            CategoryName = (await db.Categories.Where(c => c.Id == product.CategoryId).Select(c => c.Name).SingleAsync(cancellationToken)),
            Type = product.Type.ToString(),
            Unit = product.Unit,
            CostPrice = product.CostPrice,
            SellingPrice = product.SellingPrice,
            CurrentStock = product.CurrentStock,
            ReorderLevel = product.ReorderLevel,
            IsActive = product.IsActive
        };
    }
}
