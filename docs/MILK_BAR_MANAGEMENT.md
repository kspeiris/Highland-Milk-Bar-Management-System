# Milk Bar Management (Step 07)

The Milk Bar module connects Product Management, Ingredient Inventory, Recipe Management, Recipe
Costing and Pricing into one operational workspace. It is built on the existing `Recipe` /
`RecipeItem` / `Product` / `Unit` / `InventoryBatch` model and reuses `RecipeService`,
`InventoryService` and `SalesService` — no duplicate inventory, product or sales systems.

## Module structure

```
Milk Bar  (sidebar)
├── Overview      KPIs, charts and alerts
├── Menu          prepared menu items with recipe cost / margin
├── Recipes       recipe list with create / edit / activate / details
└── Ingredients   ingredient products with sellable stock and recipe usage
```

Plus two dialogs:
- **Recipe editor** — general info + dynamic ingredient rows + live cost summary.
- **Recipe details** — cost breakdown, ingredient availability, max servings and pricing calculator.

## Where each concern lives

| Concern | Type |
| --- | --- |
| Menu / ingredient / overview queries | `IMilkBarService` → `MilkBarService` |
| Recipe CRUD, costing, availability, pricing | `IRecipeService` → `RecipeService` |
| Unit conversion policy | `Application.Common.UnitConversion` + `IUnitConverter` |
| POS ingredient deduction | `SalesService.ConsumeRecipeAsync` |
| ViewModels | `MilkBarViewModel`, `MenuViewModel`, `RecipeListViewModel`, `IngredientListViewModel`, `RecipeEditorViewModel`, `RecipeDetailsViewModel` |
| Views | `MilkBarView` (+ `MilkBarOverviewView`, `MenuView`, `RecipeListView`, `IngredientListView`), `RecipeEditorWindow`, `RecipeDetailsWindow` |

## Menu items are products

A menu item is a `Product` with `Type = PreparedItem`. There is no separate menu database. The menu
list shows: name, SKU, category, selling price, recipe status, estimated cost, estimated profit,
estimated margin and active/inactive status.

- Edit a menu item through the existing Product Management service (`ProductService`).
- Activate/deactivate via the product's `IsActive` flag.
- A prepared item **without an active recipe cannot be sold** (POS rejects it).

## Ingredient relationships

An ingredient is a `Product` with `Type = Ingredient` and `TrackInventory = true`. A recipe line
(`RecipeItem`) references an ingredient product and stores a quantity + unit. Ingredients are never
duplicated into a second table.

## Milk Bar overview (real data)

KPI cards: total menu items, active recipes, ingredients used, low-stock ingredients, average recipe
cost and average estimated margin.

Analytics: menu items by category, highest-cost recipes, highest/lowest margin products and the
ingredient cost breakdown. Alerts cover missing recipes, insufficient ingredients, low stock and
products below the configured target margin (`pricing.target_margin`, default 40%). All values come
from the database.

## Business rules

See `RECIPE_MANAGEMENT.md`, `RECIPE_COSTING.md`, `UNIT_CONVERSION_RULES.md`,
`MENU_PRICING_RULES.md` and `INGREDIENT_AVAILABILITY.md` for the detailed REC-01…REC-15 rules.

## Database changes

**None.** The existing `Recipe` (`YieldQuantity`, `YieldUnit`, `Version`, `IsActive`),
`RecipeItem` (`QuantityRequired`, `Unit`, `SortOrder`) and `Product`/`Unit` model already support
the requirements, so no migration was added. Recipe units are resolved to `Unit` records at runtime
by matching `RecipeItem.Unit` / `Product.Unit` against `Unit.Name`/`Unit.Symbol`.

## POS integration

The existing POS already deducts recipe ingredients atomically at checkout. This step additionally
**normalises recipe units into each ingredient's stock unit** before allocation (falling back to the
previous behaviour for legacy data created outside the validated editor). Inactive recipes are never
used; a prepared item with no active recipe is rejected. Historical sale COGS is snapshotted on the
sale item and is never rewritten by recipe edits.