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
