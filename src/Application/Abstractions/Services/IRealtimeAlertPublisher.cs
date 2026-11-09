using Domain.Entities.Alarms;

namespace Application.Abstractions.Services;

public interface IRealtimeAlertPublisher
{
    Task PublishTriggeredAsync(
        AlarmEvent alarm,
        CancellationToken cancellationToken = default);
}
