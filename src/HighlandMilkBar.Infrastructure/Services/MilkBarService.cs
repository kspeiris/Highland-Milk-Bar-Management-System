using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Read-only Milk Bar queries: the operational dashboard, the menu-item list and the ingredient
/// list. Recipe costing/availability is delegated to <see cref="IRecipeService"/> so there is a
/// single costing policy.
/// </summary>
public sealed class MilkBarService(AppDbContext db, IRecipeService recipes) : IMilkBarService
{
    public async Task<MilkBarOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;

        var preparedProducts = await db.Products.AsNoTracking()
            .Where(p => p.Type == ProductType.PreparedItem)
            .Select(p => new { p.Id, p.Name, p.CategoryId, CategoryName = p.Category != null ? p.Category.Name : "Uncategorised", p.SellingPrice, p.IsActive })
            .ToListAsync(cancellationToken);

        var activeRecipes = await db.Recipes.AsNoTracking()
            .Include(r => r.MenuProduct)
            .Include(r => r.Items).ThenInclude(i => i.IngredientProduct)
            .Where(r => r.IsActive)
            .ToListAsync(cancellationToken);

        var activeByProduct = activeRecipes
            .Where(r => r.MenuProduct != null)
            .GroupBy(r => r.MenuProductId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Version).First());

        // Per-recipe cost and margin (uses the shared costing service).
        var costByRecipe = new Dictionary<int, decimal>();
        var marginByProduct = new Dictionary<int, decimal>();
        var allLines = new List<RecipeCostLine>();
        foreach (var recipe in activeRecipes)
        {
            var yield = recipe.YieldQuantity <= 0 ? 1 : recipe.YieldQuantity;
            var cost = await recipes.CalculateCostAsync(recipe.Id, yield, cancellationToken);
            costByRecipe[recipe.Id] = cost.CostPerServing;
            allLines.AddRange(cost.Lines);

            var price = recipe.MenuProduct?.SellingPrice ?? 0m;
            if (price > 0)
                marginByProduct[recipe.MenuProductId] = (price - cost.CostPerServing) / price * 100m;
        }

        // Ingredient usage and low-stock.
        var ingredientIds = await db.RecipeItems.AsNoTracking()
            .Select(i => i.IngredientProductId).Distinct().ToListAsync(cancellationToken);
        var stockMap = await LoadIngredientStockAsync(ingredientIds, cancellationToken);
        var lowStockIngredients = await db.Products.AsNoTracking()
            .Where(p => ingredientIds.Contains(p.Id) && p.TrackInventory && p.IsActive)
            .Select(p => new { p.Id, p.Name, p.ReorderLevel })
            .ToListAsync(cancellationToken);
        var lowStockCount = lowStockIngredients.Count(p =>
            stockMap.TryGetValue(p.Id, out var s) && s.Sellable <= p.ReorderLevel);

        var menuByCategory = preparedProducts
            .Where(p => p.IsActive)
            .GroupBy(p => p.CategoryName)
            .Select(g => new CategoryCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(c => c.Count)
            .ToList();

        var highestCost = activeRecipes
            .Where(r => r.MenuProduct != null)
            .OrderByDescending(r => costByRecipe.GetValueOrDefault(r.Id))
            .Take(5)
            .Select(r => new RecipeCostRankDto
            {
                Name = r.Name,
                Category = r.MenuProduct?.Category?.Name ?? r.MenuProduct?.Name ?? "Uncategorised",
                CostPerServing = costByRecipe.GetValueOrDefault(r.Id)
            })
            .ToList();

        var marginRows = preparedProducts
            .Where(p => p.IsActive && activeByProduct.ContainsKey(p.Id) && p.SellingPrice > 0)
            .Select(p => new MenuMarginRankDto
            {
                Name = p.Name,
                SellingPrice = p.SellingPrice,
                Cost = costByRecipe.GetValueOrDefault(activeByProduct[p.Id].Id),
                MarginPercent = marginByProduct.GetValueOrDefault(p.Id)
            })
            .ToList();

        var highestMargin = marginRows.OrderByDescending(m => m.MarginPercent).Take(5).ToList();
        var lowestMargin = marginRows.OrderBy(m => m.MarginPercent).Take(5).ToList();

        var ingredientBreakdown = allLines
            .GroupBy(l => l.IngredientName)
            .Select(g => new IngredientCostShareDto { Name = g.Key, TotalCost = g.Sum(x => x.LineCost) })
            .OrderByDescending(x => x.TotalCost)
            .Take(8)
            .ToList();
        var breakdownTotal = ingredientBreakdown.Sum(x => x.TotalCost);
        foreach (var row in ingredientBreakdown)
            row.SharePercent = breakdownTotal > 0 ? row.TotalCost / breakdownTotal * 100m : 0m;

        var avgCost = costByRecipe.Count > 0 ? costByRecipe.Values.Average() : 0m;
        var avgMargin = marginByProduct.Count > 0 ? marginByProduct.Values.Average() : 0m;

        var targetMargin = await GetTargetMarginAsync(cancellationToken);

        var alerts = new List<MilkBarAlertDto>();

        foreach (var p in preparedProducts.Where(p => p.IsActive && !activeByProduct.ContainsKey(p.Id)))
            alerts.Add(new MilkBarAlertDto { Type = "Missing recipe", Message = $"{p.Name} has no active recipe.", Severity = MilkBarAlertSeverity.Warning });

        foreach (var recipe in activeRecipes)
        {
            var avail = await recipes.GetAvailabilityAsync(recipe.Id, cancellationToken);
            if (!avail.AllAvailable)
                alerts.Add(new MilkBarAlertDto { Type = "Insufficient ingredients", Message = $"{recipe.Name} is missing required ingredients.", Severity = MilkBarAlertSeverity.Critical });
        }

        foreach (var p in lowStockIngredients)
        {
            if (!stockMap.TryGetValue(p.Id, out var s) || s.Sellable > p.ReorderLevel) continue;
            alerts.Add(new MilkBarAlertDto
            {
                Type = "Low ingredient stock",
                Message = $"{p.Name}: {s.Sellable:0.###} remaining (reorder at {p.ReorderLevel:0.###}).",
                Severity = MilkBarAlertSeverity.Warning
            });
        }

        foreach (var m in marginRows.Where(m => m.MarginPercent < targetMargin))
            alerts.Add(new MilkBarAlertDto
            {
                Type = "Below target margin",
                Message = $"{m.Name}: {m.MarginPercent:0.#}% margin is below the {targetMargin * 100m:0.#}% target.",
                Severity = MilkBarAlertSeverity.Warning
            });

        return new MilkBarOverviewDto
        {
            TotalMenuItems = preparedProducts.Count(p => p.IsActive),
            ActiveRecipes = activeRecipes.Count,
            TotalIngredientsUsed = ingredientIds.Count,
            LowStockIngredients = lowStockCount,
            AverageRecipeCost = avgCost,
            AverageMarginPercent = avgMargin,
            MenuByCategory = menuByCategory,
            HighestCostRecipes = highestCost,
            HighestMarginProducts = highestMargin,
            LowestMarginProducts = lowestMargin,
            IngredientCostBreakdown = ingredientBreakdown,
            Alerts = alerts
        };
    }

    public async Task<PagedResult<MenuItemDto>> GetMenuItemsAsync(MenuFilter filter, CancellationToken cancellationToken = default)
    {
        filter ??= new MenuFilter();

        var query = db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.Type == ProductType.PreparedItem);

        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{EscapeLike(search)}%";
            query = query.Where(p => EF.Functions.Like(p.Name, like) || EF.Functions.Like(p.Sku, like));
        }
        if (filter.CategoryId is { } cat) query = query.Where(p => p.CategoryId == cat);
        if (filter.IsActive is { } active) query = query.Where(p => p.IsActive == active);

        var products = await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
        if (products.Count == 0)
            return new PagedResult<MenuItemDto> { Items = new List<MenuItemDto>(), TotalCount = 0, Page = 1, PageSize = filter.PageSize };

        var productIds = products.Select(p => p.Id).ToList();
        var activeRecipes = await db.Recipes.AsNoTracking()
            .Include(r => r.MenuProduct)
            .Include(r => r.Items).ThenInclude(i => i.IngredientProduct)
            .Where(r => r.IsActive && productIds.Contains(r.MenuProductId))
            .ToListAsync(cancellationToken);
        var byProduct = activeRecipes.GroupBy(r => r.MenuProductId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Version).First());

        var rows = new List<MenuItemDto>();
        foreach (var p in products)
        {
            byProduct.TryGetValue(p.Id, out var recipe);
            decimal cost = 0m;
            var allAvailable = true;
            if (recipe != null)
            {
                var yield = recipe.YieldQuantity <= 0 ? 1 : recipe.YieldQuantity;
                var c = await recipes.CalculateCostAsync(recipe.Id, yield, cancellationToken);
                cost = c.CostPerServing;
                allAvailable = c.AllIngredientsAvailable;
            }

            var profit = p.SellingPrice - cost;
            var margin = p.SellingPrice > 0 ? profit / p.SellingPrice * 100m : 0m;

            rows.Add(new MenuItemDto
            {
                ProductId = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                CategoryId = p.CategoryId,
                CategoryName = p.Category?.Name ?? "Uncategorised",
                SellingPrice = p.SellingPrice,
                IsActive = p.IsActive,
                RecipeId = recipe?.Id,
                HasActiveRecipe = recipe != null,
                EstimatedCost = cost,
                EstimatedProfit = recipe != null ? profit : 0m,
                EstimatedMarginPercent = recipe != null ? margin : 0m,
                AllIngredientsAvailable = allAvailable
            });
        }

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 200 ? 20 : filter.PageSize;
        return new PagedResult<MenuItemDto>
        {
            Items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = rows.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<IngredientDto>> GetIngredientsAsync(IngredientFilter filter, CancellationToken cancellationToken = default)
    {
        filter ??= new IngredientFilter();

        var ingredientIds = await db.RecipeItems.AsNoTracking()
            .Select(i => i.IngredientProductId).Distinct().ToListAsync(cancellationToken);

        var query = db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => ingredientIds.Contains(p.Id));

        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{EscapeLike(search)}%";
            query = query.Where(p => EF.Functions.Like(p.Name, like) || EF.Functions.Like(p.Sku, like));
        }
        if (filter.CategoryId is { } cat) query = query.Where(p => p.CategoryId == cat);
        if (filter.IsActive is { } active) query = query.Where(p => p.IsActive == active);

        var products = await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
        if (products.Count == 0)
            return new PagedResult<IngredientDto> { Items = new List<IngredientDto>(), TotalCount = 0, Page = 1, PageSize = filter.PageSize };

        var ids = products.Select(p => p.Id).ToList();
        var stockMap = await LoadIngredientStockAsync(ids, cancellationToken);

        var usage = await db.RecipeItems.AsNoTracking()
            .Where(i => ids.Contains(i.IngredientProductId) && i.Recipe!.IsActive)
            .Select(i => i.IngredientProductId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var usageCounts = await db.RecipeItems.AsNoTracking()
            .Where(i => ids.Contains(i.IngredientProductId) && i.Recipe!.IsActive)
            .GroupBy(i => i.IngredientProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Select(x => x.RecipeId).Distinct().Count() })
            .ToListAsync(cancellationToken);
        var usageMap = usageCounts.ToDictionary(x => x.ProductId, x => x.Count);

        var rows = products.Select(p =>
        {
            var s = stockMap.TryGetValue(p.Id, out var found) ? found : new StockInfo(0m, p.CostPrice);
            var status = !p.TrackInventory ? StockStatus.InStock
                : s.Sellable <= 0 ? StockStatus.OutOfStock
                : s.Sellable <= p.ReorderLevel ? StockStatus.LowStock
                : StockStatus.InStock;
            return new IngredientDto
            {
                ProductId = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                CategoryId = p.CategoryId,
                CategoryName = p.Category?.Name ?? "Uncategorised",
                Unit = p.Unit,
                SellableStock = p.TrackInventory ? s.Sellable : decimal.MaxValue,
                ReorderLevel = p.ReorderLevel,
                UnitCost = s.UnitCost,
                ActiveRecipeCount = usageMap.GetValueOrDefault(p.Id),
                IsActive = p.IsActive,
                Status = status
            };
        }).AsEnumerable();

        if (filter.Stock is { } stock)
            rows = rows.Where(r => r.Status == stock);

        var list = rows.OrderBy(r => r.Name).ToList();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 200 ? 20 : filter.PageSize;
        return new PagedResult<IngredientDto>
        {
            Items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = list.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    // ===== helpers =====

    private sealed record StockInfo(decimal Sellable, decimal UnitCost);

    private async Task<Dictionary<int, StockInfo>> LoadIngredientStockAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, StockInfo>();
        if (productIds.Count == 0) return result;

        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.CostPrice, p.TrackInventory })
            .ToListAsync(cancellationToken);

        var batches = await db.InventoryBatches.AsNoTracking()
            .Where(b => productIds.Contains(b.ProductId) && b.IsActive && b.QuantityRemaining > 0)
            .Select(b => new { b.ProductId, b.QuantityRemaining, b.UnitCost, b.ExpiryDate })
            .ToListAsync(cancellationToken);

        var today = DateTime.Today;
        foreach (var p in products)
        {
            var list = batches.Where(b => b.ProductId == p.Id).ToList();
            var sellable = list.Where(b => b.ExpiryDate is null || b.ExpiryDate.Value.Date >= today).Sum(b => b.QuantityRemaining);
            var totalQty = list.Sum(b => b.QuantityRemaining);
            var unitCost = totalQty > 0 ? list.Sum(b => b.QuantityRemaining * b.UnitCost) / totalQty : p.CostPrice;
            result[p.Id] = new StockInfo(p.TrackInventory ? sellable : decimal.MaxValue, unitCost);
        }
        return result;
    }

    private async Task<decimal> GetTargetMarginAsync(CancellationToken cancellationToken)
    {
        var raw = await db.AppSettings.AsNoTracking()
            .Where(s => s.Key == "pricing.target_margin")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);
        return decimal.TryParse(raw, out var v) && v >= 0 && v < 1 ? v : 0.4m;
    }

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}