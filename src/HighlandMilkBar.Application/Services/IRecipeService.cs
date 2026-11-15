using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Recipe management and costing. The single source of truth for recipe data and estimated
/// recipe cost (distinct from the actual COGS captured on a completed sale).
/// </summary>
public interface IRecipeService
{
    Task<PagedResult<RecipeSummaryDto>> SearchAsync(RecipeFilter filter, CancellationToken cancellationToken = default);

    Task<RecipeDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<RecipeSummaryDto> CreateAsync(CreateRecipeRequest request, CancellationToken cancellationToken = default);

    Task<RecipeSummaryDto> UpdateAsync(UpdateRecipeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Activates or deactivates a recipe. Activating deactivates other recipes for the same menu product.</summary>
    Task<RecipeSummaryDto> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Estimated cost breakdown for <paramref name="servings"/> servings of a recipe.</summary>
    Task<RecipeCostBreakdown> CalculateCostAsync(int recipeId, decimal servings, CancellationToken cancellationToken = default);

    /// <summary>Ingredient availability and maximum producible servings for one recipe yield.</summary>
    Task<RecipeAvailabilityDto> GetAvailabilityAsync(int recipeId, CancellationToken cancellationToken = default);

    /// <summary>Recommends a selling price for a target gross margin (never applies it).</summary>
    Task<PricingRecommendationDto> CalculatePricingAsync(
        int recipeId, decimal targetMarginPercent, decimal roundingIncrement, CancellationToken cancellationToken = default);
}