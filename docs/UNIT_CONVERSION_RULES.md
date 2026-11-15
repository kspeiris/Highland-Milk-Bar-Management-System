# Unit Conversion Rules

Recipe quantities and inventory quantities must be comparable. The conversion policy is centralised
in `Application.Common.UnitConversion` (pure, testable) and exposed through `IUnitConverter` /
`UnitConverter` for services and ViewModels.

## Canonical symbols and kinds

| Kind | Units | Base unit |
| --- | --- | --- |
| Weight | milligram (mg), gram (g), kilogram (kg) | gram |
| Volume | millilitre (ml), litre (l) | millilitre |

Common synonyms are normalised before comparison: e.g. `Litre`/`liter`/`L`/`ltr` → `l`,
`Millilitre`/`ml` → `ml`, `Gram`/`gm`/`g` → `g`, `Kilogram`/`kilo`/`kg` → `kg`, and
`Piece`/`pc`/`pcs`/`Count`/`Unit` → `pcs`.

## Conversion rules

1. **Standard ↔ standard, same kind** — convert through the base unit.
   - `2000 ml → 2 l`, `2 l → 2000 ml`, `500 g → 0.5 kg`, `2 kg → 2000 g`.
2. **Same symbol** — identity (`2 scoop → 2 scoop`).
3. **Different kinds** — rejected (`g → ml` is not allowed).
4. **Standard ↔ opaque** — rejected (`ml → scoop`).
5. **Opaque ↔ opaque (different names)** — rejected. Count-style units (`scoop`, `cup`, `glass`,
   `bottle`, `pack`, `pot`, `portion`) are **opaque**: they only convert 1:1 with themselves.

## Scoop / cup / piece policy

A "scoop" is **never** assumed to equal a fixed mass or volume across ingredients. Conversions like
`scoop → ml` are not performed automatically; an ingredient-specific factor would be required (not
modelled). This prevents silent mass/volume errors in dairy recipes.

## Where conversion is applied

- **Recipe costing** — the recipe quantity is converted to the ingredient's stock unit before
  multiplying by the unit cost.
- **Ingredient availability** — required quantity and sellable stock are compared in the same unit.
- **POS consumption** — `SalesService.ConsumeRecipeAsync` converts the recipe quantity to the
  ingredient's stock unit before allocating batches (legacy data created outside the validated
  editor falls back to the raw quantity, preserving previous behaviour).

## Validation

`RecipeService` rejects a recipe line whose unit is not compatible with the ingredient's stock unit
(REC-05). The ingredient's stock unit is resolved from `Product.BaseUnit` when set, otherwise by
matching `Product.Unit` against the `Unit` table (`Name`/`Symbol`), otherwise the raw `Product.Unit`.

Automated tests cover standard conversions, incompatible pairs, identity and opaque-unit rules
(`UnitConversionTests`).