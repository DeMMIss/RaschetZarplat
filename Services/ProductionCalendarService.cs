using System.Text.Json;

namespace РасчетЗадолженностиЗП.Services;

public class ProductionCalendarService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<int, HashSet<DateTime>> _nonWorkingDays = new();

    public ProductionCalendarService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task Load(int fromYear, int toYear)
    {
        for (int year = fromYear; year <= toYear; year++)
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
                        if (int.TryParse(cleaned, out int day))
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

        return !_nonWorkingDays[date.Year].Contains(date.Date);
    }

    public int GetWorkingDays(int year, int month, int fromDay, int toDay)
    {
        int count = 0;
        int daysInMonth = DateTime.DaysInMonth(year, month);
        int actualToDay = Math.Min(toDay, daysInMonth);

        for (int d = fromDay; d <= actualToDay; d++)
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
