namespace РасчетВыплатЗарплаты.Models.Domain;

public interface ICbKeyRateService
{
    Task Load(DateTime from, DateTime to);
    decimal GetRate(DateTime date);
    List<(DateTime From, DateTime To, decimal Rate)> GetRatePeriods(DateTime from, DateTime to);
    bool IsLoaded { get; }
}
