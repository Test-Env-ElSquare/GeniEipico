using System.Text.Json.Serialization;

namespace Application.Abstractions.Services;

public interface ISignageLayoutService
{
    Task<IReadOnlyList<SignageLineSignals>> GetFactorySignalsAsync(ScadaLiveViewScope scope);
    IReadOnlyList<SignageLineDefinition> GetFactoryLines(ScadaLiveViewScope scope);
    IReadOnlyList<SignageMachineDefinition> GetMachines(ScadaLiveViewScope scope);
    IReadOnlyList<SignageScaleStatus> GetScales(ScadaLiveViewScope scope);
    IReadOnlyList<SignageCurrentBatch> GetCurrentBatches(ScadaLiveViewScope scope);
}

public sealed record SignageLineSignals(int LineId, int MachineCount, IReadOnlyList<SignageMachineSignal> Machines);
public sealed record SignageMachineSignal(string MachineName, DateTime ShiftStartTime, decimal TotalCountDiff,
    DateTime LatestTimeStamp, SignageLatestSignal LatestSignal);
public sealed record SignageLatestSignal(int Id, string MachineName, int State, decimal Count,
    int Qreject, int Speed, string? StateType, string StateReason, string Sku, decimal CountDiff,
    DateTime TimeStamp, DateTime ShiftStartTime, int MachineId, int FactoryId, int LineId,
    int Fault, string? Functionality, decimal QrejectDiff);
public sealed record SignageLineDefinition(int Id, string Name, string? AliasName, int Number,
    int FactoryId, string? Type, string? Area, IReadOnlyList<SignageMachineDefinition> Lines);
public sealed record SignageMachineDefinition(int Id, string Name, int LineId, string Faunctionality,
    int? RatedSpeed, bool IsDominant, [property: JsonPropertyName("uid")] string? Uid = null);
public sealed record SignageScaleStatus(int ScaleId, string ScaleName, string? Room, int FactoryId,
    DateTime? LastSignalTime);
public sealed record SignageCurrentBatch(int LineId, string? SKUName, string? JobOrderId, string BatchId,
    DateTime? ActivationDate, DateTime? FinishingDate, int? Count, int MachineId, string MachineName);