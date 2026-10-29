using Domain.Common;

namespace Domain.Entities.Analytics;

// Persisted local-time hourly analysis; created by EF migrations.
public class PowerAnalysisPerHour : Entity
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
    public DateTime HourStartTime { get; set; }
    public DateTime HourEndTime { get; set; }
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
