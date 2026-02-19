namespace РасчетВыплатЗарплаты.Models.Domain;

public interface INdflCalculator
{
    decimal CalculateNdfl(decimal grossAmount, decimal accumulatedIncomeForYear);
    decimal CalculateNetAmount(decimal grossAmount, decimal accumulatedIncomeForYear);
    decimal CalculateGrossFromNet(decimal netAmount, decimal accumulatedIncome);
}
