namespace РасчетЗадолженностиЗП.Models;

public enum PaymentType
{
    Advance,
    Settlement,
    HolidayWork
}

public class PaymentRecord
{
    public int Year { get; set; }
    public int Month { get; set; }
    public PaymentType Type { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal IndexedGrossAmount { get; set; }
    public decimal IndexedNetAmount { get; set; }
    public decimal Underpayment { get; set; }
    public int DelayDays { get; set; }
    public decimal Compensation { get; set; }
    public List<CompensationDetail> CompensationDetails { get; set; } = new();

    public string PeriodDisplay => $"{Year}-{Month:D2}";
    public string TypeDisplay => Type switch
    {
        PaymentType.Advance => "Аванс",
        PaymentType.Settlement => "Расчёт",
        PaymentType.HolidayWork => "Праздн.",
        _ => "—"
    };
}

public class CompensationDetail
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int Days { get; set; }
    public decimal KeyRate { get; set; }
    public decimal DailyRate { get; set; }
    public decimal Amount { get; set; }
}
