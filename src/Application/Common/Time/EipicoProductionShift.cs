namespace Application.Common.Time;

/// <summary>Eipico production shifts start at 08:00 and 20:00 in the plant's local clock.</summary>
public static class EipicoProductionShift
{
    public static DateTime ResolveUtc(DateTime timestampUtc, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc), zone);
        var start = local.Hour < 8
            ? local.Date.AddDays(-1).AddHours(20)
            : local.Date.AddHours(local.Hour < 20 ? 8 : 20);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(start, DateTimeKind.Unspecified), zone);
    }
}