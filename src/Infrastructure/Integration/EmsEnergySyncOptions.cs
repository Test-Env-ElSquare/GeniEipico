namespace Infrastructure.Integration;

public sealed class EmsEnergySyncOptions
{
    public const string SectionName = "EmsEnergySync";

    public string ExpectedSourceDatabase { get; set; } = "EMS";
    public int PollIntervalSeconds { get; set; } = 5;
    public int DefinitionsSyncIntervalSeconds { get; set; } = 300;
    public int BatchSize { get; set; } = 100;
    public string SourceTimeZoneId { get; set; } = "Africa/Cairo";
}
