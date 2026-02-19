namespace LBElectronica.Server.Services;

public class DateRangeService
{
    public (DateTime start, DateTime end) Resolve(DateTime? startDate, DateTime? endDate, string? preset)
    {
        var now = DateTime.Now;

        if (!string.IsNullOrWhiteSpace(preset))
        {
            switch (preset.ToLowerInvariant())
            {
                case "today":
                    return (now.Date, now.Date.AddDays(1).AddTicks(-1));
                case "month":
                case "thismonth":
                    var monthStart = new DateTime(now.Year, now.Month, 1);
                    var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
                    return (monthStart, monthEnd);
            }
        }

        var start = startDate?.Date ?? now.Date;
        var end = endDate?.Date.AddDays(1).AddTicks(-1) ?? now.Date.AddDays(1).AddTicks(-1);
        return (start, end);
    }
}
