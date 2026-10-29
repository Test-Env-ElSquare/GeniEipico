using Microsoft.Extensions.Options;

namespace Infrastructure.Integration;

/// <summary>
/// Converts Eipico SQL datetime values, which are stored in the plant's local
/// clock, to the UTC convention used by Genie entities and time filters.
/// </summary>
internal sealed class EipicoSourceTime
{
    private readonly TimeZoneInfo sourceTimeZone;

    public EipicoSourceTime(IOptions<EipicoSyncOptions> configuredOptions)
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

    public DateTime? ToUtc(DateTime? value)
        => value.HasValue ? ToUtc(value.Value) : null;

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        var configured = string.IsNullOrWhiteSpace(timeZoneId)
            ? "Africa/Cairo"
            : timeZoneId.Trim();

        if (TryFind(configured, out var sourceTimeZone))
            return sourceTimeZone;

        var fallback = configured.Equals("Africa/Cairo", StringComparison.OrdinalIgnoreCase)
            ? "Egypt Standard Time"
            : configured.Equals("Egypt Standard Time", StringComparison.OrdinalIgnoreCase)
                ? "Africa/Cairo"
                : null;
        if (fallback is not null && TryFind(fallback, out sourceTimeZone))
            return sourceTimeZone;

        throw new InvalidOperationException(
            $"Eipico source time zone '{configured}' is not available on this server.");
    }

    private static bool TryFind(string timeZoneId, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = null!;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = null!;
            return false;
        }
    }
}
