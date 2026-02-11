using ClosedXML.Excel;
using РасчетЗадолженностиЗП.Models;

namespace РасчетЗадолженностиЗП.Services;

public class ExcelExportService
{
    public void Export(SalaryInput input, List<PaymentRecord> records, string filePath)
    {
        using var workbook = new XLWorkbook();

        CreateParametersSheet(workbook, input);
        CreateMainSheet(workbook, records);
        CreateCompensationDetailSheet(workbook, records);

        workbook.SaveAs(filePath);
    }

    private static void CreateParametersSheet(XLWorkbook workbook, SalaryInput input)
    {
        var ws = workbook.Worksheets.Add("Параметры");

        ws.Cell("A1").Value = "Параметр";
        ws.Cell("B1").Value = "Значение";
        StyleHeader(ws.Range("A1:B1"));

        var data = new (string param, string value)[]
        {
            ("Оклад", $"{input.MonthlySalary:N2} руб."),
            ("Тип оклада", input.SalaryType == SalaryType.Net ? "Net (на руки)" : "Gross (до НДФЛ)"),
            ("Оклад (gross)", $"{input.GrossSalary:N2} руб."),
            ("День выплаты аванса", input.AdvancePayDay.ToString()),
            ("День выплаты расчёта", input.SettlementPayDay.ToString()),
            ("Процент индексации", $"{input.IndexationPercent}%"),
            ("Индексированный оклад (gross)", $"{input.IndexedGrossSalary:N2} руб."),
            ("Дата индексации", input.IndexationDate.ToString("dd.MM.yyyy")),
            ("Дата расчёта задолженности", input.CalculationDate.ToString("dd.MM.yyyy")),
            ("Рабочие дни в праздники", input.HolidayWorkDates.Count > 0
                ? string.Join(", ", input.HolidayWorkDates.OrderBy(d => d).Select(d => d.ToString("dd.MM.yyyy")))
                : "нет"),
        };

        for (int i = 0; i < data.Length; i++)
        {
            ws.Cell(i + 2, 1).Value = data[i].param;
            ws.Cell(i + 2, 2).Value = data[i].value;
        }

        ws.Columns().AdjustToContents();
    }

    private static void CreateMainSheet(XLWorkbook workbook, List<PaymentRecord> records)
    {
        var ws = workbook.Worksheets.Add("Расчёт задолженности");

        string[] headers =
        {
            "Период", "Тип", "Дата выплаты",
            "Без индексации (gross)", "Без индексации (net)",
            "С индексацией (gross)", "С индексацией (net)",
            "Недоплата (net)", "Дней просрочки", "Компенсация"
        };

        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        StyleHeader(ws.Range(1, 1, 1, headers.Length));

        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            int row = i + 2;

            ws.Cell(row, 1).Value = r.PeriodDisplay;
            ws.Cell(row, 2).Value = r.TypeDisplay;
            ws.Cell(row, 3).Value = r.PaymentDate.ToString("dd.MM.yyyy");
            ws.Cell(row, 4).Value = r.GrossAmount;
            ws.Cell(row, 5).Value = r.NetAmount;
            ws.Cell(row, 6).Value = r.IndexedGrossAmount;
            ws.Cell(row, 7).Value = r.IndexedNetAmount;
            ws.Cell(row, 8).Value = r.Underpayment;
            ws.Cell(row, 9).Value = r.DelayDays;
            ws.Cell(row, 10).Value = r.Compensation;

            for (int c = 4; c <= 8; c++)
                ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";
        }

        int totalRow = records.Count + 2;
        ws.Cell(totalRow, 1).Value = "ИТОГО";
        ws.Cell(totalRow, 1).Style.Font.Bold = true;
        ws.Cell(totalRow, 8).Value = records.Sum(r => r.Underpayment);
        ws.Cell(totalRow, 8).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(totalRow, 8).Style.Font.Bold = true;
        ws.Cell(totalRow, 10).Value = records.Sum(r => r.Compensation);
        ws.Cell(totalRow, 10).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(totalRow, 10).Style.Font.Bold = true;

        int grandTotalRow = totalRow + 1;
        ws.Cell(grandTotalRow, 1).Value = "ИТОГО К ВЗЫСКАНИЮ";
        ws.Cell(grandTotalRow, 1).Style.Font.Bold = true;
        ws.Cell(grandTotalRow, 8).Value = records.Sum(r => r.Underpayment) + records.Sum(r => r.Compensation);
        ws.Cell(grandTotalRow, 8).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(grandTotalRow, 8).Style.Font.Bold = true;

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

        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        StyleHeader(ws.Range(1, 1, 1, headers.Length));

        int row = 2;
        foreach (var record in records)
        {
            if (record.CompensationDetails.Count == 0)
                continue;

            foreach (var detail in record.CompensationDetails)
            {
                ws.Cell(row, 1).Value = record.PeriodDisplay;
                ws.Cell(row, 2).Value = record.TypeDisplay;
                ws.Cell(row, 3).Value = record.PaymentDate.ToString("dd.MM.yyyy");
                ws.Cell(row, 4).Value = record.Underpayment;
                ws.Cell(row, 5).Value = detail.From.ToString("dd.MM.yyyy");
                ws.Cell(row, 6).Value = detail.To.ToString("dd.MM.yyyy");
                ws.Cell(row, 7).Value = detail.Days;
                ws.Cell(row, 8).Value = detail.KeyRate;
                ws.Cell(row, 9).Value = detail.DailyRate;
                ws.Cell(row, 10).Value = detail.Amount;

                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 9).Style.NumberFormat.Format = "0.00000000";
                ws.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";

                row++;
            }
        }

        int totalRow = row;
        ws.Cell(totalRow, 1).Value = "ИТОГО";
        ws.Cell(totalRow, 1).Style.Font.Bold = true;
        ws.Cell(totalRow, 10).Value = records.Sum(r => r.Compensation);
        ws.Cell(totalRow, 10).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(totalRow, 10).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }
}
