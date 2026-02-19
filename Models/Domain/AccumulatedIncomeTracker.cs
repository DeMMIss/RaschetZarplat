namespace РасчетВыплатЗарплаты.Models.Domain;

public class AccumulatedIncomeTracker
{
    private readonly Dictionary<int, decimal> _accumulatedIncomeByYear = new();

    public decimal GetAccumulatedIncome(int year)
    {
        return _accumulatedIncomeByYear.GetValueOrDefault(year, 0);
    }

    public void AddIncome(int year, decimal grossAmount)
    {
        var currentIncome = GetAccumulatedIncome(year);
        _accumulatedIncomeByYear[year] = currentIncome + grossAmount;
    }

    public void Reset()
    {
        _accumulatedIncomeByYear.Clear();
    }
}
