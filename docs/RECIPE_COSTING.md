# Recipe Costing

Recipe costing estimates what it costs to produce a menu item, so pricing and margin decisions are
based on real ingredient costs rather than guesswork.

## Costing policy (single source of truth)

`RecipeService` is the only place that calculates estimated recipe cost. The WPF layer never
duplicates the calculation.

**Ingredient unit cost** = the quantity-weighted average unit cost of the ingredient's active,
non-expired batches (`Σ remaining × unitCost ÷ Σ remaining`). When a product has no batches, it
falls back to the product's recorded `CostPrice`.

Selling prices are **never** used as ingredient costs.

## Formulas

```
Ingredient line cost = normalized ingredient quantity × ingredient unit cost
Total recipe cost    = Σ ingredient line costs
Cost per serving     = total recipe cost ÷ servings
Gross profit         = menu selling price − cost per serving
Gross margin %       = (menu selling price − cost per serving) ÷ menu selling price × 100
```

Quantities are normalised into the ingredient's stock unit before costing (see
`UNIT_CONVERSION_RULES.md`). Zero selling price yields a margin of 0% (no divide-by-zero).

## Worked example — Chocolate Milkshake

| Ingredient | Qty | Unit cost | Line cost |
| --- | ---: | ---: | ---: |
| Fresh Milk | 200 ml | 0.25 /ml | 50.00 |
| Vanilla Ice Cream | 2 scoop | 35.00 /scoop | 70.00 |
| Chocolate Syrup | 20 ml | 0.80 /ml | 16.00 |
| Sugar | 10 g | 0.30 /g | 3.00 |
| Milkshake Cup | 1 pcs | 12.00 /pcs | 12.00 |
| | | **Total** | **151.00** |

Cost per serving (yield 1) = 151.00. At a selling price of 250.00: gross profit = 99.00, margin =
39.6%.

## Estimated cost vs actual COGS

Two distinct numbers:

- **Estimated recipe cost** — computed from current ingredient costs (above). Used for pricing and
  the Milk Bar dashboard.
- **Actual sale COGS** — the cost of the batches actually consumed during a completed POS
  transaction. It is snapshotted on each `SaleItem` (`UnitCostSnapshot`) and on `Sale.CostOfGoodsSold`
  at sale time.

Changing a recipe's quantities or an ingredient's cost later updates the **estimated** cost but
**never** rewrites a completed sale's COGS. This is covered by an integration test
(`CompletedSaleCogs_IsPreservedAfterRecipeEdit`).

## Cost breakdown view

The recipe details window lists, per ingredient: quantity (in stock unit), unit, unit cost, line
cost and share of total cost, plus the total cost, cost per serving, selling price, gross profit and
margin. All values are labelled as estimates.