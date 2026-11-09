using System.Text.Json.Serialization;

namespace Application.Dtos.EnergyDashboard;

public sealed class TransformerSummaryDto
{
    public decimal TotalEnergy { get; set; }
    public EntityCountDto Transformers { get; set; } = new();
    public EntityCountDto Zones { get; set; } = new();
    public EntityCountDto Lines { get; set; } = new();
    public decimal AvgPowerFactor { get; set; }
    public string Level { get; set; } = "Main";
}

public sealed class EntityCountDto
{
    public int TotalCount { get; set; }
}

public sealed class OnlineStatusDto
{
    public EntityStatusDto Transformers { get; set; } = new();
    public EntityStatusDto Zones { get; set; } = new();
    public EntityStatusDto Lines { get; set; } = new();
    public string Level { get; set; } = "Main";
}

public sealed class EntityStatusDto
{
    public int OnlineCount { get; set; }
    public int OfflineCount { get; set; }
}

public sealed class EnergyConsumptionDto
{
    public string Level { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TransformerId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransformerName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ZoneId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ZoneName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LineId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LineName { get; set; }

    public decimal Energy { get; set; }
}

public sealed class HourlyEnergyConsumptionDto
{
    public int FactoryId { get; set; }
    public DateTime HourStartTime { get; set; }
    public string Level { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TransformerId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransformerName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ZoneId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ZoneName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LineId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LineName { get; set; }

    public decimal Energy { get; set; }
}

public sealed class TopEnergyConsumerDto
{
    public int Rank { get; set; }
    public string Level { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TransformerId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransformerName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ZoneId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ZoneName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LineId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LineName { get; set; }

    public decimal Energy { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public sealed class VoltageGraphDto
{
    public int FactoryId { get; set; }
    public DateTime HourStartTime { get; set; }
    public decimal AvgVoltage { get; set; }
}

public sealed class CurrentGraphDto
{
    public int FactoryId { get; set; }
    public DateTime HourStartTime { get; set; }
    public decimal AvgCurrent { get; set; }
}

public sealed class HarmonicsGraphDto
{
    public int FactoryId { get; set; }
    public DateTime HourStartTime { get; set; }
    public decimal AvgHarmonics { get; set; }
}

public sealed class EnergyHeatmapDto
{
    public DateTime Date { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TransformerId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TransformerName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ZoneId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ZoneName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LineId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LineName { get; set; }

    public decimal Energy { get; set; }

    // EMS serializes EnergyStatus numerically: Unknown=0, Low=1, Medium=2, High=3.
    public int Status { get; set; }
}
