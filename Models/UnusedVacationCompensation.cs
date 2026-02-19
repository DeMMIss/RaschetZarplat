namespace РасчетВыплатЗарплаты.Models;

public class UnusedVacationCompensation
{
    public DateTime HireDate { get; set; }
    public DateTime DismissalDate { get; set; }
    public int WorkMonths { get; set; }
    public int EarnedVacationDays { get; set; }
    public int UsedVacationDays { get; set; }
    public int UnusedVacationDays { get; set; }
    public decimal AvgDailyGross { get; set; }
    public decimal CompensationGross { get; set; }
    public decimal CompensationNet { get; set; }
    public decimal AvgDailyGrossWithoutIndexation { get; set; }
    public decimal CompensationGrossWithoutIndexation { get; set; }
    public decimal CompensationNetWithoutIndexation { get; set; }
    public decimal DifferenceGross { get; set; }
    public decimal DifferenceNet { get; set; }
}
