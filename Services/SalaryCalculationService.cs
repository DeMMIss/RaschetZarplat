using РасчетВыплатЗарплаты.Models;
using РасчетВыплатЗарплаты.Models.Domain;
using РасчетВыплатЗарплаты.Services.Infrastructure;

namespace РасчетВыплатЗарплаты.Services;

public class SalaryCalculationService
{
    private readonly IProductionCalendar _calendar;
    private readonly INdflCalculator _ndflCalculator;

    public SalaryCalculationService(ProductionCalendarService calendar)
    {
        _calendar = calendar;
        _ndflCalculator = new Infrastructure.NdflCalculationService();
    }

    public List<PaymentRecord> Calculate(SalaryInput input)
    {
        var calculation = new PaymentCalculation(input, _calendar, _ndflCalculator);
        return calculation.CalculateAll();
    }
}
