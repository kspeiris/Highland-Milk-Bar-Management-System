# Ingredient Availability

Ingredient availability answers "can we make this menu item right now, and how many?" using **sellable
stock** — active, non-expired batch quantity — not physical stock that includes expired goods.

## Availability rules

- Only **sellable** stock counts: `InventoryBatch` rows that are active, have `QuantityRemaining > 0`
  and are not expired (`ExpiryDate` null or `>= today`).
- Recipe quantities are normalised into the ingredient's stock unit before comparison.
- Non-inventory-tracked ingredients (if any are referenced) are treated as unlimited and do not
  constrain production.
- REC-12: availability uses sellable stock. REC-13: expired ingredients cannot be used.

## Calculations

```
Required per serving = recipe quantity per yield ÷ recipe yield
Required for a batch = recipe quantity for one yield
Possible servings (this ingredient) = floor(available sellable stock ÷ required per serving)
Maximum servings (recipe)           = minimum across all constraining ingredients
Shortage (this ingredient)          = max(0, required for a batch − available sellable stock)
```

`Maximum servings` is the largest whole number of servings that can be produced given the tightest
ingredient. When no ingredient constrains production, the result is reported as **unlimited**.

## Worked example — Chocolate Milkshake (yield 1)

| Ingredient | Required | Available | Servings possible |
| --- | ---: | ---: | ---: |
| Fresh Milk | 200 ml | 5,000 ml | 25 |
| Ice Cream | 2 scoop | 30 scoop | 15 |
| Chocolate Syrup | 20 ml | 100 ml | 5 |
| Sugar | 10 g | 500 g | 50 |
| Milkshake Cup | 1 pcs | 100 pcs | 100 |

Maximum producible = **5 servings** (Chocolate Syrup is the limiting ingredient). If only 10 ml of
syrup remained, the recipe would show a shortage of 10 ml and 0 producible servings.

## Important properties

- Availability is **informational only** — it never deducts or reserves stock.
- It uses sellable stock (expired batches are excluded), respecting FEFO eligibility.
- The POS **always revalidates** inventory at checkout; availability shown earlier can change.

The recipe details window lists each ingredient's required-per-serving, available stock, unit,
shortage and availability status, plus the overall maximum servings.

Automated tests cover limiting ingredient, expired-batch exclusion, zero stock, non-tracked
ingredients and the unlimited case (`RecipeAvailabilityTests`).