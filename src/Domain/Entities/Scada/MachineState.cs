using Domain.Common;

namespace Domain.Entities.Scada;

/// <summary>
/// One immutable production reading for an external machine identifier.
/// Persisted as RealTime.Production.
/// </summary>
public class MachineState : Entity
{
    public string MachineId { get; set; } = string.Empty;
    public int State { get; set; }
    public int Speed { get; set; }
    public int Count { get; set; }
    public decimal CountDiff { get; set; }
    public int CustomerId { get; set; }
    public int LineId { get; set; }
    public int FactoryId { get; set; }
    public decimal? Temp { get; set; }
    public int QReject { get; set; }
    public decimal QRejectDiff { get; set; }
    public int StateReason { get; set; }
    public int Sku { get; set; }
    public int Fault { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime? ShiftStartTime { get; set; }

    public void ApplyCount(int currentCount)
    {
        CountDiff = Math.Max(0m, (decimal)currentCount - Count);
        Count = currentCount;
    }
}
