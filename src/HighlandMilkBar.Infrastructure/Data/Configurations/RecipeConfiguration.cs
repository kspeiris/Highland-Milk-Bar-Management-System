using HighlandMilkBar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HighlandMilkBar.Infrastructure.Data.Configurations;

public sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> b)
    {
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.YieldQuantity).HasPrecision(18, 3);
        b.Property(x => x.YieldUnit).HasMaxLength(32);
        b.HasIndex(x => new { x.MenuProductId, x.Version });

        b.HasOne(x => x.MenuProduct).WithMany()
            .HasForeignKey(x => x.MenuProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Items).WithOne(x => x.Recipe)
            .HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RecipeItemConfiguration : IEntityTypeConfiguration<RecipeItem>
{
    public void Configure(EntityTypeBuilder<RecipeItem> b)
    {
        b.Property(x => x.QuantityRequired).HasPrecision(18, 3);
        b.Property(x => x.Unit).HasMaxLength(32);

        b.HasOne(x => x.IngredientProduct).WithMany()
            .HasForeignKey(x => x.IngredientProductId).OnDelete(DeleteBehavior.Restrict);
    }
}