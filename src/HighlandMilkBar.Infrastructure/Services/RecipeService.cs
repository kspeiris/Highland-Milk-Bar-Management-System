using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Application.Services;
using HighlandMilkBar.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Services;

public sealed class RecipeService(AppDbContext db) : IRecipeService
{
    public async Task<RecipeCostBreakdown> CalculateCostAsync(int recipeId, decimal servings, CancellationToken cancellationToken = default)
    {
        if (servings <= 0)
            throw new ArgumentException("Servings must be greater than zero.", nameof(servings));

        var recipe = await db.Recipes
            .Include(r => r.Items)
                .ThenInclude(i => i.IngredientProduct)
            .FirstOrDefaultAsync(r => r.Id == recipeId, cancellationToken)
            ?? throw new InvalidOperationException($"Recipe {recipeId} was not found.");

        var yield = recipe.YieldQuantity <= 0 ? 1 : recipe.YieldQuantity;
        var factor = servings / yield;

        var lines = new List<RecipeCostLine>();
        var total = 0m;
        var allAvailable = true;

        foreach (var item in recipe.Items)
        {
            var required = item.QuantityRequired * factor;
            var unitCost = item.IngredientProduct?.CostPrice ?? 0m;
            var lineCost = required * unitCost;
            var available = item.IngredientProduct == null
                ? false
                : !item.IngredientProduct.TrackInventory || item.IngredientProduct.CurrentStock >= required;

            if (!available) allAvailable = false;
            total += lineCost;

            lines.Add(new RecipeCostLine
            {
                IngredientProductId = item.IngredientProductId,
                IngredientName = item.IngredientProduct?.Name ?? "Unknown ingredient",
                Quantity = required,
                Unit = item.Unit,
                UnitCost = unitCost,
                LineCost = lineCost,
                InStock = available
            });
        }

        return new RecipeCostBreakdown
        {
            RecipeId = recipe.Id,
            Servings = servings,
            CostPerServing = total / servings,
            TotalCost = total,
            AllIngredientsAvailable = allAvailable,
            Lines = lines
        };
    }
}