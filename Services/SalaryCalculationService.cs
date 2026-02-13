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

        var startDate = DetermineStartDate(input);
        var endDate = input.CalculationDate;

        var holidaysByMonth = input.HolidayWorkDates
            .Where(d => d >= startDate && d <= endDate)
            .GroupBy(d => new { d.Year, d.Month })
            .ToDictionary(g => g.Key, g => g.Count());

        var current = startDate;
        while (current <= endDate)
        {
            var year = current.Year;
            var month = current.Month;

            var effectiveWorkDays = GetEffectiveWorkingDays(year, month, input);
            var firstHalfEffectiveDays = GetEffectiveWorkingDaysInRange(year, month, 1, 14, input);
            var secondHalfEffectiveDays = GetEffectiveWorkingDaysInRange(year, month, 15, DateTime.DaysInMonth(year, month), input);

            if (effectiveWorkDays == 0)
            {
                current = current.AddMonths(1);
                continue;
            }

            var monthDate = new DateTime(year, month, 15);
            var baseGrossSalary = input.GetGrossSalaryForDate(monthDate);
            
            decimal indexedGrossSalary;
            var unperformedIndexations = input.IndexationRules.Where(r => !r.IsPerformed).ToList();
            if (unperformedIndexations.Count == 0)
            {
                indexedGrossSalary = baseGrossSalary;
            }
            else
            {
                var firstIndexationDate = unperformedIndexations.OrderBy(r => r.Date).First().Date;
                indexedGrossSalary = monthDate < firstIndexationDate
                    ? baseGrossSalary
                    : input.GetIndexedGrossSalary(monthDate);
            }

            var advanceDate = GetPaymentDate(year, month, input.AdvancePayDay);
            if (advanceDate <= endDate && firstHalfEffectiveDays > 0)
            {
                var advanceGross = Math.Round(baseGrossSalary / effectiveWorkDays * firstHalfEffectiveDays, 2);
                var indexedAdvanceGross = Math.Round(indexedGrossSalary / effectiveWorkDays * firstHalfEffectiveDays, 2);

                records.Add(CreateRecord(
                    year, month, PaymentType.Advance, advanceDate,
                    advanceGross, indexedAdvanceGross, endDate));
            }

            var settlementDate = GetSettlementPaymentDate(year, month, input.SettlementPayDay);
            if (settlementDate <= endDate && secondHalfEffectiveDays > 0)
            {
                var settlementGross = Math.Round(baseGrossSalary / effectiveWorkDays * secondHalfEffectiveDays, 2);
                var indexedSettlementGross = Math.Round(indexedGrossSalary / effectiveWorkDays * secondHalfEffectiveDays, 2);

                records.Add(CreateRecord(
                    year, month, PaymentType.Settlement, settlementDate,
                    settlementGross, indexedSettlementGross, endDate));
            }

            var key = new { Year = year, Month = month };
            if (holidaysByMonth.TryGetValue(key, out var holidayDays) && settlementDate <= endDate && effectiveWorkDays > 0)
            {
                var dailyRate = Math.Round(baseGrossSalary / effectiveWorkDays, 2);
                var indexedDailyRate = Math.Round(indexedGrossSalary / effectiveWorkDays, 2);

                var holidayGross = Math.Round(dailyRate * 2 * holidayDays, 2);
                var indexedHolidayGross = Math.Round(indexedDailyRate * 2 * holidayDays, 2);

                records.Add(CreateRecord(
                    year, month, PaymentType.HolidayWork, settlementDate,
                    holidayGross, indexedHolidayGross, endDate));
            }

            current = current.AddMonths(1);
        }

        AddSickLeaveRecords(records, input, endDate);
        AddVacationRecords(records, input, endDate);

        return records.OrderBy(r => r.PaymentDate).ThenBy(r => r.Type).ToList();
    }

    private DateTime DetermineStartDate(SalaryInput input)
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

    private int GetEffectiveWorkingDays(int year, int month, SalaryInput input)
    {
        var totalDays = _calendar.GetTotalWorkingDays(year, month);
        var excludedDays = CountExcludedDays(year, month, input);
        return Math.Max(0, totalDays - excludedDays);
    }

    private int GetEffectiveWorkingDaysInRange(int year, int month, int fromDay, int toDay, SalaryInput input)
    {
        var totalDays = _calendar.GetWorkingDays(year, month, fromDay, toDay);
        var excludedDays = CountExcludedDaysInRange(year, month, fromDay, toDay, input);
        return Math.Max(0, totalDays - excludedDays);
    }

    private int CountExcludedDays(int year, int month, SalaryInput input)
    {
        var count = 0;
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));

        foreach (var sickLeave in input.SickLeaves)
        {
            var overlapStart = sickLeave.From > monthStart ? sickLeave.From : monthStart;
            var overlapEnd = sickLeave.To < monthEnd ? sickLeave.To : monthEnd;

            if (overlapStart <= overlapEnd)
            {
                for (var date = overlapStart.Date; date <= overlapEnd.Date; date = date.AddDays(1))
                {
                    if (_calendar.IsWorkingDay(date))
                        count++;
                }
            }
        }

        foreach (var vacation in input.Vacations)
        {
            var overlapStart = vacation.From > monthStart ? vacation.From : monthStart;
            var overlapEnd = vacation.To < monthEnd ? vacation.To : monthEnd;

            if (overlapStart <= overlapEnd)
            {
                for (var date = overlapStart.Date; date <= overlapEnd.Date; date = date.AddDays(1))
                {
                    if (_calendar.IsWorkingDay(date))
                        count++;
                }
            }
        }

        return count;
    }

    private int CountExcludedDaysInRange(int year, int month, int fromDay, int toDay, SalaryInput input)
    {
        var count = 0;
        var rangeStart = new DateTime(year, month, fromDay);
        var rangeEnd = new DateTime(year, month, Math.Min(toDay, DateTime.DaysInMonth(year, month)));

        foreach (var sickLeave in input.SickLeaves)
        {
            var overlapStart = sickLeave.From > rangeStart ? sickLeave.From : rangeStart;
            var overlapEnd = sickLeave.To < rangeEnd ? sickLeave.To : rangeEnd;

            if (overlapStart <= overlapEnd)
            {
                for (var date = overlapStart.Date; date <= overlapEnd.Date; date = date.AddDays(1))
                {
                    if (_calendar.IsWorkingDay(date))
                        count++;
                }
            }
        }

        foreach (var vacation in input.Vacations)
        {
            var overlapStart = vacation.From > rangeStart ? vacation.From : rangeStart;
            var overlapEnd = vacation.To < rangeEnd ? vacation.To : rangeEnd;

            if (overlapStart <= overlapEnd)
            {
                for (var date = overlapStart.Date; date <= overlapEnd.Date; date = date.AddDays(1))
                {
                    if (_calendar.IsWorkingDay(date))
                        count++;
                }
            }
        }

        return count;
    }

    private void AddSickLeaveRecords(List<PaymentRecord> records, SalaryInput input, DateTime calculationDate)
    {
        foreach (var sickLeave in input.SickLeaves)
        {
            if (sickLeave.To > calculationDate)
                continue;

            var paymentDate = GetSettlementPaymentDate(sickLeave.To.Year, sickLeave.To.Month, input.SettlementPayDay);
            if (paymentDate > calculationDate)
                paymentDate = calculationDate;

            var grossAmount = Math.Round(sickLeave.Amount / 0.87m, 2);
            decimal indexedGrossForSickLeave;
            var unperformedIndexations = input.IndexationRules.Where(r => !r.IsPerformed).ToList();
            if (unperformedIndexations.Count == 0)
            {
                indexedGrossForSickLeave = grossAmount;
            }
            else
            {
                var baseGross = input.BaseSalary ?? input.GrossSalary;
                var indexedGrossAmount = input.GetIndexedGrossSalary(sickLeave.To);
                var coefficient = indexedGrossAmount / baseGross;
                indexedGrossForSickLeave = Math.Round(grossAmount * coefficient, 2);
            }
            var indexedNetAmount = ApplyNdfl(indexedGrossForSickLeave);

            records.Add(new PaymentRecord
            {
                Year = sickLeave.To.Year,
                Month = sickLeave.To.Month,
                Type = PaymentType.SickLeave,
                PaymentDate = paymentDate,
                GrossAmount = grossAmount,
                NetAmount = sickLeave.Amount,
                IndexedGrossAmount = indexedGrossForSickLeave,
                IndexedNetAmount = indexedNetAmount,
                Underpayment = Math.Round(indexedNetAmount - sickLeave.Amount, 2),
                DelayDays = Math.Max(0, (calculationDate - paymentDate).Days)
            });
        }
    }

    private void AddVacationRecords(List<PaymentRecord> records, SalaryInput input, DateTime calculationDate)
    {
        foreach (var vacation in input.Vacations)
        {
            if (vacation.To > calculationDate)
                continue;

            var paymentDate = GetSettlementPaymentDate(vacation.To.Year, vacation.To.Month, input.SettlementPayDay);
            if (paymentDate > calculationDate)
                paymentDate = calculationDate;

            var grossAmount = Math.Round(vacation.Amount / 0.87m, 2);
            decimal indexedGrossForVacation;
            var unperformedIndexations = input.IndexationRules.Where(r => !r.IsPerformed).ToList();
            if (unperformedIndexations.Count == 0)
            {
                indexedGrossForVacation = grossAmount;
            }
            else
            {
                var baseGross = input.BaseSalary ?? input.GrossSalary;
                var indexedGrossAmount = input.GetIndexedGrossSalary(vacation.To);
                var coefficient = indexedGrossAmount / baseGross;
                indexedGrossForVacation = Math.Round(grossAmount * coefficient, 2);
            }
            var indexedNetAmount = ApplyNdfl(indexedGrossForVacation);

            records.Add(new PaymentRecord
            {
                Year = vacation.To.Year,
                Month = vacation.To.Month,
                Type = PaymentType.Vacation,
                PaymentDate = paymentDate,
                GrossAmount = grossAmount,
                NetAmount = vacation.Amount,
                IndexedGrossAmount = indexedGrossForVacation,
                IndexedNetAmount = indexedNetAmount,
                Underpayment = Math.Round(indexedNetAmount - vacation.Amount, 2),
                DelayDays = Math.Max(0, (calculationDate - paymentDate).Days)
            });
        }
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

    private static decimal ApplyNdfl(decimal gross)
    {
        return Math.Round(gross - Math.Round(gross * NdflRate, 0), 2);
    }
}
