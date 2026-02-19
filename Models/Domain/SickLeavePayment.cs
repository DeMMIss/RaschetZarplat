using РасчетВыплатЗарплаты.Models;

namespace РасчетВыплатЗарплаты.Models.Domain;

public class SickLeavePayment
{
    private readonly IProductionCalendar _calendar;
    private readonly INdflCalculator _ndflCalculator;

    public SickLeavePayment(IProductionCalendar calendar, INdflCalculator ndflCalculator)
    {
        _calendar = calendar;
        _ndflCalculator = ndflCalculator;
    }

    public PaymentRecord? Calculate(
        SickLeavePeriod period,
        SalaryInput input,
        DateTime calculationDate,
        AccumulatedIncomeTracker incomeTracker)
    {
        if (period.To > calculationDate)
        {
            return null;
        }

        var paymentDate = GetSettlementPaymentDate(period.To.Year, period.To.Month, input.SettlementPayDay);
        if (paymentDate > calculationDate)
        {
            paymentDate = calculationDate;
        }

        var year = paymentDate.Year;
        var accumulatedIncome = incomeTracker.GetAccumulatedIncome(year);
        var grossAmount = _ndflCalculator.CalculateGrossFromNet(period.Amount, accumulatedIncome);
        var netAmount = _ndflCalculator.CalculateNetAmount(grossAmount, accumulatedIncome);
        
        var record = new PaymentRecord
        {
            Year = period.To.Year,
            Month = period.To.Month,
            Type = PaymentType.SickLeave,
            PaymentDate = paymentDate,
            GrossAmount = grossAmount,
            NetAmount = netAmount,
            IndexedGrossAmount = grossAmount,
            IndexedNetAmount = netAmount,
            Underpayment = 0,
            DelayDays = Math.Max(0, (calculationDate - paymentDate).Days)
        };

        incomeTracker.AddIncome(year, grossAmount);
        
        return record;
    }

    private DateTime GetSettlementPaymentDate(int year, int month, int day)
    {
        var nextMonth = new DateTime(year, month, 1).AddMonths(1);
        var daysInNextMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
        var actualDay = Math.Min(day, daysInNextMonth);
        var date = new DateTime(nextMonth.Year, nextMonth.Month, actualDay);
        return _calendar.GetNearestWorkingDayBefore(date);
    }
}
