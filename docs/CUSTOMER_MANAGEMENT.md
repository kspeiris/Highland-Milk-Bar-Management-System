# Customer Management

Customer records, profiles and the read models that feed the listing and profile screens. Built in **Step 09** on the existing `Customer` entity (extended, not replaced).

## Architecture

| Layer | Type | Responsibility |
| --- | --- | --- |
| Domain | `Customer` (extended) | Code, type, phone, alternative phone, email, address, city, date of birth, notes, active flag. |
| Domain | `LoyaltyTransaction` | Append-only loyalty ledger. |
| Application | `ICustomerService` | Create, update, activate/deactivate, safe delete, phone-duplicate check. |
| Application | `ICustomerQueryService` | Listing (DB-side filter/sort/paging), profile, history, products, payments/returns, analytics. |
| Application | `CustomerDtos`, `CustomerAnalyticsDtos`, `LoyaltyDtos` | Contracts for the above. |
| Infrastructure | `CustomerService`, `CustomerQueryService` | EF Core implementations. |
| WPF | `CustomersViewModel`, `CustomerDetailsViewModel`, `CustomerAnalyticsViewModel` | Listing, profile, analytics. |
| WPF | `CustomersView`, `CustomerEditorWindow`, `CustomerDetailsWindow`, `CustomerAnalyticsWindow` | List, add/edit, profile tabs, analytics. |

Schema additions (migration `20261115130000_AddCustomerManagementLoyalty`): `Customers.CustomerCode`, `Type`, `AlternativePhone`, `City`, `DateOfBirth`, `Notes`, `UpdatedAt`; new `LoyaltyTransactions` table.

## Customer fields

| Field | Notes |
| --- | --- |
| Customer Code | Generated `CUS-00000001`, unique |
| Type | `Individual` or `Business` |
| Name | Required, ≤ 200 chars |
| Phone | Required on create; digits normalized |
| Alternative Phone | Optional |
| Email | Optional; format-checked when present |
| Address / City | Optional |
| Date of Birth | Optional |
| Notes | Optional |
| Active | Active customers are selectable at POS |

## Listing

- KPI cards: total, active, new this month (by registration date), repeat customers (2+ completed sales), total customer sales.
- Search across code, name, phone and email.
- Filters by type and status; sort by name, spending or last purchase.
- Pagination, 15 rows per page, applied database-side.

## Activation and deletion

- Deactivated customers remain visible and keep their sales; they are not selectable at POS.
- A customer with sales, returns or loyalty history cannot be deleted — deactivate instead.

## Related docs

- `CUSTOMER_BUSINESS_RULES.md` — lifecycle and validation rules.
- `CUSTOMER_ANALYTICS.md` — metrics and segmentation.
- `CUSTOMER_LOYALTY_RULES.md` — points earning and reversal.
- `POS_CUSTOMER_INTEGRATION.md` — optional customer selection at checkout.