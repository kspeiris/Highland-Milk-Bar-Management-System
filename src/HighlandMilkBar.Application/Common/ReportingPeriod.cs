namespace HighlandMilkBar.Application.Common;

/// <summary>Preset reporting periods shared across analytics screens.</summary>
public enum ReportingPreset
{
    Today,
    Yesterday,
    ThisWeek,
    LastWeek,
    ThisMonth,
    LastMonth,
    ThisYear,
    LastYear
}

/// <summary>
/// An inclusive-start / exclusive-end reporting window using local business dates.
/// The immediately preceding equivalent window is available for period comparisons.
/// </summary>
public sealed class ReportingPeriod
{
    public ReportingPreset Preset { get; init; }
    public DateTime Start { get; init; }
    public DateTime EndExclusive { get; init; }
    public string Label { get; init; } = string.Empty;

    public int LengthDays => Math.Max(1, (int)(EndExclusive - Start).TotalDays);

    public static ReportingPeriod FromPreset(ReportingPreset preset, DateTime? now = null)
    {
        var today = (now ?? DateTime.Now).Date;
        var (start, end) = preset switch
        {
            ReportingPreset.Today => (today, today.AddDays(1)),
            ReportingPreset.Yesterday => (today.AddDays(-1), today),
            ReportingPreset.ThisWeek => (WeekStart(today), WeekStart(today).AddDays(7)),
            ReportingPreset.LastWeek => (WeekStart(today).AddDays(-7), WeekStart(today)),
            ReportingPreset.ThisMonth => (MonthStart(today), MonthStart(today).AddMonths(1)),
            ReportingPreset.LastMonth => (MonthStart(today).AddMonths(-1), MonthStart(today)),
            ReportingPreset.ThisYear => (new DateTime(today.Year, 1, 1), new DateTime(today.Year + 1, 1, 1)),
            ReportingPreset.LastYear => (new DateTime(today.Year - 1, 1, 1), new DateTime(today.Year, 1, 1)),
            _ => (MonthStart(today), MonthStart(today).AddMonths(1))
        };

        var label = start == end.AddDays(-1) && preset is ReportingPreset.Today or ReportingPreset.Yesterday
            ? start.ToString("dd MMM yyyy")
            : $"{start:dd MMM yyyy} – {end.AddDays(-1):dd MMM yyyy}";

        return new ReportingPeriod
        {
            Preset = preset,
            Start = start,
            EndExclusive = end,
            Label = label
        };
    }

    /// <summary>The immediately preceding window of equal length (for comparisons).</summary>
    public ReportingPeriod Previous()
    {
        var length = EndExclusive - Start;
        var prevStart = Start - length;
        return new ReportingPeriod
        {
            Preset = Preset,
            Start = prevStart,
            EndExclusive = Start,
            Label = $"{prevStart:dd MMM yyyy} – {Start.AddDays(-1):dd MMM yyyy}"
        };
    }

    public static ReportingPeriod Custom(DateTime start, DateTime endExclusive)
    {
        if (endExclusive <= start) endExclusive = start.AddDays(1);
        return new ReportingPeriod
        {
            Preset = ReportingPreset.ThisMonth,
            Start = start.Date,
            EndExclusive = endExclusive.Date,
            Label = $"{start:dd MMM yyyy} – {endExclusive.AddDays(-1):dd MMM yyyy}"
        };
    }

    private static DateTime WeekStart(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    private static DateTime MonthStart(DateTime date) => new(date.Year, date.Month, 1);
}