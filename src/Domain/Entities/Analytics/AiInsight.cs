using Domain.Common;
using Domain.Entities.Organization;

namespace Domain.Entities.Analytics;

public class AiInsight : SoftDeleteEntity
{
    public int CustomerId { get; set; }
    public string InsightType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public decimal? ImpactValue { get; set; }
    public string? ImpactCurrency { get; set; }
    public decimal? ConfidencePct { get; set; }
    public string? ModelVersion { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsDismissed { get; set; }

    public Customer Customer { get; set; } = null!;
}
