using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

public interface IRecipeService
{
    Task<RecipeCostBreakdown> CalculateCostAsync(int recipeId, decimal servings, CancellationToken cancellationToken = default);
}