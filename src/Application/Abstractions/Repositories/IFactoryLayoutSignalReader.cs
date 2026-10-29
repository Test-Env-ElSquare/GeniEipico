namespace Application.Abstractions.Repositories;

public interface IFactoryLayoutSignalReader
{
    Task<IReadOnlyList<FactoryLayoutSignal>> ReadAsync(int customerId, int factoryId, int[] lineIds);
}

public sealed class FactoryLayoutSignal
{
    public int CustomerId { get; set; }
    public int FactoryId { get; set; }
    public int LineId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public DateTime ShiftStartTime { get; set; }
    public decimal TotalCountDiff { get; set; }
    public DateTime LatestTimeStamp { get; set; }
    public int SignalId { get; set; }
    public int State { get; set; }
    public int Count { get; set; }
    public int Qreject { get; set; }
    public int Speed { get; set; }
    public int StateReason { get; set; }
    public int SKU { get; set; }
    public decimal SignalCountDiff { get; set; }
    public int Fault { get; set; }
    public decimal QrejectDiff { get; set; }
}