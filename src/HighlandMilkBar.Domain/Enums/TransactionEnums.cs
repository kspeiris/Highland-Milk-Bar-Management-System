namespace HighlandMilkBar.Domain.Enums;

public enum PaymentMethod
{
    Cash,
    Card,
    BankTransfer,
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
    Restock,
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