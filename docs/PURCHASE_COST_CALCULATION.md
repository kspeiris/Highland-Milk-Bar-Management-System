# Purchase Cost Calculation

How purchase totals and landed inventory costs are computed. The arithmetic lives in `Application/Common/PurchaseFinancials.cs` (pure, side-effect-free) and is used by `PurchaseService`, the WPF editor and the tests so all three agree.

## Line and header totals

```text
Line subtotal   = Quantity × UnitCost
Line total      = max(0, Quantity × UnitCost − LineDiscount)
Subtotal        = Σ line totals
Grand total     = max(0, Subtotal − PurchaseDiscount + AdditionalCharges)
```

Validation:

- Quantity must be > 0 (PUR-04).
- Unit cost ≥ 0 (PUR-05).
- Line discount ≥ 0; purchase discount ≥ 0 and ≤ subtotal; additional charges ≥ 0.
- Grand total can never be negative (floored at 0).

All money is `decimal`, currency **LKR**. No new money type was introduced.

## Landed batch cost (charge allocation)

When a purchase is received, purchase-level **additional charges** and **purchase discount** are spread across lines so inventory carries an accurate landed cost (and RecipeService weighted-average costing stays correct).

```text
weights[i]  = line net value (line total after line discount)
pool        = AdditionalCharges − PurchaseDiscount
allocated[i]= weights[i] / Σweights × pool           (last line absorbs rounding remainder)
landedUnitCost[i] = (weights[i] + allocated[i]) / Quantity[i]   (rounded to 4 dp)
```

- `AllocateProportionally` guarantees the parts sum **exactly** to the pool (the last line absorbs the rounding remainder) — no charges are lost or double-counted.
- The landed unit cost is passed to `IInventoryService.ReceiveAsync`, so the created `InventoryBatch.UnitCost` reflects charges/discounts, not just the raw line cost.
- Worked example: 10 units × 100 + 50 additional charges → landed unit cost = (1000 + 50) / 10 = **105.0000**.

## Preserving historical purchase cost

`PurchaseItem.UnitCost` is the price recorded on the line at purchase time. It is **never** recalculated when a product's cost price later changes (PUR-14). Purchase price history reads `PurchaseItem.UnitCost` from received purchases, so every historical price is preserved.

## Effect on recipe costing

`RecipeService` derives ingredient cost from the quantity-weighted average unit cost of active, non-expired batches. Because received purchases create batches at landed cost, new purchase prices flow into recipe costing automatically — no separate cost pipeline.