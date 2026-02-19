namespace РасчетВыплатЗарплаты.Models.Domain;

public class WorkingDaysCalculator
{
    public int CalculateEffectiveDays(int year, int month, SalaryInput input, IProductionCalendar calendar)
    {
        var totalDays = calendar.GetTotalWorkingDays(year, month);
        var excludedDays = CountExcludedDays(year, month, input, calendar);
        return Math.Max(0, totalDays - excludedDays);
    }

    public int CalculateEffectiveDaysInRange(int year, int month, int fromDay, int toDay, SalaryInput input, IProductionCalendar calendar)
    {
        var totalDays = calendar.GetWorkingDays(year, month, fromDay, toDay);
        var excludedDays = CountExcludedDaysInRange(year, month, fromDay, toDay, input, calendar);
        return Math.Max(0, totalDays - excludedDays);
    }

    private int CountExcludedDays(int year, int month, SalaryInput input, IProductionCalendar calendar)
    {
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        return CountExcludedDaysInRange(year, month, monthStart, monthEnd, input, calendar);
    }

    private int CountExcludedDaysInRange(int year, int month, int fromDay, int toDay, SalaryInput input, IProductionCalendar calendar)
    {
        var rangeStart = new DateTime(year, month, fromDay);
        var rangeEnd = new DateTime(year, month, Math.Min(toDay, DateTime.DaysInMonth(year, month)));
        return CountExcludedDaysInRange(year, month, rangeStart, rangeEnd, input, calendar);
    }

    private int CountExcludedDaysInRange(int year, int month, DateTime rangeStart, DateTime rangeEnd, SalaryInput input, IProductionCalendar calendar)
    {
        var count = 0;

        foreach (var sickLeave in input.SickLeaves)
        {
            count += CountWorkingDaysInOverlap(sickLeave.From, sickLeave.To, rangeStart, rangeEnd, calendar);
        }

        foreach (var vacation in input.Vacations)
        {
            count += CountWorkingDaysInOverlap(vacation.From, vacation.To, rangeStart, rangeEnd, calendar);
        }

        return count;
    }

    private int CountWorkingDaysInOverlap(DateTime periodStart, DateTime periodEnd, DateTime rangeStart, DateTime rangeEnd, IProductionCalendar calendar)
    {
        var overlapStart = periodStart > rangeStart ? periodStart : rangeStart;
        var overlapEnd = periodEnd < rangeEnd ? periodEnd : rangeEnd;

        if (overlapStart > overlapEnd)
        {
            return 0;
        }

        var count = 0;
        for (var date = overlapStart.Date; date <= overlapEnd.Date; date = date.AddDays(1))
        {
            if (calendar.IsWorkingDay(date))
            {
                count++;
            }
        }

        return count;
    }
}
