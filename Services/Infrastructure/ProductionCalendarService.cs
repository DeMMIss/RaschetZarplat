using System.Net.Http;
using System.Text.Json;
using РасчетВыплатЗарплаты.Models.Domain;

namespace РасчетВыплатЗарплаты.Services.Infrastructure;

public class ProductionCalendarService : IProductionCalendar
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<int, HashSet<DateTime>> _nonWorkingDays = new();

    public ProductionCalendarService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task Load(int fromYear, int toYear)
    {
        for (var year = fromYear; year <= toYear; year++)
        {
            if (_nonWorkingDays.ContainsKey(year))
                continue;

            var url = $"https://xmlcalendar.ru/data/ru/{year}/calendar.json";
            var json = await _httpClient.GetStringAsync(url);
            var calendar = JsonSerializer.Deserialize<CalendarJson>(json);

            var holidays = new HashSet<DateTime>();

            if (calendar?.months != null)
            {
                foreach (var month in calendar.months)
                {
                    var dayStrings = month.days.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var dayStr in dayStrings)
                    {
                        var cleaned = dayStr.Trim().TrimEnd('*', '+');
                        if (int.TryParse(cleaned, out var day))
                        {
                            if (dayStr.Trim().Contains('*'))
                                continue;

                            holidays.Add(new DateTime(year, month.month, day));
                        }
                    }
                }
            }

            _nonWorkingDays[year] = holidays;
        }
    }

    public bool IsWorkingDay(DateTime date)
    {
        if (!_nonWorkingDays.ContainsKey(date.Year))
            throw new InvalidOperationException($"Производственный календарь для {date.Year} года не загружен.");

        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            return false;

        return !_nonWorkingDays[date.Year].Contains(date.Date);
    }

    public int GetWorkingDays(int year, int month, int fromDay, int toDay)
    {
        var count = 0;
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var actualToDay = Math.Min(toDay, daysInMonth);

        for (var d = fromDay; d <= actualToDay; d++)
        {
            var date = new DateTime(year, month, d);
            if (IsWorkingDay(date))
                count++;
        }

        return count;
    }

    public int GetTotalWorkingDays(int year, int month)
    {
        return GetWorkingDays(year, month, 1, DateTime.DaysInMonth(year, month));
    }

    public int GetFirstHalfWorkingDays(int year, int month)
    {
        return GetWorkingDays(year, month, 1, 14);
    }

    public int GetSecondHalfWorkingDays(int year, int month)
    {
        return GetWorkingDays(year, month, 15, DateTime.DaysInMonth(year, month));
    }

    public DateTime GetNearestWorkingDayBefore(DateTime date)
    {
        var current = date.Date;
        while (!IsWorkingDay(current))
        {
            current = current.AddDays(-1);
        }
        return current;
    }

    public decimal GetAverageMonthlyWorkDaysPerYear(int year)
    {
        var totalWorkDays = 0;
        var monthsCount = 0;
        
        for (int month = 1; month <= 12; month++)
        {
            var workDays = GetTotalWorkingDays(year, month);
            if (workDays > 0)
            {
                totalWorkDays += workDays;
                monthsCount++;
            }
        }
        
        return monthsCount > 0 ? (decimal)totalWorkDays / monthsCount : 0;
    }

    private class CalendarJson
    {
        public int year { get; set; }
        public List<MonthJson>? months { get; set; }
    }

    private class MonthJson
    {
        public int month { get; set; }
        public string days { get; set; } = string.Empty;
    }
}
