namespace РасчетВыплатЗарплаты.Models;

public enum SalaryType
{
    Net,
    Gross
}

public class SalaryInput
{
    public decimal MonthlySalary { get; set; }
    public SalaryType SalaryType { get; set; }
    public int AdvancePayDay { get; set; }
    public int SettlementPayDay { get; set; }
    public DateTime CalculationDate { get; set; }

    public List<IndexationRule> IndexationRules { get; set; } = new();
    public DateTime? BaseIndexationDate { get; set; }
    public decimal? BaseSalary { get; set; }
    public DateTime? HireDate { get; set; }
    
    public decimal? ProbationSalary { get; set; }
    public int? ProbationPeriodMonths { get; set; }

    public List<DateTime> HolidayWorkDates { get; set; } = new();
    public HolidayWorkDailyRateMethod HolidayWorkDailyRateMethod { get; set; } = HolidayWorkDailyRateMethod.MonthlyWorkDays;
    public List<SickLeavePeriod> SickLeaves { get; set; } = new();
    public List<VacationPeriod> Vacations { get; set; } = new();
    
    public bool CalculateIndexationUnderpayments { get; set; } = false;
    public bool CalculateUnusedVacationCompensation { get; set; } = false;
    public DateTime? DismissalDate { get; set; }

    public decimal GrossSalary => SalaryType == SalaryType.Gross
        ? MonthlySalary
        : Math.Round(MonthlySalary / 0.87m, 2);

    public DateTime? GetProbationEndDate()
    {
        if (!HireDate.HasValue || !ProbationPeriodMonths.HasValue)
            return null;
        
        return HireDate.Value.AddMonths(ProbationPeriodMonths.Value);
    }

    public bool IsInProbationPeriod(DateTime date)
    {
        if (!HireDate.HasValue || !ProbationPeriodMonths.HasValue)
            return false;
        
        var probationEnd = GetProbationEndDate();
        return probationEnd.HasValue && date < probationEnd.Value;
    }

    public decimal GetGrossSalaryForDate(DateTime targetDate)
    {
        if (IsInProbationPeriod(targetDate) && ProbationSalary.HasValue)
        {
            return SalaryType == SalaryType.Gross
                ? ProbationSalary.Value
                : Math.Round(ProbationSalary.Value / 0.87m, 2);
        }
        
        return GrossSalary;
    }

    public decimal GetIndexedGrossSalary(DateTime targetDate)
    {
        var baseSalaryForDate = GetGrossSalaryForDate(targetDate);
        
        decimal baseSalary;
        if (BaseSalary.HasValue && !IsInProbationPeriod(targetDate))
        {
            baseSalary = BaseSalary.Value;
            if (SalaryType == SalaryType.Net)
            {
                baseSalary = Math.Round(baseSalary / 0.87m, 2);
            }
        }
        else
        {
            baseSalary = baseSalaryForDate;
        }

        var unperformedIndexations = IndexationRules.Where(r => !r.IsPerformed).ToList();
        var firstIndexationDate = unperformedIndexations.Count > 0 
            ? unperformedIndexations.OrderBy(r => r.Date).First().Date 
            : (DateTime?)null;
        
        if (firstIndexationDate.HasValue && targetDate < firstIndexationDate.Value)
        {
            return baseSalary;
        }

        var salary = baseSalary;
        foreach (var rule in IndexationRules.Where(r => r.Date <= targetDate && !r.IsPerformed).OrderBy(r => r.Date))
        {
            salary *= (1 + rule.Percent / 100m);
        }
        return Math.Round(salary, 2);
    }

    public decimal GetIndexedNetSalary(DateTime targetDate)
    {
        var grossSalary = GetIndexedGrossSalary(targetDate);
        return Math.Round(grossSalary * 0.87m, 2);
    }

    [Obsolete("Используйте GetIndexedGrossSalary(DateTime)")]
    public decimal IndexedGrossSalary => IndexationRules.Count > 0 && CalculationDate != default
        ? GetIndexedGrossSalary(CalculationDate)
        : GrossSalary;
}
