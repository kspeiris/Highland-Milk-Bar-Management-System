# Menu Pricing Rules

The pricing calculator recommends a selling price for a target gross-profit margin. It never changes
a product's price automatically — the user must explicitly approve an update.

## Formula

```
Recommended price = cost per serving ÷ (1 − target gross margin)
```

Rounded to a configurable increment (nearest LKR 1 / 5 / 10). The recommendation uses the **rounded
up** value so the realised margin is never below the target.

## Inputs and validation

- Target margin must be `>= 0` and `< 100%` (a 100% margin is mathematically impossible and is
  rejected).
- Rounding increment must be `>= 0` (0 = no rounding).
- Cost per serving must be valid (a recipe with no cost yields a "add costs" message).

## Displayed metrics

- Recipe cost per serving
- Current selling price
- Target gross margin %
- Recommended selling price (exact and rounded)
- Current margin %
- Estimated gross profit at the recommended price
- Whether the current price already meets the target

## Worked example

```
Cost per serving   = LKR 151.00
Target margin      = 40%
Recommended price  = 151 / (1 − 0.40) = LKR 251.67  → rounded to LKR 251.67
Current price      = LKR 250.00
Current margin     = 39.6%   (below the 40% target → warning)
```

With a rounding increment of 5: `ceil(251.67 / 5) × 5 = LKR 255.00`.

## Approval workflow

- **Calculate** shows the recommendation.
- **Apply recommended price** asks for explicit confirmation, then updates the menu product's
  selling price through the existing `ProductService.UpdateAsync`.
- REC-14: recommendations never overwrite the selling price without confirmation.
- Historical sale prices are preserved (each sale stores the unit price it was sold at).

## Warnings

The Milk Bar dashboard flags menu items whose current margin is below the configured target margin
(`pricing.target_margin`, default 40%). The pricing calculator itself shows whether the current price
meets the target.

Automated tests cover the recommendation formula, the 100%-margin rejection, rounding and the
"does not auto-apply" guarantee (`RecipePricingTests`).