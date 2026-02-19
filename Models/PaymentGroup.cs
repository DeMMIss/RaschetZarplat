namespace РасчетВыплатЗарплаты.Models;

public class PaymentGroup
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<PaymentRecord> Payments { get; set; } = new();
    public bool IsExpanded { get; set; } = false;
    public bool IsGroup => true;

    public string PeriodDisplay => new DateTime(Year, Month, 1).ToString("MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
    
    public decimal NetAmount => Payments.Sum(p => p.NetAmount);
    public decimal IndexedNetAmount => Payments.Sum(p => p.IndexedNetAmount);
    public decimal Underpayment => Payments.Sum(p => p.Underpayment);
    public decimal Compensation => Payments.Sum(p => p.Compensation);
    public decimal NdflAmount => Payments.Sum(p => p.NdflAmount);
    public decimal IndexedNdflAmount => Payments.Sum(p => p.IndexedNdflAmount);
    
    public string TypeDisplay => $"Итого за месяц ({Payments.Count} выплат)";
    public DateTime? PaymentDate => null;
}
