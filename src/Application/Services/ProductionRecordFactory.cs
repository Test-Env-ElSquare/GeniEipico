using Domain.Entities.Scada;

namespace Application.Services;

public static class ProductionRecordFactory
{
    public static MachineState Create(MachineState reading, MachineState? previous, bool manualStateOnly)
    {
        var measurements = manualStateOnly ? previous ?? reading : reading;
        return new MachineState
        {
            MachineId = reading.MachineId,
            CustomerId = reading.CustomerId,
            FactoryId = reading.FactoryId,
            LineId = reading.LineId,
            Timestamp = reading.Timestamp,
            ShiftStartTime = reading.ShiftStartTime,
            State = reading.State,
            Speed = measurements.Speed,
            Count = measurements.Count,
            CountDiff = manualStateOnly || previous is null
                ? 0m
                : Math.Max(0m, (decimal)measurements.Count - previous.Count),
            Temp = measurements.Temp,
            // Qreject is a cumulative counter like Count. Manual state rows keep
            // its last value as the baseline, but never report another increment.
            QReject = measurements.QReject,
            QRejectDiff = manualStateOnly || previous is null
                ? 0m
                : Math.Max(0m, (decimal)measurements.QReject - previous.QReject),
            StateReason = measurements.StateReason,
            Sku = measurements.Sku,
            Fault = measurements.Fault
        };
    }

    public static string StateLabel(MachineState record)
        => record.Fault != 0 ? "fault" : record.State switch
        {
            1 => "running",
            0 => "idle",
            2 => "offline",
            3 => "maintenance",
            _ => "unknown"
        };
}
