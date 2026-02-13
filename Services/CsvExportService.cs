using System.Globalization;
using System.Text;
using РасчетЗадолженностиЗП.Models;

namespace РасчетЗадолженностиЗП.Services;

public class CsvExportService
{
    public void Export(SalaryInput input, List<PaymentRecord> records, List<VacationPayResult> vacationPayResults, UnusedVacationCompensation? unusedVacationCompensation, string filePath)
    {
        var csv = new StringBuilder();

        csv.AppendLine("=== ПАРАМЕТРЫ ===");
        csv.AppendLine("Параметр;Значение");
        csv.AppendLine($"Оклад;{input.MonthlySalary:N2} руб.");
        csv.AppendLine($"Тип оклада;{(input.SalaryType == SalaryType.Net ? "Net (на руки)" : "Gross (до НДФЛ)")}");
        csv.AppendLine($"Оклад (gross);{input.GrossSalary:N2} руб.");
        
        if (input.ProbationSalary.HasValue)
        {
            csv.AppendLine($"Оклад на испытательном сроке;{input.ProbationSalary.Value:N2} руб. ({(input.SalaryType == SalaryType.Net ? "Net" : "Gross")})");
        }
        
        if (input.ProbationPeriodMonths.HasValue)
        {
            csv.AppendLine($"Длительность испытательного срока;{input.ProbationPeriodMonths.Value} мес.");
            if (input.HireDate.HasValue)
            {
                var probationEnd = input.HireDate.Value.AddMonths(input.ProbationPeriodMonths.Value);
                csv.AppendLine($"Окончание испытательного срока;{probationEnd:dd.MM.yyyy}");
            }
        }
        
        csv.AppendLine($"День выплаты аванса;{input.AdvancePayDay}");
        csv.AppendLine($"День выплаты расчёта;{input.SettlementPayDay}");

        if (input.HireDate.HasValue)
        {
            csv.AppendLine($"Дата найма;{input.HireDate.Value:dd.MM.yyyy}");
        }

        if (input.BaseSalary.HasValue)
        {
            csv.AppendLine($"Базовая зарплата (net);{input.BaseSalary.Value:N2} руб.");
        }

        if (input.IndexationRules.Count > 0)
        {
            var unperformedCount = input.IndexationRules.Count(r => !r.IsPerformed);
            csv.AppendLine($"Правила индексации;{input.IndexationRules.Count} шт. (не проведено: {unperformedCount})");
            foreach (var rule in input.IndexationRules.OrderBy(r => r.Date))
            {
                var status = rule.IsPerformed ? "проведена" : "не проведена";
                var freq = rule.FrequencyMonths.HasValue ? $" (каждые {rule.FrequencyMonths} мес.)" : "";
                csv.AppendLine($"  - {rule.Date:dd.MM.yyyy};{rule.Percent}%{freq} ({status})");
            }
        }

        csv.AppendLine($"Дата расчёта задолженности;{input.CalculationDate:dd.MM.yyyy}");

        if (input.HolidayWorkDates.Count > 0)
        {
            csv.AppendLine($"Рабочие дни в праздники;{string.Join(", ", input.HolidayWorkDates.OrderBy(d => d).Select(d => d.ToString("dd.MM.yyyy")))}");
        }

        if (input.SickLeaves.Count > 0)
        {
            csv.AppendLine($"Больничные;{input.SickLeaves.Count} период(ов)");
        }

        if (input.Vacations.Count > 0)
        {
            csv.AppendLine($"Отпуска;{input.Vacations.Count} период(ов)");
        }

        csv.AppendLine();
        var hasIndexation = input.IndexationRules.Any(r => !r.IsPerformed);
        
        if (hasIndexation)
        {
            csv.AppendLine("=== РАСЧЁТ ЗАДОЛЖЕННОСТИ ===");
            csv.AppendLine("Период;Тип;Дата выплаты;Без индексации (gross);Без индексации (net);С индексацией (gross);С индексацией (net);Недоплата (net);Дней просрочки;Компенсация;Подлежала индексации");

            var unperformedIndexations = input.IndexationRules.Where(r => !r.IsPerformed).ToList();
            var firstIndexationDate = unperformedIndexations.OrderBy(r => r.Date).First().Date;

            foreach (var r in records)
            {
                var wasIndexed = r.PaymentDate >= firstIndexationDate;
                csv.AppendLine($"{EscapeCsv(r.PeriodDisplay)};{EscapeCsv(r.TypeDisplay)};{r.PaymentDate:dd.MM.yyyy};{r.GrossAmount:N2};{r.NetAmount:N2};{r.IndexedGrossAmount:N2};{r.IndexedNetAmount:N2};{r.Underpayment:N2};{r.DelayDays};{r.Compensation:N2};{(wasIndexed ? "Да" : "Нет")}");
            }

            var totalUnderpayment = records.Sum(r => r.Underpayment);
            var totalCompensation = records.Sum(r => r.Compensation);
            csv.AppendLine($"ИТОГО;;;;;;;;{totalUnderpayment:N2};;{totalCompensation:N2};");
            csv.AppendLine($"ИТОГО К ВЗЫСКАНИЮ;;;;;;;;{totalUnderpayment + totalCompensation:N2};;;");
            
            csv.AppendLine();
            csv.AppendLine($"Примечание: Выплаты до {firstIndexationDate:dd.MM.yyyy} не подлежали индексации.");
        }
        else
        {
            csv.AppendLine("=== ОТЧЁТ ПО ЗАРПЛАТЕ ===");
            csv.AppendLine("Период;Тип;Дата выплаты;Сумма (gross);Сумма (net)");

            foreach (var r in records)
            {
                csv.AppendLine($"{EscapeCsv(r.PeriodDisplay)};{EscapeCsv(r.TypeDisplay)};{r.PaymentDate:dd.MM.yyyy};{r.GrossAmount:N2};{r.NetAmount:N2}");
            }

            var totalGross = records.Sum(r => r.GrossAmount);
            var totalNet = records.Sum(r => r.NetAmount);
            csv.AppendLine($"ИТОГО;;;{totalGross:N2};{totalNet:N2}");
        }

        if (hasIndexation)
        {
            csv.AppendLine();
            csv.AppendLine("=== ДЕТАЛИЗАЦИЯ КОМПЕНСАЦИИ ===");
            csv.AppendLine("Период;Тип;Дата выплаты;Недоплата;Период (с);Период (по);Дней;Ключевая ставка %;Дневная ставка;Компенсация");

        var groupedDetails = records
            .Where(r => r.CompensationDetails.Count > 0)
            .SelectMany(r => r.CompensationDetails.Select(d => new { Record = r, Detail = d }))
            .GroupBy(x => new
            {
                x.Record.PeriodDisplay,
                x.Record.TypeDisplay,
                x.Record.PaymentDate,
                x.Record.Underpayment,
                x.Detail.KeyRate
            })
            .Select(g => new
            {
                g.Key.PeriodDisplay,
                g.Key.TypeDisplay,
                g.Key.PaymentDate,
                g.Key.Underpayment,
                From = g.Min(x => x.Detail.From),
                To = g.Max(x => x.Detail.To),
                Days = g.Sum(x => x.Detail.Days),
                KeyRate = g.Key.KeyRate,
                DailyRate = g.First().Detail.DailyRate,
                Amount = g.Sum(x => x.Detail.Amount)
            })
            .OrderBy(x => x.PaymentDate)
            .ThenBy(x => x.PeriodDisplay)
            .ToList();

        foreach (var grouped in groupedDetails)
        {
            csv.AppendLine($"{EscapeCsv(grouped.PeriodDisplay)};{EscapeCsv(grouped.TypeDisplay)};{grouped.PaymentDate:dd.MM.yyyy};{grouped.Underpayment:N2};{grouped.From:dd.MM.yyyy};{grouped.To:dd.MM.yyyy};{grouped.Days};{grouped.KeyRate:N2};{grouped.DailyRate:N8};{grouped.Amount:N2}");
        }

            csv.AppendLine($"ИТОГО;;;;;;;;;{records.Sum(r => r.Compensation):N2}");
        }

        if (vacationPayResults.Count > 0)
        {
            csv.AppendLine();
            csv.AppendLine("=== ОТПУСКНЫЕ ===");
            csv.AppendLine("Период (с);Период (по);Календ. дней;Ср. дневной (gross);Расчёт (gross);Расчёт (net);Выплачено (net);Разница (net)");
            foreach (var r in vacationPayResults)
            {
                var diff = r.DifferenceNet.HasValue ? r.DifferenceNet.Value.ToString("N2") : "—";
                csv.AppendLine($"{r.From:dd.MM.yyyy};{r.To:dd.MM.yyyy};{r.CalendarDays};{r.AvgDailyGross:N2};{r.CalculatedGross:N2};{r.CalculatedNet:N2};{r.PaidNet:N2};{diff}");
            }
            csv.AppendLine($"ИТОГО;;{vacationPayResults.Sum(x => x.CalendarDays)};;;{vacationPayResults.Sum(x => x.CalculatedNet):N2};{vacationPayResults.Sum(x => x.PaidNet):N2};{vacationPayResults.Sum(x => x.DifferenceNet ?? 0):N2}");
        }

        if (unusedVacationCompensation != null)
        {
            csv.AppendLine();
            csv.AppendLine("=== КОМПЕНСАЦИЯ ЗА НЕИСПОЛЬЗОВАННЫЙ ОТПУСК ПРИ УВОЛЬНЕНИИ ===");
            csv.AppendLine("Параметр;Значение");
            csv.AppendLine($"Дата найма;{unusedVacationCompensation.HireDate:dd.MM.yyyy}");
            csv.AppendLine($"Дата увольнения;{unusedVacationCompensation.DismissalDate:dd.MM.yyyy}");
            csv.AppendLine($"Отработано месяцев;{unusedVacationCompensation.WorkMonths}");
            csv.AppendLine($"Заработано дней отпуска;{unusedVacationCompensation.EarnedVacationDays}");
            csv.AppendLine($"Использовано дней отпуска;{unusedVacationCompensation.UsedVacationDays}");
            csv.AppendLine($"Неиспользовано дней отпуска;{unusedVacationCompensation.UnusedVacationDays}");
            csv.AppendLine(";");
            csv.AppendLine("Без учета индексации:;");
            csv.AppendLine($"  Средний дневной заработок (gross);{unusedVacationCompensation.AvgDailyGrossWithoutIndexation:N2} руб.");
            csv.AppendLine($"  Компенсация (gross);{unusedVacationCompensation.CompensationGrossWithoutIndexation:N2} руб.");
            csv.AppendLine($"  Компенсация (net);{unusedVacationCompensation.CompensationNetWithoutIndexation:N2} руб.");
            csv.AppendLine(";");
            csv.AppendLine("С учетом индексации (как должно быть):;");
            csv.AppendLine($"  Средний дневной заработок (gross);{unusedVacationCompensation.AvgDailyGross:N2} руб.");
            csv.AppendLine($"  Компенсация (gross);{unusedVacationCompensation.CompensationGross:N2} руб.");
            csv.AppendLine($"  Компенсация (net);{unusedVacationCompensation.CompensationNet:N2} руб.");
            csv.AppendLine(";");
            csv.AppendLine("Разница из-за непроведенной индексации:;");
            csv.AppendLine($"  Разница (gross);{unusedVacationCompensation.DifferenceGross:N2} руб.");
            csv.AppendLine($"  Разница (net);{unusedVacationCompensation.DifferenceNet:N2} руб.");
        }

        File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
