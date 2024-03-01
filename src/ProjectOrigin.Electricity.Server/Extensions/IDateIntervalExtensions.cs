using System;

namespace ProjectOrigin.Electricity.Extensions;

public static class IDateIntervalExtensions
{
    public static TimeSpan GetTimeSpan(this V1.DateInterval dateInterval)
    {
        return dateInterval.End.ToDateTimeOffset() - dateInterval.Start.ToDateTimeOffset();
    }

    public static bool IsDateIntervalOverlapping(this V1.DateInterval first, V1.DateInterval second)
    {
        var aStart = first.Start.ToDateTimeOffset();
        var aEnd = first.End.ToDateTimeOffset();
        var bStart = second.Start.ToDateTimeOffset();
        var bEnd = second.End.ToDateTimeOffset();

        return (aStart >= bStart && aEnd <= bEnd) || (bStart >= aStart && bEnd <= aEnd);
    }
}
