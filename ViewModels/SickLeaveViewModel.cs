namespace РасчетВыплатЗарплаты.ViewModels;

public class SickLeaveViewModel
{
    private static readonly DateTime DefaultDate = new DateTime(DateTime.Now.Year, 1, 1);
    public DateTime From { get; set; } = DefaultDate;
    public DateTime To { get; set; } = DefaultDate;
    public decimal Amount { get; set; }
}
