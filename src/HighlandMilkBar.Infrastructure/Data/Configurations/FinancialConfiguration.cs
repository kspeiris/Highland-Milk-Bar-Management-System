using HighlandMilkBar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HighlandMilkBar.Infrastructure.Data.Configurations;

public sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> b)
    {
        b.Property(x => x.Name).IsRequired().HasMaxLength(120);
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> b)
    {
        b.Property(x => x.Description).IsRequired().HasMaxLength(300);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.ReferenceNumber).HasMaxLength(120);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => x.ExpenseDate);

        b.HasOne(x => x.ExpenseCategory).WithMany()
            .HasForeignKey(x => x.ExpenseCategoryId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.CashSession).WithMany(x => x.Expenses)
            .HasForeignKey(x => x.CashSessionId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> b)
    {
        b.Property(x => x.OpeningFloat).HasPrecision(18, 2);
        b.Property(x => x.ClosingCounted).HasPrecision(18, 2);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => x.Status);

        b.HasMany(x => x.Movements).WithOne(x => x.CashSession)
            .HasForeignKey(x => x.CashSessionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> b)
    {
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.CashSessionId, x.CreatedAt });
    }
}