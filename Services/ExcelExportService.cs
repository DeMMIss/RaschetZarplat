using ClosedXML.Excel;
using РасчетЗадолженностиЗП.Models;

namespace РасчетЗадолженностиЗП.Services;

public class ExcelExportService
{
    public void Export(SalaryInput input, List<PaymentRecord> records, List<VacationPayResult> vacationPayResults, UnusedVacationCompensation? unusedVacationCompensation, string filePath)
    {
        using var workbook = new XLWorkbook();

        var hasIndexation = input.IndexationRules.Any(r => !r.IsPerformed);
        
        CreateParametersSheet(workbook, input);
        CreateMainSheet(workbook, records, input, hasIndexation);
        
        if (hasIndexation)
        {
            CreateCompensationDetailSheet(workbook, records);
        }

        if (vacationPayResults.Count > 0)
        {
            CreateVacationPaySheet(workbook, vacationPayResults);
        }

        if (unusedVacationCompensation != null)
        {
            CreateUnusedVacationCompensationSheet(workbook, unusedVacationCompensation);
        }

        workbook.SaveAs(filePath);
    }

    private static void CreateParametersSheet(XLWorkbook workbook, SalaryInput input)
    {
        var ws = workbook.Worksheets.Add("Параметры");

        ws.Cell("A1").Value = "Параметр";
        ws.Cell("B1").Value = "Значение";
        StyleHeader(ws.Range("A1:B1"));

        var data = new List<(string param, string value)>
        {
            ("Оклад", $"{input.MonthlySalary:N2} руб."),
            ("Тип оклада", input.SalaryType == SalaryType.Net ? "Net (на руки)" : "Gross (до НДФЛ)"),
            ("Оклад (gross)", $"{input.GrossSalary:N2} руб."),
            ("День выплаты аванса", input.AdvancePayDay.ToString()),
            ("День выплаты расчёта", input.SettlementPayDay.ToString()),
        };

        if (input.HireDate.HasValue)
        {
            data.Add(("Дата найма", input.HireDate.Value.ToString("dd.MM.yyyy")));
        }

        if (input.ProbationSalary.HasValue)
        {
            data.Add(("Оклад на испытательном сроке", $"{input.ProbationSalary.Value:N2} руб. ({(input.SalaryType == SalaryType.Net ? "Net" : "Gross")})"));
        }

        if (input.ProbationPeriodMonths.HasValue)
        {
            data.Add(("Длительность испытательного срока", $"{input.ProbationPeriodMonths.Value} мес."));
            if (input.HireDate.HasValue)
            {
                var probationEnd = input.HireDate.Value.AddMonths(input.ProbationPeriodMonths.Value);
                data.Add(("Окончание испытательного срока", probationEnd.ToString("dd.MM.yyyy")));
            }
        }

        if (input.BaseSalary.HasValue)
        {
            data.Add(("Базовая зарплата (net)", $"{input.BaseSalary.Value:N2} руб."));
        }

        if (input.IndexationRules.Count > 0)
        {
            var unperformedCount = input.IndexationRules.Count(r => !r.IsPerformed);
            data.Add(("Правила индексации", $"{input.IndexationRules.Count} шт. (не проведено: {unperformedCount})"));
            foreach (var rule in input.IndexationRules.OrderBy(r => r.Date))
            {
                var status = rule.IsPerformed ? "проведена" : "не проведена";
                var freq = rule.FrequencyMonths.HasValue ? $" (каждые {rule.FrequencyMonths} мес.)" : "";
                data.Add(($"  - {rule.Date:dd.MM.yyyy}", $"{rule.Percent}%{freq} ({status})"));
            }
        }

        data.Add(("Дата расчёта задолженности", input.CalculationDate.ToString("dd.MM.yyyy")));

        if (input.HolidayWorkDates.Count > 0)
        {
            data.Add(("Рабочие дни в праздники", string.Join(", ", input.HolidayWorkDates.OrderBy(d => d).Select(d => d.ToString("dd.MM.yyyy")))));
        }

        if (input.SickLeaves.Count > 0)
        {
            data.Add(("Больничные", $"{input.SickLeaves.Count} период(ов)"));
        }

        if (input.Vacations.Count > 0)
        {
            data.Add(("Отпуска", $"{input.Vacations.Count} период(ов)"));
        }

        for (var i = 0; i < data.Count; i++)
        {
            ws.Cell(i + 2, 1).Value = data[i].param;
            ws.Cell(i + 2, 2).Value = data[i].value;
        }

        ws.Columns().AdjustToContents();
    }

    private static void CreateMainSheet(XLWorkbook workbook, List<PaymentRecord> records, SalaryInput input, bool hasIndexation)
    {
        var ws = workbook.Worksheets.Add(hasIndexation ? "Расчёт задолженности" : "Отчёт по зарплате");

        var unperformedIndexations = input.IndexationRules.Where(r => !r.IsPerformed).ToList();
        var firstIndexationDate = hasIndexation
            ? unperformedIndexations.OrderBy(r => r.Date).First().Date
            : (DateTime?)null;

        string[] headers;
        if (hasIndexation)
        {
            headers = new[]
            {
                "Период", "Тип", "Дата выплаты",
                "Без индексации (gross)", "Без индексации (net)",
                "С индексацией (gross)", "С индексацией (net)",
                "Недоплата (net)", "Дней просрочки", "Компенсация", "Подлежала индексации"
            };
        }
        else
        {
            headers = new[]
            {
                "Период", "Тип", "Дата выплаты",
                "Сумма (gross)", "Сумма (net)"
            };
        }

        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        StyleHeader(ws.Range(1, 1, 1, headers.Length));

        for (var i = 0; i < records.Count; i++)
        {
            var r = records[i];
            var row = i + 2;

            ws.Cell(row, 1).Value = r.PeriodDisplay;
            ws.Cell(row, 2).Value = r.TypeDisplay;
            ws.Cell(row, 3).Value = r.PaymentDate.ToString("dd.MM.yyyy");

            if (hasIndexation)
            {
                var wasIndexed = !firstIndexationDate.HasValue || r.PaymentDate >= firstIndexationDate.Value;

                ws.Cell(row, 4).Value = r.GrossAmount;
                ws.Cell(row, 5).Value = r.NetAmount;
                ws.Cell(row, 6).Value = r.IndexedGrossAmount;
                ws.Cell(row, 7).Value = r.IndexedNetAmount;
                ws.Cell(row, 8).Value = r.Underpayment;
                ws.Cell(row, 9).Value = r.DelayDays;
                ws.Cell(row, 10).Value = r.Compensation;
                ws.Cell(row, 11).Value = wasIndexed ? "Да" : "Нет";

                for (var c = 4; c <= 8; c++)
                    ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";

                if (!wasIndexed)
                {
                    ws.Range(row, 1, row, 11).Style.Fill.BackgroundColor = XLColor.LightYellow;
                }
            }
            else
            {
                ws.Cell(row, 4).Value = r.GrossAmount;
                ws.Cell(row, 5).Value = r.NetAmount;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            }
        }

        var totalRow = records.Count + 2;
        ws.Cell(totalRow, 1).Value = "ИТОГО";
        ws.Cell(totalRow, 1).Style.Font.Bold = true;
        
        if (hasIndexation)
        {
            ws.Cell(totalRow, 8).Value = records.Sum(r => r.Underpayment);
            ws.Cell(totalRow, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(totalRow, 8).Style.Font.Bold = true;
            ws.Cell(totalRow, 10).Value = records.Sum(r => r.Compensation);
            ws.Cell(totalRow, 10).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(totalRow, 10).Style.Font.Bold = true;

            var grandTotalRow = totalRow + 1;
            ws.Cell(grandTotalRow, 1).Value = "ИТОГО К ВЗЫСКАНИЮ";
            ws.Cell(grandTotalRow, 1).Style.Font.Bold = true;
            ws.Cell(grandTotalRow, 8).Value = records.Sum(r => r.Underpayment) + records.Sum(r => r.Compensation);
            ws.Cell(grandTotalRow, 8).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(grandTotalRow, 8).Style.Font.Bold = true;
            
            if (firstIndexationDate.HasValue)
            {
                var noteRow = grandTotalRow + 2;
                ws.Cell(noteRow, 1).Value = "Примечание:";
                ws.Cell(noteRow, 1).Style.Font.Bold = true;
                ws.Cell(noteRow + 1, 1).Value = $"Выплаты до {firstIndexationDate.Value:dd.MM.yyyy} не подлежали индексации и выделены желтым цветом.";
                ws.Range(noteRow + 1, 1, noteRow + 1, 11).Merge();
            }
        }
        else
        {
            ws.Cell(totalRow, 4).Value = records.Sum(r => r.GrossAmount);
            ws.Cell(totalRow, 5).Value = records.Sum(r => r.NetAmount);
            ws.Cell(totalRow, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(totalRow, 4).Style.Font.Bold = true;
            ws.Cell(totalRow, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(totalRow, 5).Style.Font.Bold = true;
        }

        ws.Columns().AdjustToContents();
    }

    private static void CreateCompensationDetailSheet(XLWorkbook workbook, List<PaymentRecord> records)
    {
        var ws = workbook.Worksheets.Add("Детализация компенсации");

        string[] headers =
        {
            "Период", "Тип", "Дата выплаты", "Недоплата",
            "Период (с)", "Период (по)", "Дней", "Ключевая ставка %",
            "Дневная ставка", "Компенсация"
        };

        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        StyleHeader(ws.Range(1, 1, 1, headers.Length));

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

        var row = 2;
        foreach (var grouped in groupedDetails)
        {
            ws.Cell(row, 1).Value = grouped.PeriodDisplay;
            ws.Cell(row, 2).Value = grouped.TypeDisplay;
            ws.Cell(row, 3).Value = grouped.PaymentDate.ToString("dd.MM.yyyy");
            ws.Cell(row, 4).Value = grouped.Underpayment;
            ws.Cell(row, 5).Value = grouped.From.ToString("dd.MM.yyyy");
            ws.Cell(row, 6).Value = grouped.To.ToString("dd.MM.yyyy");
            ws.Cell(row, 7).Value = grouped.Days;
            ws.Cell(row, 8).Value = grouped.KeyRate;
            ws.Cell(row, 9).Value = grouped.DailyRate;
            ws.Cell(row, 10).Value = grouped.Amount;

            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 9).Style.NumberFormat.Format = "0.00000000";
            ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";

            row++;
        }

        var totalRow = row;
        ws.Cell(totalRow, 1).Value = "ИТОГО";
        ws.Cell(totalRow, 1).Style.Font.Bold = true;
        ws.Cell(totalRow, 10).Value = records.Sum(r => r.Compensation);
        ws.Cell(totalRow, 10).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(totalRow, 10).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
    }

    private static void CreateVacationPaySheet(XLWorkbook workbook, List<VacationPayResult> results)
    {
        var ws = workbook.Worksheets.Add("Отпускные");
        string[] headers = { "Период (с)", "Период (по)", "Календ. дней", "Ср. дневной (gross)", "Расчёт (gross)", "Расчёт (net)", "Выплачено (net)", "Разница (net)" };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        StyleHeader(ws.Range(1, 1, 1, headers.Length));

        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var row = i + 2;
            ws.Cell(row, 1).Value = r.From.ToString("dd.MM.yyyy");
            ws.Cell(row, 2).Value = r.To.ToString("dd.MM.yyyy");
            ws.Cell(row, 3).Value = r.CalendarDays;
            ws.Cell(row, 4).Value = r.AvgDailyGross;
            ws.Cell(row, 5).Value = r.CalculatedGross;
            ws.Cell(row, 6).Value = r.CalculatedNet;
            ws.Cell(row, 7).Value = r.PaidNet;
            ws.Cell(row, 8).Value = r.DifferenceNet ?? 0;
            for (var c = 4; c <= 8; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.00";
        }
        var totalRow = results.Count + 2;
        ws.Cell(totalRow, 1).Value = "ИТОГО";
        ws.Cell(totalRow, 1).Style.Font.Bold = true;
        ws.Cell(totalRow, 3).Value = results.Sum(x => x.CalendarDays);
        ws.Cell(totalRow, 6).Value = results.Sum(x => x.CalculatedNet);
        ws.Cell(totalRow, 6).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(totalRow, 6).Style.Font.Bold = true;
        ws.Cell(totalRow, 7).Value = results.Sum(x => x.PaidNet);
        ws.Cell(totalRow, 7).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(totalRow, 7).Style.Font.Bold = true;
        ws.Cell(totalRow, 8).Value = results.Sum(x => x.DifferenceNet ?? 0);
        ws.Cell(totalRow, 8).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(totalRow, 8).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
    }

    private static void CreateUnusedVacationCompensationSheet(XLWorkbook workbook, UnusedVacationCompensation compensation)
    {
        var ws = workbook.Worksheets.Add("Компенс. неисп. отпуск");
        
        ws.Cell("A1").Value = "Параметр";
        ws.Cell("B1").Value = "Значение";
        StyleHeader(ws.Range("A1:B1"));

        var data = new List<(string param, string value)>
        {
            ("Дата найма", compensation.HireDate.ToString("dd.MM.yyyy")),
            ("Дата увольнения", compensation.DismissalDate.ToString("dd.MM.yyyy")),
            ("Отработано месяцев", compensation.WorkMonths.ToString()),
            ("Заработано дней отпуска", compensation.EarnedVacationDays.ToString()),
            ("Использовано дней отпуска", compensation.UsedVacationDays.ToString()),
            ("Неиспользовано дней отпуска", compensation.UnusedVacationDays.ToString()),
            ("", ""),
            ("Без учета индексации:", ""),
            ("  Средний дневной заработок (gross)", $"{compensation.AvgDailyGrossWithoutIndexation:N2} руб."),
            ("  Компенсация (gross)", $"{compensation.CompensationGrossWithoutIndexation:N2} руб."),
            ("  Компенсация (net)", $"{compensation.CompensationNetWithoutIndexation:N2} руб."),
            ("", ""),
            ("С учетом индексации (как должно быть):", ""),
            ("  Средний дневной заработок (gross)", $"{compensation.AvgDailyGross:N2} руб."),
            ("  Компенсация (gross)", $"{compensation.CompensationGross:N2} руб."),
            ("  Компенсация (net)", $"{compensation.CompensationNet:N2} руб."),
            ("", ""),
            ("Разница из-за непроведенной индексации:", ""),
            ("  Разница (gross)", $"{compensation.DifferenceGross:N2} руб."),
            ("  Разница (net)", $"{compensation.DifferenceNet:N2} руб.")
        };

        for (var i = 0; i < data.Count; i++)
        {
            ws.Cell(i + 2, 1).Value = data[i].param;
            ws.Cell(i + 2, 2).Value = data[i].value;
        }

        ws.Columns().AdjustToContents();
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }
}
