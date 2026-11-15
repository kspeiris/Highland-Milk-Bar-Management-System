# Supplier Management

The supplier directory: CRUD, activation, and the financial rollups that feed the details and statement screens. Built in **Step 08** on the existing `Supplier` entity.

## Architecture

| Layer | Type | Responsibility |
| --- | --- | --- |
| Application | `ISupplierService` | CRUD, activate/deactivate, delete guard, details, statement. |
| Application | `SupplierDtos.cs` | Directory, detail, purchase/payment history, statement DTOs. |
| Infrastructure | `SupplierService` | EF Core implementation with database-backed rollups. |
| WPF | `SuppliersViewModel`, `SupplierDetailsViewModel` | Directory list; details/statement. |
| WPF | `SuppliersView`, `SupplierEditorWindow`, `SupplierDetailsWindow` | List, add/edit, details + statement. |

Schema additions (migration `20261115120000_AddPurchaseSupplierManagement`): `Code`, `BusinessRegNo`, `TaxRegNo`, `UpdatedAt`.

## Supplier fields

| Field | Notes |
| --- | --- |
| Supplier ID | Primary key |
| Supplier Code | Optional internal code; unique (case-insensitive) when provided |
| Supplier Name | Required, ≤ 200 chars |
| Contact Person | Optional |
| Phone | Optional; permissive format check (digits and `+ - space ( ) /`) |
| Email | Optional; validated when present |
| Address | Optional |
| Business Registration No. | Optional |
| Tax Registration No. | Optional |
| Notes | Optional |
| Status | Active / Inactive |
| Created / Updated | Timestamps |

## Operations

- **Add / edit** — validated in `SupplierService` (name required, email when present, unique code). Inactive suppliers cannot be selected for a new purchase.
- **Activate / deactivate** — `SetActiveAsync`. Deactivation preserves all history; use it instead of deletion.
- **Delete** — rejected when the supplier has any purchase or payment (deactivate instead). Existing purchase/payment records are never removed by deactivation.
- **Search / filter** — by text (name/code/contact/phone) and by active status.
- **Details** — overview (contact, total purchased, total paid, outstanding, latest purchase/payment), purchase history, payment history.
- **Statement** — dated ledger with running balance (`SUPPLIER_STATEMENTS.md`).

## Supplier details rollups

- **Total Purchased** = Σ `TotalAmount` of the supplier's **received** purchases.
- **Total Paid** = Σ `SupplierPayment.Amount` allocated to that supplier.
- **Outstanding** = Total Purchased − Total Paid (floored at zero).

Draft and cancelled purchases never contribute (PUR-17). Only committed purchases and payments are counted.

## Currency

All amounts are in **LKR** and stored as `decimal`.