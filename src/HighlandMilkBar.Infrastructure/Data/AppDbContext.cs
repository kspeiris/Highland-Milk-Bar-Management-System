using HighlandMilkBar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<InventoryBatch> InventoryBatches => Set<InventoryBatch>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Barcode);
        modelBuilder.Entity<Product>().Property(x => x.CostPrice).HasPrecision(18, 2);
        modelBuilder.Entity<Product>().Property(x => x.SellingPrice).HasPrecision(18, 2);
        modelBuilder.Entity<Product>().Property(x => x.CurrentStock).HasPrecision(18, 3);
        modelBuilder.Entity<Product>().Property(x => x.ReorderLevel).HasPrecision(18, 3);
        modelBuilder.Entity<Product>().HasOne(x => x.Category).WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Sale>().HasIndex(x => x.ReceiptNumber).IsUnique();
        modelBuilder.Entity<Sale>().Property(x => x.TotalAmount).HasPrecision(18, 2);
        modelBuilder.Entity<Sale>().HasMany(x => x.Items).WithOne(x => x.Sale)
            .HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Sale>().HasMany(x => x.Payments).WithOne(x => x.Sale)
            .HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Sale>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<SaleItem>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Purchase>().HasIndex(x => x.InvoiceNumber);
        modelBuilder.Entity<Purchase>().HasMany(x => x.Items).WithOne(x => x.Purchase)
            .HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Purchase>().HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PurchaseItem>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InventoryBatch>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StockMovement>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StockMovement>().HasOne(x => x.InventoryBatch).WithMany().HasForeignKey(x => x.InventoryBatchId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Recipe>().HasOne(x => x.MenuProduct).WithMany().HasForeignKey(x => x.MenuProductId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Recipe>().HasMany(x => x.Items).WithOne(x => x.Recipe)
            .HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RecipeItem>().HasOne(x => x.IngredientProduct).WithMany().HasForeignKey(x => x.IngredientProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExpenseCategory>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<Expense>().HasOne(x => x.ExpenseCategory).WithMany().HasForeignKey(x => x.ExpenseCategoryId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<AppSetting>().HasIndex(x => x.Key).IsUnique();
    }
}
