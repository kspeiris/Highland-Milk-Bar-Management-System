# UI / UX Design System

## Visual direction

A calm, modern retail-management interface: Highland-inspired deep blue identity, bright white work surfaces, soft cool-grey background, and restrained green/amber/red status colors. The design combines an ERP navigation shell with a fast POS workspace.

## Foundations (design tokens)

All colours are defined once in `src/HighlandMilkBar.WPF/Resources/Themes/Colors.xaml` as `Color` and `SolidColorBrush` resources. Use the brush keys in XAML; never hard-code hex values in views.

### Palette

| Token | Value | Brush key | Usage |
| --- | --- | --- | --- |
| Primary | `#145DA0` | `PrimaryBrush` | Primary buttons, links, active accents |
| Primary dark | `#12304A` | `PrimaryDarkBrush` | Strong emphasis, sidebar brand |
| Primary light | `#EAF3FB` | `PrimaryLightBrush` | Secondary buttons, info badges, icon tiles |
| Accent | `#32A6C7` | `AccentBrush` | Selection indicators, highlights |
| Background | `#F4F7FB` | `BackgroundBrush` | App canvas behind cards |
| Surface | `#FFFFFF` | `SurfaceBrush` | Cards, header, dialogs |
| Text primary | `#172B3A` | `TextPrimaryBrush` | Default text |
| Text secondary | `#647789` | `TextSecondaryBrush` | Captions, labels, hints |
| Success | `#23976A` | `SuccessBrush` | Positive / in-stock / completed |
| Warning | `#E5A132` | `WarningBrush` | Low stock, near-expiry, demo markers |
| Danger | `#D94B4B` | `DangerBrush` | Errors, expired, destructive actions |
| Border | `#E3EAF1` | `BorderBrush` | Dividers, card outlines |

Derived interaction tokens also exist: `PrimaryHoverBrush` (`#11528C`), `PrimaryPressedBrush` (`#0E4677`), `SuccessHoverBrush`, `DangerHoverBrush`, `FocusBrush` (`#32A6C7`).

Sidebar tokens: `SidebarBrush` (`#12304A`), `SidebarHoverBrush` (`#1B3A52`), `SidebarSelectedBrush` (`#1C4C70`), `SidebarTextBrush` (`#C7D6E4`).

### Legacy aliases

To keep existing views compiling, the following legacy brush keys map onto the new palette and should be replaced over time: `BrandBrush` → Primary, `BrandDarkBrush` → Primary dark, `CanvasBrush` → Background, `TextBrush` → Text primary, `MutedBrush` → Text secondary, `BorderBrushSoft` → Border, `GreenBrush` → Success, `AmberBrush` → Warning. Prefer the canonical keys above in new work.

## Typography

`Resources/Themes/Typography.xaml`. Base `TextBlock` is Segoe UI. Named text styles:

| Style | Size | Use |
| --- | --- | --- |
| `PageTitleText` | 26 | Page titles |
| `SectionHeadingText` | 18 | Section headings |
| `CardHeadingText` | 15 | Card titles |
| `BodyText` | 14 | Body copy |
| `SecondaryText` | 12 | Captions, subtitles |
| `KpiValueText` | 28 | KPI numbers |
| `LabelText` | 10, semibold | Uppercase micro-labels |

## Spacing, radii, elevation

`Resources/Themes/Spacing.xaml`

- Spacing scale: `Spacing4` / `Spacing8` / `Spacing12` / `Spacing16` / `Spacing20` / `Spacing24` / `Spacing32` / `Spacing40`.
- Corner radii: `ControlRadius` 9, `CardRadius` 14, `PillRadius` 18.
- Standard paddings: `CardPadding` 20, `ControlPadding` 12,8, `InputPadding` 11,9, `PageMargin` 28,22,28,26.

`Resources/Themes/Shadows.xaml`

- `CardShadow`: subtle elevation for content cards (blur 24, opacity 0.08).
- `SoftShadow`: stronger elevation for dialogs and popups (blur 32, opacity 0.14).

## Layout

- Window: 1440×900 default, minimum 1120×720 (the handoff's usability floor).
- Sidebar: expanded 248 px, collapsed 76 px. Toggled from the header/sidebar collapse button.
- Header: 84 px tall, surface background, bottom border; holds page title/subtitle, search, date pill, notification badge and settings button.
- Main content: hosts the current page via a `ContentControl`, inset with `PageMargin`.

## Interaction principles

- POS: minimize clicks, keep product search and cart visible, highlight payable total and change due, support keyboard operation and barcode scanner input.
- Data entry: labels above fields, required fields clearly marked, numeric units shown explicitly, sensible defaults, validation beside the relevant control.
- Data grids: sortable columns, search/filter, clear empty state, avoid excessive columns on smaller screens.
- Destructive actions: confirmation plus reason where relevant; completed financial records should be reversed/voided rather than deleted.
- Status: never rely on colour alone; pair colour with text/icon.
- Feedback: every screen needs loading, empty, validation, success and failure states. Prevent duplicate submit while a transaction is processing.

## Screen patterns

- Dashboard: KPI cards, sales trend, category distribution, top products, inventory alerts, recent transactions and quick actions.
- POS: left category/search/product selection; right cart, discount, payment summary and checkout action. Larger touch-friendly targets than management forms.
- Catalogue/inventory: filter toolbar and data grid with create/edit/deactivate workflows.
- Dialogs: compact single-purpose dialogs for add/edit; use full pages for complex workflows.
- Reports: date filter, summary cards, table/chart and export actions.

## Implementation status

Step 03 establishes the shared design system: centralised theme dictionaries, reusable controls (see `COMPONENT_LIBRARY.md`), a collapsible navigation shell and a rebuilt Dashboard. Screens not yet built use a `PlaceholderView` scaffold that reuses these tokens. All new screens must reuse the palette, button styles, card geometry, typography and spacing above rather than introducing a new visual language.