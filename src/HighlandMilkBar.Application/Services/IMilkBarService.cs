using HighlandMilkBar.Application.DTOs;

namespace HighlandMilkBar.Application.Services;

/// <summary>
/// Read-only queries that back the Milk Bar module: the operational dashboard, the menu-item
/// list and the ingredient list. Commands (recipe CRUD) live in <see cref="IRecipeService"/>.
/// </summary>
public interface IMilkBarService
{
    Task<MilkBarOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<MenuItemDto>> GetMenuItemsAsync(MenuFilter filter, CancellationToken cancellationToken = default);

    Task<PagedResult<IngredientDto>> GetIngredientsAsync(IngredientFilter filter, CancellationToken cancellationToken = default);
}