using РасчетВыплатЗарплаты.Models;

namespace РасчетВыплатЗарплаты.Models.Domain;

public class MonthlyPayment
{
    private readonly IProductionCalendar _calendar;
    private readonly INdflCalculator _ndflCalculator;
    private readonly WorkingDaysCalculator _daysCalculator;

    public MonthlyPayment(IProductionCalendar calendar, INdflCalculator ndflCalculator, WorkingDaysCalculator daysCalculator)
    {
        _calendar = calendar;
        _ndflCalculator = ndflCalculator;
        _daysCalculator = daysCalculator;
    }

    public PaymentRecord? CalculateAdvance(
        int year,
        int month,
        SalaryInput input,
        AccumulatedIncomeTracker incomeTracker,
        DateTime endDate)
    {
        var advanceDate = GetPaymentDate(year, month, input.AdvancePayDay);
        if (advanceDate > endDate)
        {
            return null;
        }

        var effectiveWorkDays = _daysCalculator.CalculateEffectiveDays(year, month, input, _calendar);
        var firstHalfEffectiveDays = _daysCalculator.CalculateEffectiveDaysInRange(year, month, 1, 14, input, _calendar);
        
        if (effectiveWorkDays == 0 || firstHalfEffectiveDays <= 0)
        {
            return null;
        }

        var monthDate = new DateTime(year, month, 15);
        var baseGrossSalary = input.GetGrossSalaryForDate(monthDate);
        var advanceGross = Math.Round(baseGrossSalary / effectiveWorkDays * firstHalfEffectiveDays, 2);
        var accumulatedIncome = incomeTracker.GetAccumulatedIncome(advanceDate.Year);
        
        var record = CreatePaymentRecord(year, month, PaymentType.Advance, advanceDate, advanceGross, endDate, accumulatedIncome);
        incomeTracker.AddIncome(advanceDate.Year, advanceGross);
        
        return record;
    }

    public PaymentRecord? CalculateSettlement(
        int year,
        int month,
        SalaryInput input,
        AccumulatedIncomeTracker incomeTracker,
        DateTime endDate)
    {
        var normalSettlementDate = GetSettlementPaymentDate(year, month, input.SettlementPayDay);
        var settlementDate = normalSettlementDate;

        if (input.DismissalDate.HasValue)
        {
            var dismissalDate = input.DismissalDate.Value.Date;
            var monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            
            if (dismissalDate > monthEnd && dismissalDate <= normalSettlementDate)
            {
                settlementDate = dismissalDate;
            }
        }

        if (settlementDate > endDate)
        {
            return null;
        }

        var effectiveWorkDays = _daysCalculator.CalculateEffectiveDays(year, month, input, _calendar);
        var secondHalfEffectiveDays = _daysCalculator.CalculateEffectiveDaysInRange(year, month, 15, DateTime.DaysInMonth(year, month), input, _calendar);
        
        if (effectiveWorkDays == 0 || secondHalfEffectiveDays <= 0)
        {
            return null;
        }

        var monthDate = new DateTime(year, month, 15);
        var baseGrossSalary = input.GetGrossSalaryForDate(monthDate);
        var settlementGross = Math.Round(baseGrossSalary / effectiveWorkDays * secondHalfEffectiveDays, 2);
        var accumulatedIncome = incomeTracker.GetAccumulatedIncome(settlementDate.Year);
        
        var record = CreatePaymentRecord(year, month, PaymentType.Settlement, settlementDate, settlementGross, endDate, accumulatedIncome);
        incomeTracker.AddIncome(settlementDate.Year, settlementGross);
        
        return record;
    }

    private DateTime GetPaymentDate(int year, int month, int day)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var actualDay = Math.Min(day, daysInMonth);
        var date = new DateTime(year, month, actualDay);
        return _calendar.GetNearestWorkingDayBefore(date);
    }

    private DateTime GetSettlementPaymentDate(int year, int month, int day)
    {
        var nextMonth = new DateTime(year, month, 1).AddMonths(1);
        var daysInNextMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
        var actualDay = Math.Min(day, daysInNextMonth);
        var date = new DateTime(nextMonth.Year, nextMonth.Month, actualDay);
        return _calendar.GetNearestWorkingDayBefore(date);
    }

    private PaymentRecord CreatePaymentRecord(
        int year,
        int month,
        PaymentType type,
        DateTime paymentDate,
        decimal grossAmount,
        DateTime calculationDate,
        decimal accumulatedIncome)
    {
        var netAmount = _ndflCalculator.CalculateNetAmount(grossAmount, accumulatedIncome);
        var delayDays = Math.Max(0, (calculationDate - paymentDate).Days);

        return new PaymentRecord
        {
            Year = year,
            Month = month,
            Type = type,
            PaymentDate = paymentDate,
            GrossAmount = grossAmount,
            NetAmount = netAmount,
            IndexedGrossAmount = grossAmount,
            IndexedNetAmount = netAmount,
            Underpayment = 0,
            DelayDays = delayDays
        };
    }
}
