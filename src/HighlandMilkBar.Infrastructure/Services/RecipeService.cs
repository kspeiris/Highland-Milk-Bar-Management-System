using HighlandMilkBar.Application.Common;
using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

/// <summary>
/// Recipe management, estimated costing, ingredient availability and pricing recommendations.
///
/// Costing policy: the estimated recipe cost uses the ingredient's current unit cost, which is the
/// quantity-weighted average unit cost of its active, non-expired batches; when a product has no
/// batches it falls back to <see cref="Product.CostPrice"/>. Selling prices are never used as
/// ingredient costs. This estimate is separate from the actual COGS captured on a completed sale
/// (which is snapshotted on the sale item at sale time and never rewritten by recipe edits).
/// </summary>
public sealed class RecipeService(AppDbContext db) : IRecipeService
{
    private sealed record UnitInfo(string Symbol, UnitKind Kind);
    private sealed record BatchLite(decimal Remaining, decimal UnitCost, DateTime? Expiry);
    private sealed record CostResult(decimal Total, decimal CostPerServing, List<RecipeCostLine> Lines, bool AllAvailable);
    private sealed record AvailResult(decimal MaxServings, bool IsUnlimited, bool AllAvailable, List<IngredientAvailabilityLine> Lines);

    // ===== queries =====

    public async Task<PagedResult<RecipeSummaryDto>> SearchAsync(RecipeFilter filter, CancellationToken cancellationToken = default)
    {
        filter ??= new RecipeFilter();
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize is < 1 or > 200 ? 20 : filter.PageSize;

        var recipes = await db.Recipes.AsNoTracking()
            .Include(r => r.MenuProduct!).ThenInclude(p => p.Category)
            .Include(r => r.Items).ThenInclude(i => i.IngredientProduct)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var units = await LoadUnitMapsAsync(cancellationToken);
        var batchMap = await LoadBatchMapAsync(recipes.SelectMany(r => r.Items).Select(i => i.IngredientProductId).Distinct().ToList(), cancellationToken);

        var today = DateTime.Today;
        IEnumerable<(Recipe Recipe, RecipeSummaryDto Summary)> rows = recipes.Select(r =>
        {
            var cost = BuildCost(r, units, batchMap, r.YieldQuantity <= 0 ? 1 : r.YieldQuantity, today);
            return (r, MapSummary(r, cost));
        });

        var search = filter.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search;
            rows = rows.Where(x => x.Recipe.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                || (x.Recipe.MenuProduct?.Name?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        if (filter.MenuCategoryId is { } cat)
            rows = rows.Where(x => x.Recipe.MenuProduct?.CategoryId == cat);
        if (filter.IsActive is { } active)
            rows = rows.Where(x => x.Recipe.IsActive == active);

        var list = rows.OrderBy(x => x.Recipe.Name).ToList();
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.Summary).ToList();

        return new PagedResult<RecipeSummaryDto>
        {
            Items = items,
            TotalCount = list.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<RecipeDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var recipe = await LoadRecipeAsync(id, cancellationToken);
        if (recipe is null) return null;

        var units = await LoadUnitMapsAsync(cancellationToken);
        var batchMap = await LoadBatchMapAsync(recipe.Items.Select(i => i.IngredientProductId).Distinct().ToList(), cancellationToken);
        var today = DateTime.Today;

        var yield = recipe.YieldQuantity <= 0 ? 1 : recipe.YieldQuantity;
        var cost = BuildCost(recipe, units, batchMap, yield, today);
        var avail = BuildAvailability(recipe, units, batchMap, today);

        var sellingPrice = recipe.MenuProduct?.SellingPrice ?? 0m;
        var grossProfit = sellingPrice - cost.CostPerServing;
        var margin = sellingPrice > 0 ? grossProfit / sellingPrice * 100m : 0m;

        var detailItems = recipe.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i =>
        {
            var line = cost.Lines.FirstOrDefault(l => l.IngredientProductId == i.IngredientProductId);
            return new RecipeDetailItemDto
            {
                IngredientProductId = i.IngredientProductId,
                IngredientName = i.IngredientProduct?.Name ?? "Unknown ingredient",
                Sku = i.IngredientProduct?.Sku ?? string.Empty,
                QuantityRequired = i.QuantityRequired,
                Unit = i.Unit,
                UnitCost = line?.UnitCost ?? 0m,
                LineCost = line?.LineCost ?? 0m,
                AvailableStock = line?.AvailableStock ?? 0m,
                StockUnit = line?.Unit ?? i.Unit,
                InStock = line?.InStock ?? false
            };
        }).ToList();

        return new RecipeDetailDto
        {
            Id = recipe.Id,
            Name = recipe.Name,
            MenuProductId = recipe.MenuProductId,
            MenuProductName = recipe.MenuProduct?.Name ?? string.Empty,
            MenuCategoryId = recipe.MenuProduct?.CategoryId,
            CategoryName = recipe.MenuProduct?.Category?.Name ?? "Uncategorised",
            YieldQuantity = recipe.YieldQuantity,
            YieldUnit = recipe.YieldUnit,
            Version = recipe.Version,
            IsActive = recipe.IsActive,
            CreatedAt = recipe.CreatedAt,
            SellingPrice = sellingPrice,
            Items = detailItems,
            Cost = new RecipeCostBreakdown
            {
                RecipeId = recipe.Id,
                Servings = yield,
                CostPerServing = cost.CostPerServing,
                TotalCost = cost.Total,
                AllIngredientsAvailable = cost.AllAvailable,
                Lines = cost.Lines,
                SellingPrice = sellingPrice,
                GrossProfit = grossProfit,
                MarginPercent = margin
            },
            Availability = new RecipeAvailabilityDto
            {
                RecipeId = recipe.Id,
                MaxServings = avail.MaxServings,
                IsUnlimited = avail.IsUnlimited,
                AllAvailable = avail.AllAvailable,
                Lines = avail.Lines
            }
        };
    }

    // ===== commands =====

    public async Task<RecipeSummaryDto> CreateAsync(CreateRecipeRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A recipe name is required.", nameof(request));
        if (request.YieldQuantity <= 0) throw new ArgumentException("Recipe yield must be greater than zero.", nameof(request));

        var menuProduct = await db.Products.FirstOrDefaultAsync(p => p.Id == request.MenuProductId, cancellationToken)
            ?? throw new ArgumentException("Select a valid menu product.", nameof(request));

        var units = await LoadUnitsAsync(cancellationToken);
        var normalized = await ValidateLinesAsync(request.Items, units, cancellationToken);

        var recipe = new Recipe
        {
            MenuProductId = menuProduct.Id,
            Name = name,
            YieldQuantity = request.YieldQuantity,
            YieldUnit = string.IsNullOrWhiteSpace(request.YieldUnit) ? "Portion" : request.YieldUnit.Trim(),
            Version = await NextVersionAsync(menuProduct.Id, cancellationToken),
            IsActive = request.IsActive,
            CreatedAt = DateTime.Now
        };
        foreach (var line in normalized)
            recipe.Items.Add(new RecipeItem
            {
                IngredientProductId = line.ProductId,
                QuantityRequired = line.Quantity,
                Unit = line.Unit,
                SortOrder = line.SortOrder
            });

        db.Recipes.Add(recipe);

        if (recipe.IsActive)
            await DeactivateOtherRecipesAsync(menuProduct.Id, recipe.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await ReloadSummaryAsync(recipe.Id, cancellationToken);
    }

    public async Task<RecipeSummaryDto> UpdateAsync(UpdateRecipeRequest request, CancellationToken cancellationToken = default)
    {
        var recipe = await db.Recipes
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("The recipe could not be found.");

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A recipe name is required.", nameof(request));
        if (request.YieldQuantity <= 0) throw new ArgumentException("Recipe yield must be greater than zero.", nameof(request));

        var menuProduct = await db.Products.FirstOrDefaultAsync(p => p.Id == request.MenuProductId, cancellationToken)
            ?? throw new ArgumentException("Select a valid menu product.", nameof(request));

        var units = await LoadUnitsAsync(cancellationToken);
        var normalized = await ValidateLinesAsync(request.Items, units, cancellationToken);

        recipe.MenuProductId = menuProduct.Id;
        recipe.Name = name;
        recipe.YieldQuantity = request.YieldQuantity;
        recipe.YieldUnit = string.IsNullOrWhiteSpace(request.YieldUnit) ? "Portion" : request.YieldUnit.Trim();
        recipe.IsActive = request.IsActive;

        db.RecipeItems.RemoveRange(recipe.Items);
        recipe.Items.Clear();
        foreach (var line in normalized)
            recipe.Items.Add(new RecipeItem
            {
                IngredientProductId = line.ProductId,
                QuantityRequired = line.Quantity,
                Unit = line.Unit,
                SortOrder = line.SortOrder
            });

        if (recipe.IsActive)
            await DeactivateOtherRecipesAsync(menuProduct.Id, recipe.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await ReloadSummaryAsync(recipe.Id, cancellationToken);
    }

    public async Task<RecipeSummaryDto> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("The recipe could not be found.");

        recipe.IsActive = isActive;
        if (isActive)
            await DeactivateOtherRecipesAsync(recipe.MenuProductId, recipe.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await ReloadSummaryAsync(recipe.Id, cancellationToken);
    }

    // ===== costing / availability / pricing =====

    public async Task<RecipeCostBreakdown> CalculateCostAsync(int recipeId, decimal servings, CancellationToken cancellationToken = default)
    {
        if (servings <= 0) throw new ArgumentException("Servings must be greater than zero.", nameof(servings));

        var recipe = await LoadRecipeAsync(recipeId, cancellationToken)
            ?? throw new InvalidOperationException($"Recipe {recipeId} was not found.");

        var units = await LoadUnitMapsAsync(cancellationToken);
        var batchMap = await LoadBatchMapAsync(recipe.Items.Select(i => i.IngredientProductId).Distinct().ToList(), cancellationToken);
        var today = DateTime.Today;

        var cost = BuildCost(recipe, units, batchMap, servings, today);
        var sellingPrice = recipe.MenuProduct?.SellingPrice ?? 0m;
        var grossProfit = sellingPrice - cost.CostPerServing;
        var margin = sellingPrice > 0 ? grossProfit / sellingPrice * 100m : 0m;

        return new RecipeCostBreakdown
        {
            RecipeId = recipe.Id,
            Servings = servings,
            CostPerServing = cost.CostPerServing,
            TotalCost = cost.Total,
            AllIngredientsAvailable = cost.AllAvailable,
            Lines = cost.Lines,
            SellingPrice = sellingPrice,
            GrossProfit = grossProfit,
            MarginPercent = margin
        };
    }

    public async Task<RecipeAvailabilityDto> GetAvailabilityAsync(int recipeId, CancellationToken cancellationToken = default)
    {
        var recipe = await LoadRecipeAsync(recipeId, cancellationToken)
            ?? throw new InvalidOperationException($"Recipe {recipeId} was not found.");

        var units = await LoadUnitMapsAsync(cancellationToken);
        var batchMap = await LoadBatchMapAsync(recipe.Items.Select(i => i.IngredientProductId).Distinct().ToList(), cancellationToken);

        var avail = BuildAvailability(recipe, units, batchMap, DateTime.Today);
        return new RecipeAvailabilityDto
        {
            RecipeId = recipe.Id,
            MaxServings = avail.MaxServings,
            IsUnlimited = avail.IsUnlimited,
            AllAvailable = avail.AllAvailable,
            Lines = avail.Lines
        };
    }

    public async Task<PricingRecommendationDto> CalculatePricingAsync(
        int recipeId, decimal targetMarginPercent, decimal roundingIncrement, CancellationToken cancellationToken = default)
    {
        if (targetMarginPercent < 0) throw new ArgumentException("Target margin cannot be negative.", nameof(targetMarginPercent));
        if (targetMarginPercent >= 100) throw new ArgumentException("Target margin must be less than 100%.", nameof(targetMarginPercent));
        if (roundingIncrement < 0) throw new ArgumentException("Rounding increment cannot be negative.", nameof(roundingIncrement));

        var recipe = await LoadRecipeAsync(recipeId, cancellationToken)
            ?? throw new InvalidOperationException($"Recipe {recipeId} was not found.");

        var yield = recipe.YieldQuantity <= 0 ? 1 : recipe.YieldQuantity;
        var units = await LoadUnitMapsAsync(cancellationToken);
        var batchMap = await LoadBatchMapAsync(recipe.Items.Select(i => i.IngredientProductId).Distinct().ToList(), cancellationToken);

        var cost = BuildCost(recipe, units, batchMap, yield, DateTime.Today);
        var costPerServing = cost.CostPerServing;
        var currentPrice = recipe.MenuProduct?.SellingPrice ?? 0m;

        var target = targetMarginPercent / 100m;
        var recommended = target >= 1m ? 0m : costPerServing / (1m - target);
        recommended = Math.Round(recommended, 2, MidpointRounding.AwayFromZero);

        var rounded = roundingIncrement > 0
            ? Math.Ceiling(recommended / roundingIncrement) * roundingIncrement
            : recommended;
        rounded = Math.Round(rounded, 2, MidpointRounding.AwayFromZero);

        var currentMargin = currentPrice > 0 ? (currentPrice - costPerServing) / currentPrice * 100m : 0m;
        var recommendedProfit = rounded - costPerServing;
        var meetsTarget = currentPrice > 0 && currentMargin + 0.001m >= targetMarginPercent;

        var text = costPerServing <= 0
            ? "Add ingredient costs to calculate a recommended price."
            : meetsTarget
                ? $"Current price meets the {targetMarginPercent:0.#}% target."
                : $"Recommended price LKR {rounded:0.00} to reach a {targetMarginPercent:0.#}% margin.";

        return new PricingRecommendationDto
        {
            RecipeId = recipe.Id,
            CostPerServing = costPerServing,
            CurrentSellingPrice = currentPrice,
            TargetMarginPercent = targetMarginPercent,
            RecommendedPrice = recommended,
            RecommendedPriceRounded = rounded,
            CurrentMarginPercent = currentMargin,
            RecommendedProfit = recommendedProfit,
            RoundingIncrement = roundingIncrement,
            MeetsTarget = meetsTarget,
            RecommendationText = text
        };
    }

    // ===== helpers =====

    private async Task<Recipe?> LoadRecipeAsync(int id, CancellationToken cancellationToken)
    {
        return await db.Recipes.AsNoTracking()
            .Include(r => r.MenuProduct!).ThenInclude(p => p.Category)
            .Include(r => r.Items).ThenInclude(i => i.IngredientProduct)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    private async Task<RecipeSummaryDto> ReloadSummaryAsync(int id, CancellationToken cancellationToken)
    {
        var recipe = await LoadRecipeAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("The recipe could not be found after saving.");
        var units = await LoadUnitMapsAsync(cancellationToken);
        var batchMap = await LoadBatchMapAsync(recipe.Items.Select(i => i.IngredientProductId).Distinct().ToList(), cancellationToken);
        var yield = recipe.YieldQuantity <= 0 ? 1 : recipe.YieldQuantity;
        var cost = BuildCost(recipe, units, batchMap, yield, DateTime.Today);
        return MapSummary(recipe, cost);
    }

    private static RecipeSummaryDto MapSummary(Recipe recipe, CostResult cost)
    {
        var sellingPrice = recipe.MenuProduct?.SellingPrice ?? 0m;
        var profit = sellingPrice - cost.CostPerServing;
        var margin = sellingPrice > 0 ? profit / sellingPrice * 100m : 0m;

        return new RecipeSummaryDto
        {
            Id = recipe.Id,
            Name = recipe.Name,
            MenuProductId = recipe.MenuProductId,
            MenuProductName = recipe.MenuProduct?.Name ?? string.Empty,
            CategoryName = recipe.MenuProduct?.Category?.Name ?? "Uncategorised",
            YieldQuantity = recipe.YieldQuantity,
            YieldUnit = recipe.YieldUnit,
            IsActive = recipe.IsActive,
            ItemCount = recipe.Items.Count,
            EstimatedCostPerServing = cost.CostPerServing,
            SellingPrice = sellingPrice,
            EstimatedGrossProfit = profit,
            EstimatedMarginPercent = margin,
            AllIngredientsAvailable = cost.AllAvailable,
            AvailabilityText = cost.AllAvailable ? "Ingredients available" : "Insufficient ingredients"
        };
    }

    private CostResult BuildCost(Recipe recipe, UnitMap units, BatchMap batches, decimal servings, DateTime today)
    {
        var yield = recipe.YieldQuantity <= 0 ? 1 : recipe.YieldQuantity;
        var factor = servings / yield;
        var lines = new List<RecipeCostLine>();
        var total = 0m;
        var allAvailable = true;

        foreach (var item in recipe.Items)
        {
            var ingredient = item.IngredientProduct;
            var stockSymbol = ResolveStockSymbol(ingredient, units);
            var recipeQty = item.QuantityRequired * factor;

            var converted = recipeQty;
            var compatible = UnitConversion.IsCompatible(item.Unit, stockSymbol);
            if (!compatible)
            {
                allAvailable = false;
                converted = recipeQty;
            }
            else
            {
                UnitConversion.TryConvert(item.Unit, stockSymbol, recipeQty, out converted, out _);
            }

            var (unitCost, sellable) = IngredientCostAndStock(ingredient, batches, today);
            var lineCost = converted * unitCost;
            var available = ingredient is null ? false
                : !ingredient.TrackInventory || sellable + 0.0001m >= converted;

            if (!available) allAvailable = false;
            total += lineCost;

            lines.Add(new RecipeCostLine
            {
                IngredientProductId = item.IngredientProductId,
                IngredientName = ingredient?.Name ?? "Unknown ingredient",
                Quantity = converted,
                Unit = stockSymbol,
                UnitCost = unitCost,
                LineCost = lineCost,
                InStock = available,
                RecipeQuantity = recipeQty,
                RecipeUnit = item.Unit,
                AvailableStock = sellable
            });
        }

        var costPerServing = servings > 0 ? total / servings : 0m;
        foreach (var l in lines)
            l.SharePercent = total > 0 ? l.LineCost / total * 100m : 0m;

        return new CostResult(total, costPerServing, lines, allAvailable);
    }

    private AvailResult BuildAvailability(Recipe recipe, UnitMap units, BatchMap batches, DateTime today)
    {
        var yield = recipe.YieldQuantity <= 0 ? 1 : recipe.YieldQuantity;
        var lines = new List<IngredientAvailabilityLine>();
        decimal? maxServings = null;
        var allAvailable = true;

        foreach (var item in recipe.Items)
        {
            var ingredient = item.IngredientProduct;
            var stockSymbol = ResolveStockSymbol(ingredient, units);
            var (_, sellable) = IngredientCostAndStock(ingredient, batches, today);

            var perServingRecipe = yield > 0 ? item.QuantityRequired / yield : item.QuantityRequired;
            var perServing = perServingRecipe;
            var requiredTotal = item.QuantityRequired;

            if (ingredient is not null && UnitConversion.IsCompatible(item.Unit, stockSymbol))
            {
                UnitConversion.TryConvert(item.Unit, stockSymbol, perServingRecipe, out perServing, out _);
                UnitConversion.TryConvert(item.Unit, stockSymbol, requiredTotal, out requiredTotal, out _);
            }
            else if (ingredient is not null)
            {
                allAvailable = false;
            }

            if (ingredient is null || !ingredient.TrackInventory)
            {
                lines.Add(new IngredientAvailabilityLine
                {
                    IngredientProductId = item.IngredientProductId,
                    IngredientName = ingredient?.Name ?? "Unknown ingredient",
                    RequiredPerServing = perServing,
                    RequiredTotal = requiredTotal,
                    AvailableStock = sellable,
                    Unit = stockSymbol,
                    Shortage = 0m,
                    IsAvailable = true,
                    MaxServings = int.MaxValue,
                    IsUnlimited = true
                });
                continue;
            }

            var available = sellable + 0.0001m >= requiredTotal;
            if (!available) allAvailable = false;

            var lineMax = perServing > 0 ? Math.Floor(sellable / perServing) : int.MaxValue;
            if (lineMax < 0) lineMax = 0;
            if (maxServings is null || lineMax < maxServings) maxServings = lineMax;

            lines.Add(new IngredientAvailabilityLine
            {
                IngredientProductId = item.IngredientProductId,
                IngredientName = ingredient.Name,
                RequiredPerServing = perServing,
                RequiredTotal = requiredTotal,
                AvailableStock = sellable,
                Unit = stockSymbol,
                Shortage = Math.Max(0m, requiredTotal - sellable),
                IsAvailable = available,
                MaxServings = lineMax,
                IsUnlimited = false
            });
        }

        var isUnlimited = maxServings is null;
        var max = isUnlimited ? int.MaxValue : (int)maxServings!.Value;

        return new AvailResult(max, isUnlimited, allAvailable, lines);
    }

    private static (decimal UnitCost, decimal SellableStock) IngredientCostAndStock(Product? product, BatchMap batches, DateTime today)
    {
        if (product is null) return (0m, 0m);
        if (!product.TrackInventory) return (product.CostPrice, decimal.MaxValue);

        if (!batches.Batches.TryGetValue(product.Id, out var list) || list.Count == 0)
            return (product.CostPrice, 0m);

        var sellable = list.Where(b => b.Expiry is null || b.Expiry.Value.Date >= today).ToList();
        if (sellable.Count == 0) sellable = list; // fall back for costing when all batches expired

        var totalQty = sellable.Sum(b => b.Remaining);
        var unitCost = totalQty > 0
            ? sellable.Sum(b => b.Remaining * b.UnitCost) / totalQty
            : product.CostPrice;

        var sellableStock = list.Where(b => b.Expiry is null || b.Expiry.Value.Date >= today).Sum(b => b.Remaining);
        return (unitCost, sellableStock);
    }

    private static string ResolveStockSymbol(Product? product, UnitMap units)
    {
        if (product is null) return "Unit";
        if (product.BaseUnitId is { } baseId && units.ById.TryGetValue(baseId, out var byId))
            return byId.Symbol;

        var norm = UnitConversion.Normalize(product.Unit);
        if (units.ByNameOrSymbol.TryGetValue(norm, out var byName))
            return byName.Symbol;

        return string.IsNullOrWhiteSpace(product.Unit) ? "Unit" : product.Unit;
    }

    private async Task<UnitMap> LoadUnitMapsAsync(CancellationToken cancellationToken)
    {
        var units = await db.Units.AsNoTracking().ToListAsync(cancellationToken);
        var map = new UnitMap();
        foreach (var u in units)
        {
            var info = new UnitInfo(u.Symbol, u.Kind);
            map.ById[u.Id] = info;
            map.ByNameOrSymbol[UnitConversion.Normalize(u.Name)] = info;
            map.ByNameOrSymbol[UnitConversion.Normalize(u.Symbol)] = info;
        }
        return map;
    }

    private Task<UnitMap> LoadUnitsAsync(CancellationToken cancellationToken) => LoadUnitMapsAsync(cancellationToken);

    private async Task<BatchMap> LoadBatchMapAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken)
    {
        var map = new BatchMap();
        if (productIds.Count == 0) return map;

        var batches = await db.InventoryBatches.AsNoTracking()
            .Where(b => productIds.Contains(b.ProductId) && b.IsActive && b.QuantityRemaining > 0)
            .Select(b => new { b.ProductId, b.QuantityRemaining, b.UnitCost, b.ExpiryDate })
            .ToListAsync(cancellationToken);

        foreach (var b in batches)
        {
            if (!map.Batches.TryGetValue(b.ProductId, out var list))
            {
                list = new List<BatchLite>();
                map.Batches[b.ProductId] = list;
            }
            list.Add(new BatchLite(b.QuantityRemaining, b.UnitCost, b.ExpiryDate));
        }
        return map;
    }

    private async Task<int> NextVersionAsync(int menuProductId, CancellationToken cancellationToken)
    {
        var max = await db.Recipes.Where(r => r.MenuProductId == menuProductId)
            .MaxAsync(r => (int?)r.Version, cancellationToken);
        return (max ?? 0) + 1;
    }

    private async Task DeactivateOtherRecipesAsync(int menuProductId, int keepRecipeId, CancellationToken cancellationToken)
    {
        var others = await db.Recipes
            .Where(r => r.MenuProductId == menuProductId && r.Id != keepRecipeId && r.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var r in others) r.IsActive = false;
    }

    private sealed record NormalizedLine(int ProductId, decimal Quantity, string Unit, int SortOrder);

    private async Task<List<NormalizedLine>> ValidateLinesAsync(
        IReadOnlyList<RecipeLineRequest> lines, UnitMap units, CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
            throw new ArgumentException("A recipe must contain at least one ingredient.", nameof(lines));

        var productIds = lines.Select(l => l.IngredientProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var result = new List<NormalizedLine>();
        var seen = new HashSet<int>();
        var order = 0;

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException("Ingredient quantities must be greater than zero.", nameof(lines));
            if (!products.TryGetValue(line.IngredientProductId, out var product) || product is null)
                throw new ArgumentException("Every ingredient must reference an existing product.", nameof(lines));
            if (!product.IsActive)
                throw new ArgumentException($"Ingredient '{product.Name}' is inactive and cannot be used.", nameof(lines));
            if (!product.TrackInventory)
                throw new ArgumentException($"Ingredient '{product.Name}' is not inventory-tracked and cannot be used in a recipe.", nameof(lines));
            if (!seen.Add(product.Id))
                throw new ArgumentException($"Ingredient '{product.Name}' is added more than once.", nameof(lines));

            var stockSymbol = ResolveStockSymbol(product, units);
            if (!UnitConversion.IsCompatible(line.Unit, stockSymbol))
                throw new ArgumentException(
                    $"Unit '{line.Unit}' is not compatible with the stock unit '{stockSymbol}' of '{product.Name}'.", nameof(lines));

            result.Add(new NormalizedLine(product.Id, line.Quantity, string.IsNullOrWhiteSpace(line.Unit) ? stockSymbol : line.Unit.Trim(), order++));
        }

        return result;
    }

    private sealed class UnitMap
    {
        public Dictionary<int, UnitInfo> ById { get; } = new();
        public Dictionary<string, UnitInfo> ByNameOrSymbol { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class BatchMap
    {
        public Dictionary<int, List<BatchLite>> Batches { get; } = new();
    }
}