using HighlandMilkBar.Application.DTOs;
using HighlandMilkBar.Domain.Enums;
using HighlandMilkBar.Infrastructure.Data;
using HighlandMilkBar.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Tests;

/// <summary>
/// Step 15 end-to-end workflow tests. Each test exercises a realistic business flow that
/// crosses several services and asserts the resulting records stay consistent. Uses a real
/// temporary SQLite database (never InMemory).
/// </summary>
public sealed class EndToEndWorkflowTests : IAsyncLifetime
{
    private readonly TestDatabase _db = new();
    private AppDbContext Db => _db.Db;

    private InventoryService Inventory => new(Db);
    private LoyaltyService Loyalty => new(Db);
    private SalesService Sales => new(Db, Inventory, Loyalty);
    private PurchaseService Purchases => new(Db, Inventory);
    private SupplierService Suppliers => new(Db);
    private SupplierPaymentService Payments => new(Db);
    private ExpenseService Expenses => new(Db);
    private RecipeService Recipes => new(Db);
    private FinancialQueryService Financial => new(Db);

    public Task InitializeAsync() => _db.InitializeAsync();
    public Task DisposeAsync() => _db.DisposeAsync();

    private static readonly DateTime WindowStart = new(2000, 1, 1);
    private static readonly DateTime WindowEnd = new(2100, 1, 1);

    // Workflow A — packaged product: purchase → inventory → POS → finance.
    [Fact]
    public async Task WorkflowA_PackagedProductSale_StayConsistent()
    {
        var cat = await _db.AddCategoryAsync("Cat");
        var product = await _db.AddProductAsync("P-A", "Fresh Milk", cat.Id, track: true, price: 500m);

        var supplier = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Dairy" });
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 100, UnitCost = 180m, BatchNumber = "B-A" }]
        });
        await Purchases.ReceiveAsync(purchase.Id);

        // Stock received.
        Assert.Equal(100m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
        Assert.Single(await Db.InventoryBatches.ToListAsync());

        // Sell 2 units.
        var sale = await Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = [new SaleLineRequest { ProductId = product.Id, Quantity = 2m, UnitPrice = 500m }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 1000m }]
        });

        // Stock reduced and a stock movement recorded.
        Assert.Equal(98m, (await Db.Products.FindAsync(product.Id))!.CurrentStock);
        Assert.True(await Db.StockMovements.AnyAsync());

        // Sale + payment persisted.
        var persisted = await Db.Sales.FindAsync(sale.SaleId);
        Assert.NotNull(persisted);
        Assert.Equal(2m * 180m, persisted!.CostOfGoodsSold);
        Assert.Single(await Db.Payments.ToListAsync());

        // Finance agrees: COGS from the sale, net sales from the total.
        var summary = await Financial.GetSummaryAsync(WindowStart, WindowEnd);
        Assert.Equal(1000m, summary.NetSales);
        Assert.Equal(360m, summary.CostOfGoodsSold);
        Assert.Equal(640m, summary.GrossProfit);
    }

    // Workflow B — milk bar: ingredient → recipe → prepared sale → ingredient deduction.
    [Fact]
    public async Task WorkflowB_MilkBarSale_DeductsIngredientsAndCostsSale()
    {
        var cat = await _db.AddCategoryAsync("Cat");
        var ingredient = await _db.AddIngredientAsync("ING-MLK", "Milk (ingredient)", "L", 0m, cat.Id);
        var menu = await _db.AddMenuItemAsync("MB-MS", "Milkshake", cat.Id, 500m);
        var recipe = await _db.AddRecipeAsync("Milkshake", menu.Id, 1m, "Glass", true, (ingredient.Id, 0.2m, "L"));

        var supplier = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Dairy" });
        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = ingredient.Id, Quantity = 100, Unit = "L", UnitCost = 200m, BatchNumber = "MLK-A" }]
        });
        await Purchases.ReceiveAsync(purchase.Id);

        // Recipe costing reflects the received batch cost (0.2 L × 200 = 40).
        var breakdown = await Recipes.CalculateCostAsync(recipe.Id, 1m);
        Assert.Equal(40m, breakdown.TotalCost);

        var sale = await Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            Items = [new SaleLineRequest { ProductId = menu.Id, Quantity = 1m, UnitPrice = 500m }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 500m }]
        });

        // Ingredient consumed (100 → 99.8 L).
        Assert.Equal(99.8m, (await Db.Products.FindAsync(ingredient.Id))!.CurrentStock);

        // Sale COGS uses the recipe cost snapshot.
        var persisted = await Db.Sales.FindAsync(sale.SaleId);
        Assert.Equal(40m, persisted!.CostOfGoodsSold);

        var summary = await Financial.GetSummaryAsync(WindowStart, WindowEnd);
        Assert.Equal(500m, summary.NetSales);
        Assert.Equal(40m, summary.CostOfGoodsSold);
    }

    // Workflow C — customer loyalty: linked sale earns points, return reverses them.
    [Fact]
    public async Task WorkflowC_Loyalty_LedgerMatchesSalesAndReturns()
    {
        var cat = await _db.AddCategoryAsync("Cat");
        var product = await _db.AddProductAsync("P-C", "Yoghurt", cat.Id, track: true, price: 500m);
        await Inventory.ReceiveAsync(new StockReceiptRequest
        {
            ProductId = product.Id, BatchNumber = "B-C", Quantity = 100, UnitCost = 200m,
            ReceivedDate = DateTime.Today, ExpiryDate = DateTime.Today.AddDays(30)
        });

        var customer = await _db.AddCustomerAsync("Buyer", "0770000001");

        var sale = await Sales.CheckoutAsync(new SaleCheckoutRequest
        {
            CustomerId = customer.Id,
            Items = [new SaleLineRequest { ProductId = product.Id, Quantity = 1m, UnitPrice = 500m }],
            Payments = [new PaymentRequest { Method = PaymentMethod.Cash, Amount = 500m }]
        });

        Assert.Equal(5, (await Loyalty.GetBalanceAsync(customer.Id)).Balance); // 500/100*1

        var saleItem = await Db.SaleItems.SingleAsync(i => i.SaleId == sale.SaleId);
        await Sales.ProcessReturnAsync(new SalesReturnRequest
        {
            SaleId = sale.SaleId,
            Reason = "Customer changed mind",
            Lines = [new ReturnLineRequest { SaleItemId = saleItem.Id, Quantity = 1m, Condition = ReturnCondition.Restock }]
        });

        Assert.Equal(0, (await Loyalty.GetBalanceAsync(customer.Id)).Balance);
        Assert.Equal(2, await Db.LoyaltyTransactions.CountAsync()); // award + reversal
    }

    // Workflow D — purchase and supplier payments: receiving, partial payments, outstanding.
    [Fact]
    public async Task WorkflowD_PurchaseAndSupplierPayments_StayConsistent()
    {
        var cat = await _db.AddCategoryAsync("Cat");
        var product = await _db.AddProductAsync("P-D", "Milk Powder", cat.Id, track: true);
        var supplier = await Suppliers.CreateAsync(new CreateSupplierRequest { Name = "Supplier D" });

        var purchase = await Purchases.CreateAsync(new CreatePurchaseRequest
        {
            SupplierId = supplier.Id,
            Items = [new PurchaseLineRequest { ProductId = product.Id, Quantity = 100, UnitCost = 500m, BatchNumber = "B-D" }]
        });
        await Purchases.ReceiveAsync(purchase.Id);
        Assert.Equal(50000m, (await Purchases.GetByIdAsync(purchase.Id))!.Outstanding);

        await Payments.RecordAsync(new RecordPaymentRequest { PurchaseId = purchase.Id, Amount = 20000m });
        await Payments.RecordAsync(new RecordPaymentRequest { PurchaseId = purchase.Id, Amount = 15000m });

        var updated = await Purchases.GetByIdAsync(purchase.Id);
        Assert.Equal(35000m, updated!.AmountPaid);
        Assert.Equal(15000m, updated.Outstanding);
        Assert.Equal(PaymentStatus.PartiallyPaid, updated.PaymentStatus);
        Assert.Equal(2, updated.Payments.Count);

        // Financial summary reflects the outstanding supplier liability (no double counting).
        var summary = await Financial.GetSummaryAsync(WindowStart, WindowEnd);
        Assert.Equal(15000m, summary.SupplierOutstanding);
    }

    // Workflow E — expense and cash: cash expense records a cash movement and reports.
    [Fact]
    public async Task WorkflowE_ExpenseAndCash_RecordsMovementAndTotal()
    {
        var session = _db.AddCashSession(open: true);
        var category = await Db.ExpenseCategories.FirstAsync(c => c.Name == "Utilities");

        var expense = await Expenses.CreateAsync(new ExpenseCreateRequest
        {
            CategoryId = category.Id,
            Description = "Electricity",
            Amount = 200m,
            ExpenseDate = DateTime.Today,
            PaymentMethod = PaymentMethod.Cash,
            CashSessionId = session.Id
        });

        Assert.Equal(200m, expense.Amount);
        Assert.Single(await Db.Expenses.ToListAsync());

        // A cash expense against an open session produces exactly one cash movement.
        var movements = await Db.CashMovements.ToListAsync();
        Assert.Single(movements);
        Assert.Equal(200m, movements[0].Amount);
        Assert.Equal(session.Id, movements[0].CashSessionId);
    }
}