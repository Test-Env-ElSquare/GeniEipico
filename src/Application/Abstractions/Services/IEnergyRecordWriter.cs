using Domain.Entities.Analytics;
namespace Application.Abstractions.Services;
public interface IEnergyRecordWriter
{
    Task AppendAsync(int transformerId, EnergyReading reading, CancellationToken cancellationToken);
}
