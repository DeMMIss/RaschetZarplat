using РасчетВыплатЗарплаты.Models;
using РасчетВыплатЗарплаты.Models.Domain;

namespace РасчетВыплатЗарплаты.Services;

public class VacationPayService
{
    private readonly VacationPayCalculation _calculation;

    public VacationPayService()
    {
        var ndflCalculator = new Infrastructure.NdflCalculationService();
        _calculation = new VacationPayCalculation(ndflCalculator);
    }

    public List<VacationPayResult> Calculate(SalaryInput input)
    {
        return _calculation.Calculate(input);
    }

    public UnusedVacationCompensation? CalculateUnusedVacationCompensation(SalaryInput input, DateTime dismissalDate)
    {
        return _calculation.CalculateUnusedVacationCompensation(input, dismissalDate);
    }
}
