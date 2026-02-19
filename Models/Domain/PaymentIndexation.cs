using РасчетВыплатЗарплаты.Models;

namespace РасчетВыплатЗарплаты.Models.Domain;

public class PaymentIndexation
{
    private readonly INdflCalculator _ndflCalculator;

    public PaymentIndexation(INdflCalculator ndflCalculator)
    {
        _ndflCalculator = ndflCalculator;
    }

    public void ApplyIndexation(List<PaymentRecord> records, SalaryInput input)
    {
        var sortedRecords = records.OrderBy(r => r.PaymentDate).ThenBy(r => r.Type).ToList();
        var accumulatedIncomeByYear = new Dictionary<int, decimal>();
        
        foreach (var record in sortedRecords)
        {
            var indexedGrossAmount = CalculateIndexedGrossAmount(record, input);
            var accumulatedIncome = accumulatedIncomeByYear.GetValueOrDefault(record.PaymentDate.Year, 0);
            var indexedNetAmount = _ndflCalculator.CalculateNetAmount(indexedGrossAmount, accumulatedIncome);
            
            record.IndexedGrossAmount = indexedGrossAmount;
            record.IndexedNetAmount = indexedNetAmount;
            record.Underpayment = Math.Round(indexedNetAmount - record.NetAmount, 2);
            
            accumulatedIncomeByYear[record.PaymentDate.Year] = accumulatedIncome + record.GrossAmount;
        }
    }

    private decimal CalculateIndexedGrossAmount(PaymentRecord record, SalaryInput input)
    {
        var unperformedIndexations = input.IndexationRules.Where(r => !r.IsPerformed).ToList();
        if (unperformedIndexations.Count == 0)
        {
            return record.GrossAmount;
        }

        var firstIndexationDate = unperformedIndexations.OrderBy(r => r.Date).First().Date;

        if (record.Type == PaymentType.SickLeave || record.Type == PaymentType.Vacation)
        {
            return CalculateIndexedAmountForPeriod(record, input, firstIndexationDate);
        }

        return CalculateIndexedAmountForMonthlyPayment(record, input, firstIndexationDate);
    }

    private decimal CalculateIndexedAmountForPeriod(PaymentRecord record, SalaryInput input, DateTime firstIndexationDate)
    {
        var baseGross = input.BaseSalary ?? input.GrossSalary;
        var indexedGrossForDate = input.GetIndexedGrossSalary(record.PaymentDate);
        var coefficient = indexedGrossForDate / baseGross;
        return Math.Round(record.GrossAmount * coefficient, 2);
    }

    private decimal CalculateIndexedAmountForMonthlyPayment(PaymentRecord record, SalaryInput input, DateTime firstIndexationDate)
    {
        var monthDate = new DateTime(record.Year, record.Month, 15);
        if (monthDate < firstIndexationDate)
        {
            return record.GrossAmount;
        }

        var monthGrossSalary = input.GetGrossSalaryForDate(monthDate);
        var indexedMonthGrossSalary = input.GetIndexedGrossSalary(monthDate);
        var coefficient = monthGrossSalary > 0 ? indexedMonthGrossSalary / monthGrossSalary : 1m;
        return Math.Round(record.GrossAmount * coefficient, 2);
    }
}
