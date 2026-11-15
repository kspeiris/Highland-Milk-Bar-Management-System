using HighlandMilkBar.Application.DTOs.Reports;
using HighlandMilkBar.Application.Services;

namespace HighlandMilkBar.Infrastructure.Services.Reports;

/// <summary>Static catalogue of available reports.</summary>
public sealed class ReportCatalogService : IReportCatalogService
{
    public IReadOnlyList<ReportDefinition> GetCatalog() => new[]
    {
        new ReportDefinition { Key = "sales.summary", Category = ReportCategory.Sales, Title = "Sales Summary", Description = "Revenue, orders and average order value for a period.", RequiresPeriod = true },
        new ReportDefinition { Key = "sales.transactions", Category = ReportCategory.Sales, Title = "Sales Transactions", Description = "Invoice-level transactions for a period.", RequiresPeriod = true },
        new ReportDefinition { Key = "sales.by_product", Category = ReportCategory.Sales, Title = "Sales by Product", Description = "Quantity and revenue by product.", RequiresPeriod = true },
        new ReportDefinition { Key = "sales.by_category", Category = ReportCategory.Sales, Title = "Sales by Category", Description = "Revenue grouped by category.", RequiresPeriod = true },
        new ReportDefinition { Key = "sales.by_payment", Category = ReportCategory.Sales, Title = "Payment Method Report", Description = "Sales payment-mix by method.", RequiresPeriod = true },

        new ReportDefinition { Key = "products.performance", Category = ReportCategory.Products, Title = "Product Performance", Description = "Top-selling products by units, revenue and profit.", RequiresPeriod = true },
        new ReportDefinition { Key = "products.no_sale", Category = ReportCategory.Products, Title = "Products With No Sales", Description = "Active products with no sales in the period.", RequiresPeriod = true },

        new ReportDefinition { Key = "inventory.stock", Category = ReportCategory.Inventory, Title = "Current Stock", Description = "Current snapshot of product stock levels.", RequiresPeriod = false },
        new ReportDefinition { Key = "inventory.valuation", Category = ReportCategory.Inventory, Title = "Inventory Valuation", Description = "Physical and sellable stock value by category.", RequiresPeriod = false },
        new ReportDefinition { Key = "inventory.low_stock", Category = ReportCategory.Inventory, Title = "Low Stock", Description = "Products at or below their reorder level.", RequiresPeriod = false },
        new ReportDefinition { Key = "inventory.expiry", Category = ReportCategory.Inventory, Title = "Expiry Report", Description = "Expired batches requiring action.", RequiresPeriod = false },

        new ReportDefinition { Key = "purchases.outstanding", Category = ReportCategory.Purchases, Title = "Supplier Outstanding", Description = "Outstanding payables by supplier (STEP 11).", RequiresPeriod = false },
        new ReportDefinition { Key = "purchases.by_supplier", Category = ReportCategory.Purchases, Title = "Purchases by Supplier", Description = "Received purchase value by supplier.", RequiresPeriod = false },

        new ReportDefinition { Key = "customers.directory", Category = ReportCategory.Customers, Title = "Customer Directory", Description = "Registered customers with spend and status.", RequiresPeriod = false },
        new ReportDefinition { Key = "customers.top_spending", Category = ReportCategory.Customers, Title = "Top Customers by Spending", Description = "Highest-spending customers.", RequiresPeriod = false },

        new ReportDefinition { Key = "expenses.by_category", Category = ReportCategory.Expenses, Title = "Expenses by Category", Description = "Expense totals and category breakdown.", RequiresPeriod = true },

        new ReportDefinition { Key = "financial.pl", Category = ReportCategory.Financial, Title = "Profit & Loss", Description = "P&L statement (STEP 11 financial service).", RequiresPeriod = true },
        new ReportDefinition { Key = "financial.cashflow", Category = ReportCategory.Financial, Title = "Cash Flow", Description = "Cash inflows and outflows (STEP 11).", RequiresPeriod = true },
        new ReportDefinition { Key = "financial.payables", Category = ReportCategory.Financial, Title = "Supplier Payables", Description = "Supplier liabilities and ageing (STEP 11).", RequiresPeriod = false },
        new ReportDefinition { Key = "financial.payment_analysis", Category = ReportCategory.Financial, Title = "Payment Analysis", Description = "Sales payment-mix (STEP 11).", RequiresPeriod = true },

        new ReportDefinition { Key = "milkbar.recipe_costing", Category = ReportCategory.MilkBar, Title = "Recipe Costing (Estimated)", Description = "Estimated recipe cost and margin per menu item.", RequiresPeriod = false }
    };
}