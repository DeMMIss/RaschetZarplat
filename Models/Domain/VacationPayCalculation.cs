using РасчетВыплатЗарплаты.Models;

namespace РасчетВыплатЗарплаты.Models.Domain;

public class VacationPayCalculation
{
    private const decimal AvgDaysPerMonth = 29.3m;
    private readonly INdflCalculator _ndflCalculator;

    public VacationPayCalculation(INdflCalculator ndflCalculator)
    {
        _ndflCalculator = ndflCalculator;
    }

    public List<VacationPayResult> Calculate(SalaryInput input)
    {
        var results = new List<VacationPayResult>();
        
        foreach (var vacation in input.Vacations)
        {
            var result = CalculateVacationPay(vacation, input);
            if (result != null)
            {
                results.Add(result);
            }
        }
        
        return results;
    }

    public UnusedVacationCompensation? CalculateUnusedVacationCompensation(SalaryInput input, DateTime dismissalDate)
    {
        if (!input.HireDate.HasValue)
        {
            return null;
        }

        var hireDate = input.HireDate.Value;

        if (dismissalDate <= hireDate)
        {
            return null;
        }

        var workMonths = CalculateWorkMonths(hireDate, dismissalDate);
        var earnedVacationDays = (int)Math.Round(workMonths * 2.33m);
        
        var usedVacationDays = input.Vacations
            .Where(v => v.To <= dismissalDate)
            .Sum(v => (v.To - v.From).Days + 1);

        var unusedVacationDays = Math.Max(0, earnedVacationDays - usedVacationDays);

        if (unusedVacationDays == 0)
        {
            return null;
        }

        var periodStart = dismissalDate.AddMonths(-12);
        if (periodStart < hireDate)
        {
            periodStart = new DateTime(hireDate.Year, hireDate.Month, 1);
        }

        var (avgDailyGrossWithIndexation, avgDailyGrossWithoutIndexation) = CalculateAverageDailyGross(
            periodStart, dismissalDate, hireDate, input);

        var compensationGrossWithIndexation = Math.Round(avgDailyGrossWithIndexation * unusedVacationDays, 2);
        var accumulatedIncome = EstimateAccumulatedIncome(input, dismissalDate);
        var compensationNetWithIndexation = _ndflCalculator.CalculateNetAmount(compensationGrossWithIndexation, accumulatedIncome);
        
        var compensationGrossWithoutIndexation = Math.Round(avgDailyGrossWithoutIndexation * unusedVacationDays, 2);
        var compensationNetWithoutIndexation = _ndflCalculator.CalculateNetAmount(compensationGrossWithoutIndexation, accumulatedIncome);
        
        var differenceGross = compensationGrossWithIndexation - compensationGrossWithoutIndexation;
        var differenceNet = compensationNetWithIndexation - compensationNetWithoutIndexation;

        return new UnusedVacationCompensation
        {
            HireDate = hireDate,
            DismissalDate = dismissalDate,
            WorkMonths = workMonths,
            EarnedVacationDays = earnedVacationDays,
            UsedVacationDays = usedVacationDays,
            UnusedVacationDays = unusedVacationDays,
            AvgDailyGross = Math.Round(avgDailyGrossWithIndexation, 2),
            CompensationGross = compensationGrossWithIndexation,
            CompensationNet = compensationNetWithIndexation,
            AvgDailyGrossWithoutIndexation = Math.Round(avgDailyGrossWithoutIndexation, 2),
            CompensationGrossWithoutIndexation = compensationGrossWithoutIndexation,
            CompensationNetWithoutIndexation = compensationNetWithoutIndexation,
            DifferenceGross = Math.Round(differenceGross, 2),
            DifferenceNet = Math.Round(differenceNet, 2)
        };
    }

    private VacationPayResult? CalculateVacationPay(VacationPeriod vacation, SalaryInput input)
    {
        var calendarDays = (vacation.To - vacation.From).Days + 1;
        var periodStart = vacation.From.AddMonths(-12);
        var periodEnd = vacation.From.AddDays(-1);
        
        if (input.HireDate.HasValue && periodStart < input.HireDate.Value)
        {
            periodStart = new DateTime(input.HireDate.Value.Year, input.HireDate.Value.Month, 1);
        }

        var (sumGross, monthsCount) = CalculateSumGrossForPeriod(periodStart, periodEnd, input);

        if (monthsCount == 0)
        {
            monthsCount = 1;
            sumGross = input.GetIndexedGrossSalary(vacation.From);
        }

        var avgDailyGross = sumGross / monthsCount / AvgDaysPerMonth;
        var calculatedGross = Math.Round(avgDailyGross * calendarDays, 2);
        var accumulatedIncome = EstimateAccumulatedIncome(input, vacation.From);
        var calculatedNet = _ndflCalculator.CalculateNetAmount(calculatedGross, accumulatedIncome);
        var paidNet = vacation.Amount;
        var diffNet = Math.Round(calculatedNet - paidNet, 2);

        return new VacationPayResult
        {
            From = vacation.From,
            To = vacation.To,
            CalendarDays = calendarDays,
            AvgDailyGross = Math.Round(avgDailyGross, 2),
            CalculatedGross = calculatedGross,
            CalculatedNet = calculatedNet,
            PaidNet = paidNet,
            DifferenceNet = diffNet != 0 ? diffNet : null
        };
    }

    private (decimal sumGross, int monthsCount) CalculateSumGrossForPeriod(DateTime periodStart, DateTime periodEnd, SalaryInput input)
    {
        decimal sumGross = 0;
        var monthsCount = 0;
        var current = new DateTime(periodStart.Year, periodStart.Month, 1);
        var lastMonth = new DateTime(periodEnd.Year, periodEnd.Month, 1);
        
        while (current <= lastMonth)
        {
            var midMonth = current.AddDays(14);
            if (!input.HireDate.HasValue || midMonth >= input.HireDate.Value)
            {
                sumGross += input.GetIndexedGrossSalary(midMonth);
                monthsCount++;
            }
            current = current.AddMonths(1);
        }

        return (sumGross, monthsCount);
    }

    private (decimal avgDailyGrossWithIndexation, decimal avgDailyGrossWithoutIndexation) CalculateAverageDailyGross(
        DateTime periodStart, DateTime dismissalDate, DateTime hireDate, SalaryInput input)
    {
        decimal sumGrossWithIndexation = 0;
        decimal sumGrossWithoutIndexation = 0;
        var monthsCount = 0;
        var current = new DateTime(periodStart.Year, periodStart.Month, 1);
        var lastMonth = new DateTime(dismissalDate.Year, dismissalDate.Month, 1);

        while (current <= lastMonth)
        {
            var midMonth = current.AddDays(14);
            if (midMonth >= hireDate && midMonth <= dismissalDate)
            {
                sumGrossWithIndexation += input.GetIndexedGrossSalary(midMonth);
                sumGrossWithoutIndexation += input.GetGrossSalaryForDate(midMonth);
                monthsCount++;
            }
            current = current.AddMonths(1);
        }

        if (monthsCount == 0)
        {
            monthsCount = 1;
            sumGrossWithIndexation = input.GetIndexedGrossSalary(dismissalDate);
            sumGrossWithoutIndexation = input.GetGrossSalaryForDate(dismissalDate);
        }

        var avgDailyGrossWithIndexation = sumGrossWithIndexation / monthsCount / AvgDaysPerMonth;
        var avgDailyGrossWithoutIndexation = sumGrossWithoutIndexation / monthsCount / AvgDaysPerMonth;
        
        return (avgDailyGrossWithIndexation, avgDailyGrossWithoutIndexation);
    }

    private int CalculateWorkMonths(DateTime hireDate, DateTime dismissalDate)
    {
        var months = 0;
        var current = new DateTime(hireDate.Year, hireDate.Month, 1);
        var lastMonth = new DateTime(dismissalDate.Year, dismissalDate.Month, 1);

        while (current <= lastMonth)
        {
            var monthStart = current;
            var monthEnd = current.AddMonths(1).AddDays(-1);
            
            if (monthStart < hireDate)
            {
                monthStart = hireDate;
            }
            if (monthEnd > dismissalDate)
            {
                monthEnd = dismissalDate;
            }

            var daysInMonth = (monthEnd - monthStart).Days + 1;
            var totalDaysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
            
            if (daysInMonth >= 15)
            {
                months++;
            }
            else if (daysInMonth > 0)
            {
                months += (int)Math.Round(daysInMonth / (decimal)totalDaysInMonth);
            }

            current = current.AddMonths(1);
        }

        return months;
    }

    private decimal EstimateAccumulatedIncome(SalaryInput input, DateTime date)
    {
        if (!input.HireDate.HasValue || input.HireDate.Value.Year != date.Year)
        {
            var yearStart = new DateTime(date.Year, 1, 1);
            var monthsFromYearStart = (date.Month - yearStart.Month) + 1;
            return input.GrossSalary * Math.Max(0, monthsFromYearStart - 1);
        }

        var hireDate = input.HireDate.Value;
        if (hireDate.Year != date.Year)
        {
            return 0;
        }

        var monthsFromHire = (date.Month - hireDate.Month) + (date.Year - hireDate.Year) * 12;
        return input.GrossSalary * Math.Max(0, monthsFromHire - 1);
    }
}
