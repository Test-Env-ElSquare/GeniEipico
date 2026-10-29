using Domain.Common;
using Domain.Entities.Organization;

namespace Domain.Entities.Configuration;

public class MetricThreshold : SoftDeleteEntity
{
    public int CustomerId { get; set; }
    public string Metric { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }

    public Customer Customer { get; set; } = null!;
}
