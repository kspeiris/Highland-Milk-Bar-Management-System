namespace HighlandMilkBar.Domain.Enums;

public enum PaymentMethod
{
    Cash,
    Card,
    BankTransfer,
    Cheque,
    Other
}

public enum SaleStatus
{
    Completed,
    Voided,
    PartiallyReturned,
    Returned
}

public enum StockMovementType
{
    Opening,
    Purchase,
    Sale,
    SaleReturn,
    AdjustmentIn,
    AdjustmentOut,
    Waste,
    Expired,
    RecipeConsumption
}

public enum PurchaseStatus
{
    Draft,
    Received,
    PartiallyPaid,
    Paid,
    Cancelled
}

/// <summary>
/// The settlement state of a purchase, derived from allocated payments against the
/// received grand total. Kept separate from <see cref="PurchaseStatus"/> (the goods
/// receiving lifecycle) so receiving and payment never get mixed (PUR rule set).
/// </summary>
public enum PaymentStatus
{
    Unpaid,
    PartiallyPaid,
    Paid
}

public enum StockAdjustmentStatus
{
    Draft,
    Applied,
    Cancelled
}

public enum SalesReturnStatus
{
    Completed,
    Voided
}

public enum ReturnCondition
{
    /// <summary>Sellable / unopened — may re-enter sellable stock.</summary>
    Restock,
    /// <summary>Damaged goods — never re-enters sellable stock.</summary>
    Damaged,
    /// <summary>Expired goods — never re-enters sellable stock.</summary>
    Expired,
    /// <summary>Not suitable for resale (opened, contaminated, etc.).</summary>
    NotForResale,
    /// <summary>Generic discard kept for backwards compatibility; never restocked.</summary>
    Discard
}

public enum CashSessionStatus
{
    Open,
    Closed
}

public enum CashMovementType
{
    OpeningFloat,
    Sale,
    Refund,
    Expense,
    PayIn,
    Payout,
    Closing
}

public enum CashDirection
{
    In,
    Out
}

public enum MovementReferenceType
{
    Opening,
    Purchase,
    Sale,
    SaleReturn,
    Adjustment,
    Recipe,
    Manual
}

/// <summary>Why stock is being received into a batch.</summary>
public enum StockReceiptReason
{
    OpeningStock,
    ManualReceipt,
    PositiveCorrection
}

/// <summary>The nature of a manual stock adjustment, which determines the ledger movement type.</summary>
public enum StockAdjustmentKind
{
    /// <summary>Sign-driven correction (positive adds, negative removes).</summary>
    Correction,
    /// <summary>Damaged stock write-off (always reduces stock).</summary>
    Damaged,
    /// <summary>Expired stock write-off (always reduces stock).</summary>
    Expired
}