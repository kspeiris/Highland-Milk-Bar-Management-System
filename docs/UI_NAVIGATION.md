# UI Navigation

## Shell structure

`MainWindow.xaml` is a two-column layout:

- **Left — Sidebar** (`Border` width bound to `SidebarWidth`, `SidebarBrush`): brand block, collapse toggle, navigation list, and a "LOCAL MODE" footer.
- **Right — Main area**: an 84 px header (`Row 0`) and the page host (`Row 1`, a `ContentControl` bound to `MainViewModel.CurrentViewModel`).

The `ContentControl` resolves the concrete view through `DataTemplate`s declared in `MainWindow.Resources`:

| ViewModel | View |
| --- | --- |
| `DashboardViewModel` | `DashboardView` |
| `ProductsViewModel` | `ProductsView` |
| `PlaceholderViewModel` | `PlaceholderView` |

Add a new real screen by creating its ViewModel + View and registering a `DataTemplate` here, then wiring the key in `MainViewModel.NavigateTo`.

## Navigation items

`MainViewModel.NavItems` defines 12 items (key, glyph, title):

Dashboard · Point of Sale · Products · Inventory · Milk Bar · Purchases · Suppliers · Customers · Expenses · Finance · Reports · Settings

Each item is a `NavItemViewModel` with `Title`, `Glyph`, `Key` and `IsSelected`. `SidebarNavButton` reacts to `IsSelected` (highlight bar, selected background, white text).

## Sidebar collapse

- `IsSidebarCollapsed` toggles `SidebarWidth` between **248** (expanded) and **76** (collapsed).
- Collapsed mode hides labels, the brand wordmark and the LOCAL MODE footer; glyphs stay centred. The header collapse button shows `«` / `»` (`SidebarGlyph`).
- Toggled via `ToggleSidebarCommand` (bound to the collapse button). Navigation still works while collapsed.

## Header

Header shows the current page title (`CurrentTitle`) and subtitle (`CurrentSubtitle`), plus:

- `SearchBox` bound to `MainViewModel.SearchText` (global search entry; per-module search lands with each module).
- Date pill bound to `TodayLabel` (`dddd, dd MMMM yyyy`).
- Notification badge (decorative for now).
- Settings button wired to `OpenSettingsCommand` → navigates to Settings.

## Navigation flow

1. `MainViewModel.NavigateTo(key)` sets the matching nav item selected, updates title/subtitle, then selects the target ViewModel:
   - `Dashboard` → `DashboardViewModel` (loads via `IDashboardService`)
   - `Products` → `ProductsViewModel` (loads via `IProductService`)
   - everything else → `PlaceholderViewModel` with a module description, planned step and glyph
2. `MainViewModel` is constructed in `App.OnStartup` with `IDashboardService` and `IProductService`, then `InitializeAsync()` loads the initial Dashboard.
3. Dashboard and Products ViewModels expose `LoadAsync()` which `NavigateTo` triggers after switching.

## Placeholder modules

Each unbuilt module opens `PlaceholderView`: a page header, a disabled search toolbar scaffold, status badges (`Status` + planned `Step`), and an `EmptyState` describing the planned workspace. This makes reserved navigation visible without pretending to be a finished screen.

Planned step mapping (`MainViewModel.GetPlannedStep`): POS → Step 06, Inventory → Step 05, Milk Bar → Step 07, Purchases/Suppliers → Step 08, Customers → Step 09, Expenses → Step 10, Finance → Step 11, Reports → Step 13, Settings → Step 14.

## Adding a screen (checklist)

1. Create `ViewModels/XxxViewModel.cs` (derive from `ObservableObject`).
2. Create `Views/XxxView.xaml` (UserControl) using the shared controls/styles.
3. Register a `DataTemplate` for the ViewModel in `MainWindow.Resources`.
4. Add/confirm the nav item key in `MainViewModel.NavItems`.
5. Add a `case` in `MainViewModel.NavigateTo` returning the new ViewModel and loading it.
6. Replace the module's entry in `GetScaffoldDescription` / `GetPlannedStep` once built.