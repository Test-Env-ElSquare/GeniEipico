namespace Infrastructure.Integration;

public sealed class EipicoSyncOptions
{
    public const string SectionName = "EipicoSync";

    public string ExpectedSourceDatabase { get; set; } = "Eipico";
    public int PollIntervalSeconds { get; set; } = 5;
    public int DefinitionsSyncIntervalSeconds { get; set; } = 300;
    public int BatchSize { get; set; } = 100;
    public int RetryBatchSize { get; set; } = 10;
    public int ErrorRetryIntervalSeconds { get; set; } = 60;
    public int ReconciliationBatchSize { get; set; } = 100;
    public int ReconciliationIntervalSeconds { get; set; } = 60;
    public string SourceTimeZoneId { get; set; } = "Africa/Cairo";
    public decimal SplitWeightToTargetFactor { get; set; } = 1000m;
    public decimal DispenseTolerancePct { get; set; } = 10m;
    public bool InitializeMissingRealtimeCheckpoints { get; set; } = true;
    public bool InitializeMissingSapCheckpoints { get; set; } = true;
    public string CustomerCode { get; set; } = "EIPICO";
    public string CustomerName { get; set; } = "Eipico";
    public string FactoryCode { get; set; } = "EIPICO";
    public string FactoryName { get; set; } = "Eipico";
    public string DefaultSkuUom { get; set; } = "EA";
    public string DefaultMaterialUom { get; set; } = "g";
}
