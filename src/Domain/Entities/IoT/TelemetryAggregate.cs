using Domain.Common;

namespace Domain.Entities.IoT;

public class TelemetryAggregate : AuditableEntity
{
    public int DeviceId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public DateTime Bucket { get; set; }
    public string Resolution { get; set; } = "1min"; // 1min | 1hr | 1day
    public decimal? AvgVal { get; set; }
    public decimal? MinVal { get; set; }
    public decimal? MaxVal { get; set; }
    public decimal? SumVal { get; set; }
    public int SampleCount { get; set; }

    public Device Device { get; set; } = null!;
}
