namespace HighlandMilkBar.Application.DTOs;

public sealed class ReceivePurchaseResult
{
    public int PurchaseId { get; init; }
    public int BatchesCreated { get; init; }
    public int MovementsRecorded { get; init; }
    public decimal TotalQuantityReceived { get; init; }
}

public sealed class BackupResult
{
    public string Path { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTime CreatedAt { get; init; }
}