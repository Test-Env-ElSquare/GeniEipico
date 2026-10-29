using Domain.Common;
using Domain.Entities.Planning;
using Domain.Entities.Scada;

namespace Domain.Entities.Analytics;

// Same properties as OeeSnapshot. ShiftDate stores the LOCAL hour start in this table.
public class OeeSnapshotsPerHour : AuditableEntity
{
    public int MachineId { get; set; }
    public string? MachineName { get; set; }
    public int? CustomerId { get; set; }
    public int? FactoryId { get; set; }
    public int? LineId { get; set; }
    public int ShiftId { get; set; }
    public DateTime ShiftDate { get; set; }
    public decimal? AvailabilityPct { get; set; }
    public decimal? PerformancePct { get; set; }
    public decimal? QualityPct { get; set; }
    public decimal? OeePct { get; set; }
    public decimal? AverageSpeedRpm { get; set; }
    public int? LastState { get; set; }
    public int TotalOutput { get; set; }
    public int RejectedUnits { get; set; }
    public int? DowntimeMin { get; set; }
    public decimal? EnergyKwh { get; set; }
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    public Machine Machine { get; set; } = null!;
    public Shift Shift { get; set; } = null!;
}
