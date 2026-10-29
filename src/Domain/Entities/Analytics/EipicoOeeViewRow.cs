namespace Domain.Entities.Analytics;

// Read-only projection of Analytics.VW_EipicoOee.
public class EipicoOeeViewRow
{
    public int MachineId { get; set; }
    public string MachineName { get; set; } = "";
    public int CustomerId { get; set; }
    public int FactoryId { get; set; }
    public int LineId { get; set; }
    public int ShiftId { get; set; }
    public string ShiftName { get; set; } = "";
    public DateTime ShiftStartTime { get; set; }
    public DateTime ShiftEndTime { get; set; }
    public int State { get; set; }
    public decimal Count { get; set; }
    public decimal Qreject { get; set; }
    public decimal Availability { get; set; }
    public decimal AVGSpeed { get; set; }
    public decimal Quality { get; set; }
    public decimal Performance { get; set; }
    public decimal OEE { get; set; }
}
