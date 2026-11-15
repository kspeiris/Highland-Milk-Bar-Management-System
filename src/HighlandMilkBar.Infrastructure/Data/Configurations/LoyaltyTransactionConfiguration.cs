using HighlandMilkBar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HighlandMilkBar.Infrastructure.Data.Configurations;

public sealed class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> b)
    {
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Reference).HasMaxLength(64);

        b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => new { x.CustomerId, x.TransactionDate });

        // Prevent awarding points twice for the same sale.
        b.HasIndex(x => x.SaleId).IsUnique()
            .HasFilter("[SaleId] IS NOT NULL AND [Type] = 0");

        b.HasOne(x => x.Customer).WithMany(x => x.LoyaltyTransactions)
            .HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Sale).WithMany()
            .HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.SalesReturn).WithMany()
            .HasForeignKey(x => x.SalesReturnId).OnDelete(DeleteBehavior.SetNull);
    }
}