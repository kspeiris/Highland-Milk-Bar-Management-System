# Recipe Management

Recipes define what ingredients (and how much of each) make up a prepared menu item. They are the
basis for recipe-based stock deduction in the POS and for estimated recipe costing.

## Entities (reused)

| Entity | Fields used |
| --- | --- |
| `Recipe` | `MenuProductId`, `Name`, `YieldQuantity`, `YieldUnit`, `Version`, `IsActive`, `CreatedAt`, `Items` |
| `RecipeItem` | `RecipeId`, `IngredientProductId`, `QuantityRequired`, `Unit`, `SortOrder` |

No new tables were added. `Recipe.MenuProductId` → `Product` (the menu item); each `RecipeItem`
→ `Product` (the ingredient).

## Lifecycle

1. **Create** — choose a menu product, yield and at least one ingredient line.
2. **Activate / deactivate** — only active recipes are usable by the POS. Activating a recipe
   deactivates other recipes for the same menu product (one active production recipe per product).
3. **Edit** — replace ingredients/quantities. Recipe edits never touch historical sales.
4. **Version** — `Version` increments per menu product; completed sales store the `RecipeId` and a
   cost snapshot, so editing a recipe cannot alter past profitability.

## Operations

- View recipes (search, filter by status/category).
- Create / edit / activate / deactivate.
- View recipe details (ingredients, cost breakdown, availability, pricing).

## Validation rules

| ID | Rule | Enforcement |
| --- | --- | --- |
| REC-01 | Every recipe must be associated with a valid menu product. | `RecipeService.CreateAsync` / `UpdateAsync` reject unknown menu products. |
| REC-02 | A recipe must contain at least one ingredient. | Empty item list is rejected. |
| REC-03 | Ingredients must reference active, inventory-tracked products. | Inactive or non-tracked ingredients are rejected. |
| REC-04 | Ingredient quantities must be positive. | `Quantity <= 0` is rejected. |
| REC-05 | Ingredient units must be compatible with the ingredient's stock unit. | Checked with `UnitConversion.IsCompatible`. |
| REC-11 | One active production recipe per menu product. | Activating a recipe deactivates the others. |
| — | Duplicate ingredient rows are not allowed. | A repeated ingredient product is rejected. |

Duplicate ingredient lines are rejected (no intentional variant support is modelled yet).

## Recipe editor

- Select a menu product (prepared items).
- Add ingredients from the inventory-tracked product list (search by name/SKU).
- Set quantity per line and choose a yield/unit.
- See live total cost and cost per serving.
- Save / update; activate or deactivate via the list.

The editor never deducts stock — stock is only deducted by the POS at sale time.

## Relationship to POS

- `SalesService.ConsumeRecipeAsync` loads the highest-version **active** recipe for a prepared item.
- Recipe quantities are normalised to the ingredient's stock unit and allocated FEFO/FIFO.
- A prepared item with no active recipe, or insufficient ingredients, is rejected and the sale
  transaction rolls back.