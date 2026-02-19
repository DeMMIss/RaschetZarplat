using РасчетВыплатЗарплаты.Models;

namespace РасчетВыплатЗарплаты.Models.Domain;

public class HolidayWorkPayment
{
    private readonly IProductionCalendar _calendar;
    private readonly INdflCalculator _ndflCalculator;

    public HolidayWorkPayment(IProductionCalendar calendar, INdflCalculator ndflCalculator)
    {
        _calendar = calendar;
        _ndflCalculator = ndflCalculator;
    }

    public PaymentRecord? Calculate(
        int year,
        int month,
        List<DateTime> holidayDates,
        SalaryInput input,
        AccumulatedIncomeTracker incomeTracker,
        DateTime settlementDate,
        DateTime endDate)
    {
        if (holidayDates.Count == 0 || settlementDate > endDate)
        {
            return null;
        }

        var totalHolidayGross = CalculateHolidayWorkGross(holidayDates, input, year, month);
        if (totalHolidayGross == 0)
        {
            return null;
        }

        var accumulatedIncome = incomeTracker.GetAccumulatedIncome(settlementDate.Year);
        var netAmount = _ndflCalculator.CalculateNetAmount(totalHolidayGross, accumulatedIncome);
        var delayDays = Math.Max(0, (endDate - settlementDate).Days);

        var record = new PaymentRecord
        {
            Year = year,
            Month = month,
            Type = PaymentType.HolidayWork,
            PaymentDate = settlementDate,
            GrossAmount = totalHolidayGross,
            NetAmount = netAmount,
            IndexedGrossAmount = totalHolidayGross,
            IndexedNetAmount = netAmount,
            Underpayment = 0,
            DelayDays = delayDays
        };

        incomeTracker.AddIncome(settlementDate.Year, totalHolidayGross);
        
        return record;
    }

    private decimal CalculateHolidayWorkGross(List<DateTime> holidayDates, SalaryInput input, int year, int month)
    {
        decimal totalGross = 0;

        foreach (var holidayDate in holidayDates)
        {
            var holidayBaseGrossSalary = input.GetGrossSalaryForDate(holidayDate);
            var dailyRate = CalculateDailyRate(input, year, month, holidayBaseGrossSalary);
            totalGross += Math.Round(dailyRate * 2, 2);
        }

        return totalGross;
    }

    public decimal CalculateDailyRate(SalaryInput input, int year, int month, decimal holidayBaseGrossSalary)
    {
        if (input.HolidayWorkDailyRateMethod == HolidayWorkDailyRateMethod.AverageMonthlyWorkDaysPerYear)
        {
            var avgMonthlyWorkDays = _calendar.GetAverageMonthlyWorkDaysPerYear(year);
            return avgMonthlyWorkDays > 0 
                ? Math.Round(holidayBaseGrossSalary / avgMonthlyWorkDays, 2) 
                : 0;
        }

        var totalWorkDays = _calendar.GetTotalWorkingDays(year, month);
        return totalWorkDays > 0 
            ? Math.Round(holidayBaseGrossSalary / totalWorkDays, 2) 
            : 0;
    }
}
