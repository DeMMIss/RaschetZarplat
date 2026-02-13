using РасчетЗадолженностиЗП.Models;

namespace РасчетЗадолженностиЗП.Services;

public class VacationPayService
{
    private const decimal AvgDaysPerMonth = 29.3m;
    private const decimal NdflRate = 0.13m;

    public List<VacationPayResult> Calculate(SalaryInput input)
    {
        var results = new List<VacationPayResult>();
        foreach (var v in input.Vacations)
        {
            var calendarDays = (v.To - v.From).Days + 1;
            var periodStart = v.From.AddMonths(-12);
            var periodEnd = v.From.AddDays(-1);
            if (input.HireDate.HasValue && periodStart < input.HireDate.Value)
            {
                periodStart = new DateTime(input.HireDate.Value.Year, input.HireDate.Value.Month, 1);
            }

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

            if (monthsCount == 0)
            {
                monthsCount = 1;
                sumGross = input.GetIndexedGrossSalary(v.From);
            }

            var avgDailyGross = sumGross / monthsCount / AvgDaysPerMonth;
            var calculatedGross = Math.Round(avgDailyGross * calendarDays, 2);
            var calculatedNet = Math.Round(calculatedGross * (1 - NdflRate), 2);
            var paidNet = v.Amount;
            var diffNet = Math.Round(calculatedNet - paidNet, 2);

            results.Add(new VacationPayResult
            {
                From = v.From,
                To = v.To,
                CalendarDays = calendarDays,
                AvgDailyGross = Math.Round(avgDailyGross, 2),
                CalculatedGross = calculatedGross,
                CalculatedNet = calculatedNet,
                PaidNet = paidNet,
                DifferenceNet = diffNet != 0 ? diffNet : null
            });
        }
        return results;
    }

    public UnusedVacationCompensation? CalculateUnusedVacationCompensation(SalaryInput input)
    {
        if (!input.HireDate.HasValue)
            return null;

        var hireDate = input.HireDate.Value;
        var dismissalDate = input.CalculationDate;

        if (dismissalDate <= hireDate)
            return null;

        var totalWorkDays = (dismissalDate - hireDate).Days;
        var workMonths = CalculateWorkMonths(hireDate, dismissalDate, input);
        var earnedVacationDays = (int)Math.Round(workMonths * 2.33m);
        
        var usedVacationDays = input.Vacations
            .Where(v => v.To <= dismissalDate)
            .Sum(v => (v.To - v.From).Days + 1);

        var unusedVacationDays = Math.Max(0, earnedVacationDays - usedVacationDays);

        if (unusedVacationDays == 0)
            return null;

        var periodStart = dismissalDate.AddMonths(-12);
        if (periodStart < hireDate)
            periodStart = new DateTime(hireDate.Year, hireDate.Month, 1);

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
        
        var compensationGrossWithIndexation = Math.Round(avgDailyGrossWithIndexation * unusedVacationDays, 2);
        var compensationNetWithIndexation = Math.Round(compensationGrossWithIndexation * (1 - NdflRate), 2);
        
        var compensationGrossWithoutIndexation = Math.Round(avgDailyGrossWithoutIndexation * unusedVacationDays, 2);
        var compensationNetWithoutIndexation = Math.Round(compensationGrossWithoutIndexation * (1 - NdflRate), 2);
        
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

    private int CalculateWorkMonths(DateTime hireDate, DateTime dismissalDate, SalaryInput input)
    {
        var months = 0;
        var current = new DateTime(hireDate.Year, hireDate.Month, 1);
        var lastMonth = new DateTime(dismissalDate.Year, dismissalDate.Month, 1);

        while (current <= lastMonth)
        {
            var monthStart = current;
            var monthEnd = current.AddMonths(1).AddDays(-1);
            
            if (monthStart < hireDate)
                monthStart = hireDate;
            if (monthEnd > dismissalDate)
                monthEnd = dismissalDate;

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
}
