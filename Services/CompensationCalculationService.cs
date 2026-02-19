using РасчетВыплатЗарплаты.Models;
using РасчетВыплатЗарплаты.Models.Domain;
using РасчетВыплатЗарплаты.Services.Infrastructure;

namespace РасчетВыплатЗарплаты.Services;

public class CompensationCalculationService
{
    private readonly CompensationCalculation _calculation;

    public CompensationCalculationService(CbKeyRateService keyRateService)
    {
        _calculation = new CompensationCalculation(keyRateService);
    }

    public void CalculateCompensation(List<PaymentRecord> records, DateTime calculationDate)
    {
        _calculation.CalculateCompensation(records, calculationDate);
    }
}
