# Business Alert Rules

Rule-based, read-only alerts generated in **Step 12** by `BusinessAnalyticsQueryService`. Alerts are calculated on demand (no persistence), explainable, and never modify data. Repeated refreshes do not duplicate alerts: each alert has a deterministic `Key` (`Type:RelatedRecordId`) and the list is de-duplicated.

## Severity

Critical > Warning > Information. Each alert carries Title, Description, Severity, Category, Metric, Suggested action, related record id, navigation target and a generated timestamp.

## Rules

| Type | Category | Condition | Severity |
| --- | --- | --- | --- |
| `Inventory.LowStock` | Inventory | A tracked product's sellable stock ≤ reorder level (from `IInventoryQueryService` low-stock alerts). | Warning |
| `Inventory.Expiry` | Inventory | An expired batch exists (from inventory expiry alerts). | Warning |
| `MilkBar.*` | Recipe | Milk Bar overview alerts (missing recipe, insufficient ingredients, etc.). | Info/Warning/Critical (mapped) |
| `Sales.Decline` | Sales | Net sales < 70% of the previous equivalent period (which is > 0). | Warning |
| `Expense.Increase` | Expense | Operating expenses > 120% of the previous equivalent period (which is > 0). | Warning |
| `Finance.Payables` | Finance | Supplier payables outstanding > 0 (from `IFinancialQueryService`). | Information |
| `Customer.NoRepeat` | Customer | Repeat customers = 0 while total customers > 0. | Information |

## Guardrails

- No AI/speculative predictions; every alert cites an observed metric.
- Alerts are separate from recommendations: each suggests a practical action and a navigation target.
- The engine never creates purchases, changes stock, or deactivates records.
- Thresholds (70% decline, 120% expense increase) are documented constants, not hidden magic numbers.