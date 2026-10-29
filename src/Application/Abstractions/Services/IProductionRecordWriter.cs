using Domain.Entities.Scada;

namespace Application.Abstractions.Services;

public interface IProductionRecordWriter
{
    // Manual commands carry forward the latest measurements without counting them again.
    Task<MachineState> AppendAsync(
        int machineId,
        MachineState reading,
        string source,
        string? notes = null,
        bool manualStateOnly = false,
        CancellationToken cancellationToken = default);
}
