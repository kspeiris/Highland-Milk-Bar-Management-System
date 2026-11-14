using HighlandMilkBar.Domain.Enums;

namespace HighlandMilkBar.Domain.Entities;

public class ExpenseCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class Expense
{
    public int Id { get; set; }
    public int? ExpenseCategoryId { get; set; }
    public ExpenseCategory? ExpenseCategory { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; } = DateTime.Today;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public int? CashSessionId { get; set; }
    public CashSession? CashSession { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>A cash drawer session: opening float, movements and closing count.</summary>
public class CashSession
{
    public int Id { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.Now;
    public decimal OpeningFloat { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal? ClosingCounted { get; set; }
    public CashSessionStatus Status { get; set; } = CashSessionStatus.Open;
    public string? Notes { get; set; }

    public ICollection<CashMovement> Movements { get; set; } = new List<CashMovement>();
    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

/// <summary>A single cash movement within a cash session (sale, expense, payout...).</summary>
public class CashMovement
{
    public int Id { get; set; }
    public int CashSessionId { get; set; }
    public CashSession? CashSession { get; set; }
    public CashMovementType MovementType { get; set; }
    public CashDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public MovementReferenceType? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}