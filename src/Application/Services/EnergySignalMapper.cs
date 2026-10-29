using Domain.Entities.Analytics;
using Domain.Entities.IoT;
namespace Application.Services;

public static class EnergySignalMapper
{
    public static readonly string[] RequiredTags = { "V1", "V2", "V3", "I1", "I2", "I3", "Pf1", "Pf2", "Pf3", "Pact1", "Pact2", "Pact3", "Preact1", "Preact2", "Preact3", "Papp1", "Papp2", "Papp3", "Energy1", "Energy2", "Energy3", "THDv1", "THDv2", "THDv3", "THDi1", "THDi2", "THDi3" };
    public static bool IsComplete(IReadOnlyDictionary<string, TelemetryTag> tags)
        => RequiredTags.All(tags.ContainsKey);
    public static EnergyReading Create(IReadOnlyDictionary<string, TelemetryTag> tags, DateTime timestamp)
        => new()
        {
            Timestamp = timestamp, Quality = null,
            VoltageL1 = tags["V1"].ValueNum,
            VoltageL2 = tags["V2"].ValueNum,
            VoltageL3 = tags["V3"].ValueNum,
            CurrentL1 = tags["I1"].ValueNum,
            CurrentL2 = tags["I2"].ValueNum,
            CurrentL3 = tags["I3"].ValueNum,
            PowerFactorL1 = tags["Pf1"].ValueNum,
            PowerFactorL2 = tags["Pf2"].ValueNum,
            PowerFactorL3 = tags["Pf3"].ValueNum,
            ActivePowerL1 = tags["Pact1"].ValueNum,
            ActivePowerL2 = tags["Pact2"].ValueNum,
            ActivePowerL3 = tags["Pact3"].ValueNum,
            ReactivePowerL1 = tags["Preact1"].ValueNum,
            ReactivePowerL2 = tags["Preact2"].ValueNum,
            ReactivePowerL3 = tags["Preact3"].ValueNum,
            ApparentPowerL1 = tags["Papp1"].ValueNum,
            ApparentPowerL2 = tags["Papp2"].ValueNum,
            ApparentPowerL3 = tags["Papp3"].ValueNum,
            EnergyL1 = tags["Energy1"].ValueNum,
            EnergyL2 = tags["Energy2"].ValueNum,
            EnergyL3 = tags["Energy3"].ValueNum,
            VoltageThdL1 = tags["THDv1"].ValueNum,
            VoltageThdL2 = tags["THDv2"].ValueNum,
            VoltageThdL3 = tags["THDv3"].ValueNum,
            CurrentThdL1 = tags["THDi1"].ValueNum,
            CurrentThdL2 = tags["THDi2"].ValueNum,
            CurrentThdL3 = tags["THDi3"].ValueNum,
        };
}
