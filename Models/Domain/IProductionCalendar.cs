namespace РасчетВыплатЗарплаты.Models.Domain;

public interface IProductionCalendar
{
    int GetTotalWorkingDays(int year, int month);
    int GetWorkingDays(int year, int month, int fromDay, int toDay);
    bool IsWorkingDay(DateTime date);
    DateTime GetNearestWorkingDayBefore(DateTime date);
    decimal GetAverageMonthlyWorkDaysPerYear(int year);
}
