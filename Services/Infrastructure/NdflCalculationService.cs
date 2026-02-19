using РасчетВыплатЗарплаты.Models.Domain;

namespace РасчетВыплатЗарплаты.Services.Infrastructure;

public class NdflCalculationService : INdflCalculator
{
    private static readonly Dictionary<decimal, decimal> TaxBrackets = new()
    {
        { 0m, 0.13m },
        { 2_400_000m, 0.15m },
        { 5_000_000m, 0.18m },
        { 20_000_000m, 0.20m },
        { 50_000_000m, 0.22m }
    };

    public decimal CalculateNdfl(decimal grossAmount, decimal accumulatedIncomeForYear)
    {
        var incomeAfterPayment = accumulatedIncomeForYear + grossAmount;
        decimal totalTax = 0;
        decimal remainingAmount = grossAmount;
        decimal currentIncome = accumulatedIncomeForYear;
        
        var sortedBrackets = TaxBrackets.OrderBy(x => x.Key).ToList();
        
        while (remainingAmount > 0 && currentIncome < incomeAfterPayment)
        {
            var currentBracket = sortedBrackets.LastOrDefault(x => x.Key <= currentIncome);
            var nextBracket = sortedBrackets.FirstOrDefault(x => x.Key > currentIncome);
            
            decimal threshold = nextBracket.Key > 0 ? nextBracket.Key : decimal.MaxValue;
            decimal rate = currentBracket.Key >= 0 ? currentBracket.Value : TaxBrackets[0m];
            
            var availableInBracket = threshold - currentIncome;
            var amountInBracket = Math.Min(remainingAmount, availableInBracket);
            
            totalTax += Math.Round(amountInBracket * rate, 0);
            remainingAmount -= amountInBracket;
            currentIncome += amountInBracket;
        }
        
        return totalTax;
    }

    public decimal CalculateNetAmount(decimal grossAmount, decimal accumulatedIncomeForYear)
    {
        var ndfl = CalculateNdfl(grossAmount, accumulatedIncomeForYear);
        return Math.Round(grossAmount - ndfl, 2);
    }

    public decimal CalculateGrossFromNet(decimal netAmount, decimal accumulatedIncome)
    {
        decimal currentIncome = accumulatedIncome;
        decimal grossAmount = 0;
        decimal remainingNet = netAmount;
        
        var sortedBrackets = TaxBrackets.OrderBy(x => x.Key).ToList();
        
        while (remainingNet > 0.01m)
        {
            var currentBracket = sortedBrackets.LastOrDefault(x => x.Key <= currentIncome);
            var nextBracket = sortedBrackets.FirstOrDefault(x => x.Key > currentIncome);
            
            decimal threshold = nextBracket.Key > 0 ? nextBracket.Key : decimal.MaxValue;
            decimal rate = currentBracket.Key >= 0 ? currentBracket.Value : TaxBrackets[0m];
            
            var availableInBracket = threshold - currentIncome;
            var netInBracket = availableInBracket * (1 - rate);
            
            if (remainingNet <= netInBracket || threshold == decimal.MaxValue)
            {
                grossAmount += remainingNet / (1 - rate);
                remainingNet = 0;
            }
            else
            {
                grossAmount += availableInBracket;
                remainingNet -= netInBracket;
                currentIncome = threshold;
            }
        }
        
        return Math.Round(grossAmount, 2);
    }
}
