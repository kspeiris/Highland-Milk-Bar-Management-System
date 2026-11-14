using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Data;

/// <summary>
/// Applies pending migrations and seeds reference data. Seeding is idempotent and
/// never deletes or recreates user data, so it is safe to run on every startup.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        await SeedUnitsAsync(db, cancellationToken);
        await SeedCategoriesAndProductsAsync(db, cancellationToken);
        await SeedExpenseCategoriesAsync(db, cancellationToken);
        await SeedSettingsAsync(db, cancellationToken);
    }

    private static async Task SeedUnitsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Units.AnyAsync(cancellationToken)) return;

        db.Units.AddRange(
            new Unit { Name = "Count", Symbol = "pcs", Kind = UnitKind.Count, SortOrder = 1 },
            new Unit { Name = "Bottle", Symbol = "btl", Kind = UnitKind.Volume, SortOrder = 2 },
            new Unit { Name = "Cup", Symbol = "cup", Kind = UnitKind.Volume, SortOrder = 3 },
            new Unit { Name = "Pack", Symbol = "pack", Kind = UnitKind.Count, SortOrder = 4 },
            new Unit { Name = "Pot", Symbol = "pot", Kind = UnitKind.Count, SortOrder = 5 },
            new Unit { Name = "Scoop", Symbol = "scoop", Kind = UnitKind.Volume, SortOrder = 6 },
            new Unit { Name = "Glass", Symbol = "glass", Kind = UnitKind.Volume, SortOrder = 7 },
            new Unit { Name = "Portion", Symbol = "portion", Kind = UnitKind.Count, SortOrder = 8 },
            new Unit { Name = "Litre", Symbol = "L", Kind = UnitKind.Volume, SortOrder = 9 },
            new Unit { Name = "Millilitre", Symbol = "ml", Kind = UnitKind.Volume, SortOrder = 10 },
            new Unit { Name = "Kilogram", Symbol = "kg", Kind = UnitKind.Weight, SortOrder = 11 },
            new Unit { Name = "Gram", Symbol = "g", Kind = UnitKind.Weight, SortOrder = 12 });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCategoriesAndProductsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Categories.AnyAsync(cancellationToken)) return;

        var categories = new List<Category>
        {
            new() { Name = "Fresh Milk", SortOrder = 1 },
            new() { Name = "Milk Powder", SortOrder = 2 },
            new() { Name = "Yoghurt", SortOrder = 3 },
            new() { Name = "Drinking Yoghurt", SortOrder = 4 },
            new() { Name = "Ice Cream", SortOrder = 5 },
            new() { Name = "Beverages", SortOrder = 6 },
            new() { Name = "Milkshakes", SortOrder = 7 },
            new() { Name = "Curd", SortOrder = 8 },
            new() { Name = "Butter & Cheese", SortOrder = 9 },
            new() { Name = "Ingredients", SortOrder = 10 }
        };
        db.Categories.AddRange(categories);
        await db.SaveChangesAsync(cancellationToken);

        // Starter catalogue only: prices and quantities are deliberately zero placeholders.
        var freshMilk = categories[0];
        var milkPowder = categories[1];
        var yoghurt = categories[2];
        var iceCream = categories[4];
        var beverages = categories[5];
        var milkshakes = categories[6];
        var curd = categories[7];

        db.Products.AddRange(
            new Product { Sku = "HLD-FM-001", Name = "Highland Fresh Milk 1L", CategoryId = freshMilk.Id, Type = ProductType.Packaged, Unit = "Bottle", TrackInventory = true },
            new Product { Sku = "HLD-MP-001", Name = "Highland Milk Powder", CategoryId = milkPowder.Id, Type = ProductType.Packaged, Unit = "Pack", TrackInventory = true },
            new Product { Sku = "HLD-YG-001", Name = "Highland Yoghurt", CategoryId = yoghurt.Id, Type = ProductType.Packaged, Unit = "Cup", TrackInventory = true },
            new Product { Sku = "HLD-CD-001", Name = "Highland Curd", CategoryId = curd.Id, Type = ProductType.Packaged, Unit = "Pot", TrackInventory = true },
            new Product { Sku = "MB-IC-001", Name = "Ice Cream Cup", CategoryId = iceCream.Id, Type = ProductType.PreparedItem, Unit = "Cup", TrackInventory = false },
            new Product { Sku = "MB-MS-001", Name = "Chocolate Milkshake", CategoryId = milkshakes.Id, Type = ProductType.PreparedItem, Unit = "Glass", TrackInventory = false }
        );
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedExpenseCategoriesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.ExpenseCategories.AnyAsync(cancellationToken)) return;

        db.ExpenseCategories.AddRange(
            new ExpenseCategory { Name = "Utilities" },
            new ExpenseCategory { Name = "Transport" },
            new ExpenseCategory { Name = "Packaging" },
            new ExpenseCategory { Name = "Maintenance" },
            new ExpenseCategory { Name = "Rent" },
            new ExpenseCategory { Name = "Wages" },
            new ExpenseCategory { Name = "Other" });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSettingsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var defaults = new (string Key, string Value)[]
        {
            ("outlet.name", "Highland Milk Bar"),
            ("currency.code", "LKR"),
            ("inventory.allow_negative_stock", "false"),
            ("inventory.allocation_policy", "FEFO")
        };

        var changed = false;
        foreach (var (key, value) in defaults)
        {
            if (await db.AppSettings.AnyAsync(s => s.Key == key, cancellationToken)) continue;
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
            changed = true;
        }

        if (changed) await db.SaveChangesAsync(cancellationToken);
    }
}