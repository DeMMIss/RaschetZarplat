namespace РасчетВыплатЗарплаты.ViewModels;

public class IndexationRuleViewModel
{
    public DateTime Date { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
    public decimal Percent { get; set; }
    public bool IsPerformed { get; set; }
}
