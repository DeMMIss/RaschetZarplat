using РасчетВыплатЗарплаты.Models;

namespace РасчетВыплатЗарплаты.Models.Domain;

public class PaymentCalculation
{
    public SalaryInput Input { get; }
    public AccumulatedIncomeTracker IncomeTracker { get; }
    public List<PaymentRecord> Records { get; }
    public DateTime StartDate { get; }
    public DateTime EndDate { get; }

    private readonly WorkingDaysCalculator _daysCalculator;
    private readonly MonthlyPayment _monthlyPayment;
    private readonly HolidayWorkPayment _holidayWorkPayment;
    private readonly SickLeavePayment _sickLeavePayment;
    private readonly VacationPayment _vacationPayment;
    private readonly PaymentIndexation _paymentIndexation;

    public PaymentCalculation(
        SalaryInput input,
        IProductionCalendar calendar,
        INdflCalculator ndflCalculator)
    {
        Input = input;
        IncomeTracker = new AccumulatedIncomeTracker();
        Records = new List<PaymentRecord>();
        StartDate = DetermineStartDate(input);
        EndDate = input.CalculationDate;
        
        _daysCalculator = new WorkingDaysCalculator();
        _monthlyPayment = new MonthlyPayment(calendar, ndflCalculator, _daysCalculator);
        _holidayWorkPayment = new HolidayWorkPayment(calendar, ndflCalculator);
        _sickLeavePayment = new SickLeavePayment(calendar, ndflCalculator);
        _vacationPayment = new VacationPayment(calendar, ndflCalculator);
        _paymentIndexation = new PaymentIndexation(ndflCalculator);
    }

    public List<PaymentRecord> CalculateAll()
    {
        var current = StartDate;
        while (current <= EndDate)
        {
            CalculateMonthlyPayments(current.Year, current.Month);
            current = current.AddMonths(1);
        }

        CalculatePeriodPayments();

        if (Input.CalculateIndexationUnderpayments)
        {
            ApplyIndexationIfNeeded();
        }

        return Records.OrderBy(r => r.PaymentDate).ThenBy(r => r.Type).ToList();
    }

    public void CalculateMonthlyPayments(int year, int month)
    {
        var advanceRecord = _monthlyPayment.CalculateAdvance(
            year, month, Input, IncomeTracker, EndDate);
        if (advanceRecord != null)
        {
            Records.Add(advanceRecord);
        }

        var settlementRecord = _monthlyPayment.CalculateSettlement(
            year, month, Input, IncomeTracker, EndDate);
        
        if (settlementRecord != null)
        {
            Records.Add(settlementRecord);
            
            var monthHolidays = Input.HolidayWorkDates
                .Where(d => d.Year == year && d.Month == month && d >= StartDate && d <= EndDate)
                .ToList();

            if (monthHolidays.Count > 0)
            {
                var holidayRecord = _holidayWorkPayment.Calculate(
                    year, month, monthHolidays, Input, IncomeTracker,
                    settlementRecord.PaymentDate, EndDate);
                if (holidayRecord != null)
                {
                    Records.Add(holidayRecord);
                }
            }
        }
    }

    public void CalculatePeriodPayments()
    {
        foreach (var sickLeave in Input.SickLeaves)
        {
            var record = _sickLeavePayment.Calculate(
                sickLeave, Input, EndDate, IncomeTracker);
            if (record != null)
            {
                Records.Add(record);
            }
        }

        foreach (var vacation in Input.Vacations)
        {
            var record = _vacationPayment.Calculate(
                vacation, Input, EndDate, IncomeTracker);
            if (record != null)
            {
                Records.Add(record);
            }
        }
    }

    public void ApplyIndexationIfNeeded()
    {
        _paymentIndexation.ApplyIndexation(Records, Input);
    }

    public static DateTime DetermineStartDate(SalaryInput input)
    {
        if (input.HireDate.HasValue)
        {
            return new DateTime(input.HireDate.Value.Year, input.HireDate.Value.Month, 1);
        }
        
        var unperformedIndexations = input.IndexationRules.Where(r => !r.IsPerformed).ToList();
        if (unperformedIndexations.Count > 0)
        {
            var firstIndexation = unperformedIndexations.OrderBy(r => r.Date).First();
            return new DateTime(firstIndexation.Date.Year, firstIndexation.Date.Month, 1);
        }

        if (input.BaseIndexationDate.HasValue)
        {
            return new DateTime(input.BaseIndexationDate.Value.Year, input.BaseIndexationDate.Value.Month, 1);
        }

        return new DateTime(input.CalculationDate.Year, input.CalculationDate.Month, 1).AddMonths(-12);
    }
}
