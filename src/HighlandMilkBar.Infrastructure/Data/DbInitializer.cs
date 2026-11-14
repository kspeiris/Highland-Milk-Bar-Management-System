using HighlandMilkBar.Domain.Entities;
using HighlandMilkBar.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HighlandMilkBar.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await db.Categories.AnyAsync(cancellationToken))
        {
            var categories = new List<Category>
            {
                new() { Name = "Packaged Dairy", SortOrder = 1 },
                new() { Name = "Yoghurt & Curd", SortOrder = 2 },
                new() { Name = "Ice Cream", SortOrder = 3 },
                new() { Name = "Beverages", SortOrder = 4 },
                new() { Name = "Milk Bar Menu", SortOrder = 5 },
                new() { Name = "Ingredients", SortOrder = 6 }
            };
            db.Categories.AddRange(categories);
            await db.SaveChangesAsync(cancellationToken);

            // Starter catalogue only: prices and quantities are deliberately zero placeholders.
            var packaged = categories[0];
            var yoghurt = categories[1];
            var iceCream = categories[2];
            var beverages = categories[3];
            db.Products.AddRange(
                new Product { Sku = "HLD-FM-001", Name = "Highland Fresh Milk 1L", CategoryId = packaged.Id, Type = ProductType.Packaged, Unit = "Bottle", TrackInventory = true },
                new Product { Sku = "HLD-MP-001", Name = "Highland Milk Powder", CategoryId = packaged.Id, Type = ProductType.Packaged, Unit = "Pack", TrackInventory = true },
                new Product { Sku = "HLD-YG-001", Name = "Highland Yoghurt", CategoryId = yoghurt.Id, Type = ProductType.Packaged, Unit = "Cup", TrackInventory = true },
                new Product { Sku = "HLD-CD-001", Name = "Highland Curd", CategoryId = yoghurt.Id, Type = ProductType.Packaged, Unit = "Pot", TrackInventory = true },
                new Product { Sku = "MB-IC-001", Name = "Ice Cream Scoop", CategoryId = iceCream.Id, Type = ProductType.PreparedItem, Unit = "Scoop", TrackInventory = false },
                new Product { Sku = "MB-MS-001", Name = "Milkshake", CategoryId = beverages.Id, Type = ProductType.PreparedItem, Unit = "Glass", TrackInventory = false }
            );
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.ExpenseCategories.AnyAsync(cancellationToken))
        {
            db.ExpenseCategories.AddRange(
                new ExpenseCategory { Name = "Utilities" },
                new ExpenseCategory { Name = "Transport" },
                new ExpenseCategory { Name = "Packaging" },
                new ExpenseCategory { Name = "Maintenance" },
                new ExpenseCategory { Name = "Other" });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
