using РасчетЗадолженностиЗП.Models;

namespace РасчетЗадолженностиЗП.Services;

public class SalaryCalculationService
{
    private const decimal NdflRate = 0.13m;

    private readonly ProductionCalendarService _calendar;

    public SalaryCalculationService(ProductionCalendarService calendar)
    {
        _calendar = calendar;
    }

    public List<PaymentRecord> Calculate(SalaryInput input)
    {
        var records = new List<PaymentRecord>();

        var startDate = new DateTime(input.IndexationDate.Year, input.IndexationDate.Month, 1);
        var endDate = input.CalculationDate;

        var holidaysByMonth = input.HolidayWorkDates
            .Where(d => d >= startDate && d <= endDate)
            .GroupBy(d => new { d.Year, d.Month })
            .ToDictionary(g => g.Key, g => g.Count());

        var current = startDate;
        while (current <= endDate)
        {
            int year = current.Year;
            int month = current.Month;

            int totalWorkDays = _calendar.GetTotalWorkingDays(year, month);
            int firstHalfWorkDays = _calendar.GetFirstHalfWorkingDays(year, month);
            int secondHalfWorkDays = _calendar.GetSecondHalfWorkingDays(year, month);

            if (totalWorkDays == 0)
            {
                current = current.AddMonths(1);
                continue;
            }

            var advanceDate = GetPaymentDate(year, month, input.AdvancePayDay);
            if (advanceDate <= endDate)
            {
                var advanceGross = Math.Round(input.GrossSalary / totalWorkDays * firstHalfWorkDays, 2);
                var indexedAdvanceGross = Math.Round(input.IndexedGrossSalary / totalWorkDays * firstHalfWorkDays, 2);

                records.Add(CreateRecord(
                    year, month, PaymentType.Advance, advanceDate,
                    advanceGross, indexedAdvanceGross, endDate));
            }

            var settlementDate = GetSettlementPaymentDate(year, month, input.SettlementPayDay);
            if (settlementDate <= endDate)
            {
                var settlementGross = Math.Round(input.GrossSalary / totalWorkDays * secondHalfWorkDays, 2);
                var indexedSettlementGross = Math.Round(input.IndexedGrossSalary / totalWorkDays * secondHalfWorkDays, 2);

                records.Add(CreateRecord(
                    year, month, PaymentType.Settlement, settlementDate,
                    settlementGross, indexedSettlementGross, endDate));
            }

            var key = new { Year = year, Month = month };
            if (holidaysByMonth.TryGetValue(key, out int holidayDays) && settlementDate <= endDate)
            {
                var dailyRate = Math.Round(input.GrossSalary / totalWorkDays, 2);
                var indexedDailyRate = Math.Round(input.IndexedGrossSalary / totalWorkDays, 2);

                var holidayGross = Math.Round(dailyRate * 2 * holidayDays, 2);
                var indexedHolidayGross = Math.Round(indexedDailyRate * 2 * holidayDays, 2);

                records.Add(CreateRecord(
                    year, month, PaymentType.HolidayWork, settlementDate,
                    holidayGross, indexedHolidayGross, endDate));
            }

            current = current.AddMonths(1);
        }

        return records;
    }

    private PaymentRecord CreateRecord(
        int year, int month, PaymentType type, DateTime paymentDate,
        decimal grossAmount, decimal indexedGrossAmount, DateTime calculationDate)
    {
        var netAmount = ApplyNdfl(grossAmount);
        var indexedNetAmount = ApplyNdfl(indexedGrossAmount);
        var underpayment = Math.Round(indexedNetAmount - netAmount, 2);
        var delayDays = Math.Max(0, (calculationDate - paymentDate).Days);

        return new PaymentRecord
        {
            Year = year,
            Month = month,
            Type = type,
            PaymentDate = paymentDate,
            GrossAmount = grossAmount,
            NetAmount = netAmount,
            IndexedGrossAmount = indexedGrossAmount,
            IndexedNetAmount = indexedNetAmount,
            Underpayment = underpayment,
            DelayDays = delayDays
        };
    }

    private DateTime GetPaymentDate(int year, int month, int day)
    {
        int daysInMonth = DateTime.DaysInMonth(year, month);
        int actualDay = Math.Min(day, daysInMonth);
        var date = new DateTime(year, month, actualDay);
        return _calendar.GetNearestWorkingDayBefore(date);
    }

    private DateTime GetSettlementPaymentDate(int year, int month, int day)
    {
        var nextMonth = new DateTime(year, month, 1).AddMonths(1);
        int daysInNextMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
        int actualDay = Math.Min(day, daysInNextMonth);
        var date = new DateTime(nextMonth.Year, nextMonth.Month, actualDay);
        return _calendar.GetNearestWorkingDayBefore(date);
    }

    private static decimal ApplyNdfl(decimal gross)
    {
        return Math.Round(gross - Math.Round(gross * NdflRate, 0), 2);
    }
}
