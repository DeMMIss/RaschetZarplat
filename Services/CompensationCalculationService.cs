using РасчетЗадолженностиЗП.Models;

namespace РасчетЗадолженностиЗП.Services;

public class CompensationCalculationService
{
    private readonly CbKeyRateService _keyRateService;

    public CompensationCalculationService(CbKeyRateService keyRateService)
    {
        _keyRateService = keyRateService;
    }

    public void CalculateCompensation(List<PaymentRecord> records, DateTime calculationDate)
    {
        foreach (var record in records)
        {
            if (record.Underpayment <= 0)
                continue;

            var delayStart = record.PaymentDate.AddDays(1);
            if (delayStart > calculationDate)
                continue;

            var periods = _keyRateService.GetRatePeriods(delayStart, calculationDate);

            decimal totalCompensation = 0;
            var details = new List<CompensationDetail>();

            foreach (var (from, to, rate) in periods)
            {
                int days = (to - from).Days + 1;
                decimal dailyRate = rate / 150m / 100m;
                decimal amount = Math.Round(record.Underpayment * dailyRate * days, 2);

                totalCompensation += amount;

                details.Add(new CompensationDetail
                {
                    From = from,
                    To = to,
                    Days = days,
                    KeyRate = rate,
                    DailyRate = Math.Round(dailyRate, 8),
                    Amount = amount
                });
            }

            record.Compensation = Math.Round(totalCompensation, 2);
            record.CompensationDetails = details;
            record.DelayDays = (calculationDate - record.PaymentDate).Days;
        }
    }
}
