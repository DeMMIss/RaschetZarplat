using РасчетВыплатЗарплаты.Models;

namespace РасчетВыплатЗарплаты.Models.Domain;

public class CompensationCalculation
{
    private readonly ICbKeyRateService _keyRateService;

    public CompensationCalculation(ICbKeyRateService keyRateService)
    {
        _keyRateService = keyRateService;
    }

    public void CalculateCompensation(List<PaymentRecord> records, DateTime calculationDate)
    {
        foreach (var record in records)
        {
            if (record.Underpayment <= 0)
            {
                continue;
            }

            var delayStart = record.PaymentDate.AddDays(1);
            if (delayStart > calculationDate)
            {
                continue;
            }

            var periods = _keyRateService.GetRatePeriods(delayStart, calculationDate);

            decimal totalCompensation = 0;
            var details = new List<CompensationDetail>();

            foreach (var (from, to, rate) in periods)
            {
                var compensationDetail = CalculateCompensationForPeriod(record.Underpayment, from, to, rate);
                totalCompensation += compensationDetail.Amount;
                details.Add(compensationDetail);
            }

            record.Compensation = Math.Round(totalCompensation, 2);
            record.CompensationDetails = details;
            record.DelayDays = (calculationDate - record.PaymentDate).Days;
        }
    }

    private CompensationDetail CalculateCompensationForPeriod(decimal underpayment, DateTime from, DateTime to, decimal rate)
    {
        var days = (to - from).Days + 1;
        var dailyRate = rate / 150m / 100m;
        var amount = Math.Round(underpayment * dailyRate * days, 2);

        return new CompensationDetail
        {
            From = from,
            To = to,
            Days = days,
            KeyRate = rate,
            DailyRate = Math.Round(dailyRate, 8),
            Amount = amount
        };
    }
}
