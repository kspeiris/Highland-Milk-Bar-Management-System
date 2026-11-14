# Component Library

Reusable WPF controls, styles and converters for the Highland Milk Bar UI. Everything lives under `src/HighlandMilkBar.WPF/` and is merged into `App.xaml`, so all keys are available application-wide without local includes.

## Resource dictionary layout

`App.xaml` merges, in order:

1. Themes — `Resources/Themes/Colors.xaml`, `Typography.xaml`, `Spacing.xaml`, `Shadows.xaml`
2. Styles — `Resources/Styles/ButtonStyles.xaml`, `TextBoxStyles.xaml`, `ComboBoxStyles.xaml`, `DataGridStyles.xaml`, `CardStyles.xaml`, `NavigationStyles.xaml`, `BadgeStyles.xaml`, `WindowStyles.xaml`

Converters registered in `App.xaml`:

| Key | Converter | Purpose |
| --- | --- | --- |
| `BoolToVis` | `BooleanToVisibilityConverter` (built-in) | `true` → Visible |
| `InverseBoolToVis` | `InverseBooleanToVisibilityConverter` | `false` → Visible |
| `InverseBool` | `InverseBooleanConverter` | Negates a boolean (e.g. enable a control when not busy) |

## Reusable UserControls (`Controls/`)

| Control | Dependency properties | Notes |
| --- | --- | --- |
| `StatCard` | `Title`, `Value`, `Caption`, `Glyph`, `IsDemo` (bool), `Accent` (Brush) | KPI tile. `IsDemo` flags placeholder figures. |
| `PageHeader` | `Title`, `Subtitle`, `HeaderContent` (object) | Page title row; `HeaderContent` hosts right-aligned actions. |
| `SectionHeader` | `Title`, `Subtitle` | Lightweight heading used above groups. |
| `StatusBadge` | `Text`, `BadgeBackground` (Brush), `BadgeForeground` (Brush) | Pill badge with themed colours. |
| `EmptyState` | `Glyph`, `Title`, `Message` | Centred placeholder for empty lists / scaffolds. |
| `AppCard` | `Header` (string), `Body` (object) | Card with optional header and arbitrary body content. |
| `SearchBox` | `Text` (two-way by default) | Rounded search input with leading glyph. |
| `LoadingIndicator` | `IsLoading` (bool) | Shows/hides an indeterminate busy state. |

Example:

```xml
<controls:StatCard Title="TODAY'S REVENUE"
                   Value="{Binding RevenueTodayText}"
                   Caption="Completed sales today"
                   Glyph="LKR"
                   Accent="{StaticResource PrimaryBrush}" />
```

## Styles

### Buttons — `ButtonStyles.xaml`

Base `AppButtonBase` supplies the template (normal / hover / pressed / disabled / keyboard-focus states). Derived keys:

- `PrimaryButton` — filled primary action.
- `SecondaryButton` — light primary tint.
- `OutlineButton` — bordered neutral.
- `GhostButton` — text-only, muted.
- `DangerButton` — destructive action.
- `SuccessButton` — positive/confirm action.
- `IconButton` — square 40×40 glyph button (header, collapse toggle).

### Text inputs — `TextBoxStyles.xaml`

- `InputTextBox` — standard single-line input (label above field pattern).

### Cards — `CardStyles.xaml`

- `AppCard` — elevated surface card (uses `CardShadow`).
- `FlatCard` — same geometry, no shadow (dense/nested layouts).
- `InsetPanel` — recessed panel on the background colour (e.g. chart area).

### Badges — `BadgeStyles.xaml`

Pill-shaped border styles: `StatusBadgeBase`, `StatusBadgeSuccess`, `StatusBadgeWarning`, `StatusBadgeDanger`, `StatusBadgeInfo`, `StatusBadgeNeutral`.

### Data grid — `DataGridStyles.xaml`

- `AppDataGrid` — themed `DataGrid` (header styling, grid line colours, row striping, selection).

### Navigation — `NavigationStyles.xaml`

- `SidebarNavButton` — sidebar item template; selection driven by the item's `IsSelected` (highlight bar + background, white semibold text).

### Window — `WindowStyles.xaml`

- `AppWindow` — `Window` style used by `MainWindow` (background, chrome).

## Legacy compatibility

`PrimaryButton`, `SecondaryButton`, `InputTextBox` and the `CanvasBrush` colour alias are preserved from earlier steps so existing views keep compiling. New code should use the canonical keys documented here.