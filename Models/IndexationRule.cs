namespace РасчетВыплатЗарплаты.Models;

public class IndexationRule
{
    public DateTime Date { get; set; }
    public decimal Percent { get; set; }
    public int? FrequencyMonths { get; set; }
    public bool IsPerformed { get; set; }
}
