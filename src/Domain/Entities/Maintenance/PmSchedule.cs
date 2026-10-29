using Domain.Common;
using Domain.Entities.AssetRegistry;
using Domain.Entities.Organization;
using Domain.Entities.Scada;

namespace Domain.Entities.Maintenance;

public class PmSchedule : SoftDeleteEntity
{
    public int CustomerId { get; set; }
    public int? MachineId { get; set; }
    public int? AssetId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string Frequency { get; set; } = "monthly";
    public int IntervalDays { get; set; }
    public DateTime? LastDoneAt { get; set; }
    public DateTime NextDueAt { get; set; }
    public string? AssignedTeam { get; set; }
    public decimal? EstimatedDurationHours { get; set; }
    public string? RequiredPartsNotes { get; set; }
    public string? Instructions { get; set; }
    public bool IsActive { get; set; } = true;

    public Customer Customer { get; set; } = null!;
    public Machine? Machine { get; set; }
    public Asset? Asset { get; set; }
}
