namespace HighlandMilkBar.Domain.Enums;

/// <summary>
/// The order batches are consumed in. FEFO (First Expired, First Out) is the default
/// for perishable stock; FIFO (First In, First Out) is the fallback when no expiry
/// information is available.
/// </summary>
public enum AllocationPolicy
{
    Fifo,
    Fefo
}