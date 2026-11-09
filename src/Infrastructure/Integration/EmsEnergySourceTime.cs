using Microsoft.Extensions.Options;

namespace Infrastructure.Integration;

public sealed class EmsEnergySourceTime
{
    private readonly TimeZoneInfo sourceTimeZone;

    public EmsEnergySourceTime(IOptions<EmsEnergySyncOptions> configuredOptions)
    {
        sourceTimeZone = ResolveTimeZone(configuredOptions.Value.SourceTimeZoneId);
    }

    public DateTime ToUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return value;

        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
            sourceTimeZone);
    }

    private static TimeZoneInfo ResolveTimeZone(string configuredId)
    {
        var candidates = new[]
        {
            configuredId,
            "Egypt Standard Time",
            "Africa/Cairo"
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException(
            $"EMS source time zone '{configuredId}' could not be resolved.");
    }
}
