namespace РасчетЗадолженностиЗП.Models;

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
    public decimal IndexationPercent { get; set; }
    public DateTime IndexationDate { get; set; }
    public DateTime CalculationDate { get; set; }

    public decimal GrossSalary => SalaryType == SalaryType.Gross
        ? MonthlySalary
        : Math.Round(MonthlySalary / 0.87m, 2);

    public List<DateTime> HolidayWorkDates { get; set; } = new();

    public decimal IndexedGrossSalary => Math.Round(GrossSalary * (1 + IndexationPercent / 100m), 2);
}
