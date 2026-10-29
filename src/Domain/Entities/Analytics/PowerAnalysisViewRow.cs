namespace Domain.Entities.Analytics;

// Read-only projection of Calculated.VW_Batterje_PowerAnalysis. No identity column in the view.
public class PowerAnalysisViewRow
{
    public int TransformerId { get; set; }
    public string TransformerName { get; set; } = "";
    public int CustomerId { get; set; }
    public int FactoryId { get; set; }
    public int? LineId { get; set; }
    public int ShiftId { get; set; }
    public string ShiftName { get; set; } = "";
    public DateTime ShiftStartTime { get; set; }
    public DateTime ShiftEndTime { get; set; }
    public decimal TotalEnergyConsumption { get; set; }
    public string Status { get; set; } = "";
    public decimal PowerFactor { get; set; }
    public decimal Voltage { get; set; }
    public decimal Current { get; set; }
    public decimal Hermonics { get; set; }
    public decimal AvgTHDv { get; set; }
    public decimal AvgTHDi { get; set; }
    public DateTime? LastReadingAt { get; set; }
    public long ReadingCount { get; set; }
    public DateTime CalculatedAt { get; set; }
}
