namespace РасчетЗадолженностиЗП.Models;

public class VacationPayResult
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int CalendarDays { get; set; }
    public decimal AvgDailyGross { get; set; }
    public decimal CalculatedGross { get; set; }
    public decimal CalculatedNet { get; set; }
    public decimal PaidNet { get; set; }
    public decimal? DifferenceNet { get; set; }
}
