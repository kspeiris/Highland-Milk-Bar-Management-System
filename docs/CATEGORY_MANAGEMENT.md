# Category Management

Separate interface for managing product categories, opened from the Products screen ("Categories" button). Built in **Step 04**.

## Architecture

| Layer | Type | Responsibility |
| --- | --- | --- |
| Application | `ICategoryService` | Category use cases. |
| Application | `CategoryDtos.cs` | `CategoryDto`, `CreateCategoryRequest`, `UpdateCategoryRequest`. |
| Infrastructure | `CategoryService` | EF Core implementation, validation, delete guard, product counts. |
| WPF | `CategoryViewModel` | List, search, status filter, CRUD orchestration. |
| WPF | `CategoryManagementWindow` + `CategoryEditorWindow` | Listing window and add/edit dialog. |

The existing `Category` entity and `AppDbContext` are reused; no new tables.

## Fields

| Field | Notes |
| --- | --- |
| Name | Required, unique (case-insensitive at the service layer; a unique index also exists in the DB). |
| Description | Optional. |
| Status | Active / Inactive. |
| Product count | Read-only, number of products in the category. |
| Created date | Not available — the `Category` entity has no `CreatedAt` column (see limitations). |

## Features

- **List** categories with description, product count and status.
- **Search** by name or description (database-side `LIKE`).
- **Filter** by status (all / active / inactive).
- **Create** via `CategoryEditorWindow`.
- **Edit** name and description.
- **Activate / deactivate** — deactivating a category does **not** deactivate its products (PR-12).
- **Delete** — blocked when the category still contains products (PR-08 analogue); use deactivate instead.

## Business rules

- Category name is required (PR-01 analogue).
- Category names are unique; "Fresh Milk" and "fresh milk" are treated as duplicates.
- Deactivated categories are not selectable for new products (product creation requires an active category).
- Deactivating a category leaves its products unchanged and active.
- Deleting a category with products is refused.

## Known limitations

- No `CreatedAt` column on `Category`, so a creation date is not shown. Adding it requires a migration and was out of scope for this step.
- Uniqueness is case-insensitive at the service layer; the DB index is case-sensitive (BINARY). The service check is the stricter gate.

## Testing

`CategoryServiceTests` (real SQLite): create valid, empty name rejected, duplicate name (case-insensitive) rejected, edit, deactivate without touching products, delete blocked when products exist, delete when empty, product count in list.