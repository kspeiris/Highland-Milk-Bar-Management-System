# UI / UX Design System

## Visual direction

A calm, modern retail-management interface: Highland-inspired deep blue identity, bright white work surfaces, soft cool-grey background, and restrained green/amber status colors. The design combines an ERP navigation shell with a fast POS workspace.

## Foundations

- Brand blue: `#1769AA`
- Deep navigation: `#102F46`
- Canvas: `#F4F7FB`
- Surface: `#FFFFFF`
- Primary text: `#172B4D`
- Secondary text: `#718096`
- Divider/border: `#E7EDF4`
- Success: `#16865B`
- Warning: `#B7791F`
- Typeface: Segoe UI (available by default on Windows)

## Layout and spacing

- Main sidebar: approximately 248 px; fixed width.
- Header: approximately 84 px high.
- Main content: 28 px outer padding.
- Spacing scale: 4 / 8 / 12 / 16 / 20 / 28 / 32 px.
- Card corner radius: 12–16 px.
- Buttons and input fields: minimum 38–42 px height.

## Interaction principles

- POS: minimize clicks, keep product search and cart visible, highlight payable total and change due, support keyboard operation and barcode scanner input.
- Data entry: labels above fields, required fields clearly marked, numeric units shown explicitly, sensible defaults, validation beside the relevant control.
- Data grids: sortable columns, search/filter, clear empty state, avoid excessive columns on smaller screens.
- Destructive actions: confirmation plus reason where relevant; completed financial records should be reversed/voided rather than deleted.
- Status: never rely on color alone; pair color with text/icon.
- Feedback: show saved/loading/error states and prevent duplicate submit while a transaction is processing.

## Screen patterns

- Dashboard: KPI cards, sales trend, low stock/expiry alerts, recent transactions and quick actions.
- POS: left category/search/product selection; right cart, discount, payment summary and checkout action. Use larger touch-friendly targets than management forms.
- Catalogue/inventory: filter toolbar and data grid with create/edit/deactivate workflows.
- Dialogs: compact single-purpose dialogs for add/edit; use full pages for complex workflows.
- Reports: date filter, summary cards, table/chart and export actions.

## Current starter implementation

The WPF shell, sidebar, top header, dashboard KPI cards, quick actions, products grid and add-product dialog establish the visual baseline. Remaining screens should reuse the palette, button styles, card geometry, typography and spacing above rather than introducing a new visual language.
