using System.Globalization;
using РасчетЗадолженностиЗП.Models;
using РасчетЗадолженностиЗП.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║   Расчёт задолженности ЗП из-за непроведённой индексации    ║");
Console.WriteLine("║            Компенсация по ст. 236 ТК РФ                     ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var input = new SalaryInput();

Console.Write("Тип оклада (1 — gross, 2 — net на руки): ");
var salaryTypeStr = Console.ReadLine()?.Trim();
input.SalaryType = salaryTypeStr == "2" ? SalaryType.Net : SalaryType.Gross;

Console.Write($"Размер месячного оклада ({(input.SalaryType == SalaryType.Gross ? "gross" : "net")}), руб.: ");
input.MonthlySalary = ReadDecimal();

Console.Write("День выплаты аванса (число месяца, например 25): ");
input.AdvancePayDay = ReadInt(1, 31);

Console.Write("День выплаты расчёта (число следующего месяца, например 10): ");
input.SettlementPayDay = ReadInt(1, 28);

Console.Write("Процент индексации (%): ");
input.IndexationPercent = ReadDecimal();

Console.Write("Дата, с которой должна была быть индексация (дд.мм.гггг): ");
input.IndexationDate = ReadDate();

Console.Write("Дата расчёта задолженности (дд.мм.гггг): ");
input.CalculationDate = ReadDate();

Console.WriteLine();
Console.WriteLine("Были ли рабочие дни в праздники? (оплата = суточная ставка * 2)");
Console.Write("Введите даты через запятую (дд.мм.гггг, дд.мм.гггг, ...) или оставьте пустым: ");
var holidayLine = Console.ReadLine()?.Trim();
if (!string.IsNullOrWhiteSpace(holidayLine))
{
    var parts = holidayLine.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var part in parts)
    {
        if (DateTime.TryParseExact(part, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var hDate))
        {
            input.HolidayWorkDates.Add(hDate);
        }
        else
        {
            Console.WriteLine($"  Пропущена некорректная дата: {part}");
        }
    }

    if (input.HolidayWorkDates.Count > 0)
        Console.WriteLine($"  Учтено рабочих праздничных дней: {input.HolidayWorkDates.Count}");
}

Console.WriteLine();
Console.WriteLine($"  Оклад (gross): {input.GrossSalary:N2} руб.");
Console.WriteLine($"  Индексированный оклад (gross): {input.IndexedGrossSalary:N2} руб.");
Console.WriteLine();

using var httpClient = new HttpClient();
httpClient.Timeout = TimeSpan.FromSeconds(30);

Console.Write("Загрузка производственного календаря...");
var calendarService = new ProductionCalendarService(httpClient);
try
{
    await calendarService.Load(input.IndexationDate.Year, input.CalculationDate.Year);
    Console.WriteLine(" OK");
}
catch (Exception ex)
{
    Console.WriteLine($" ОШИБКА: {ex.Message}");
    return;
}

Console.Write("Загрузка ключевой ставки ЦБ РФ...");
var keyRateService = new CbKeyRateService(httpClient);
try
{
    await keyRateService.Load(
        input.IndexationDate.AddMonths(-1),
        input.CalculationDate.AddDays(1));
    Console.WriteLine(" OK");
}
catch (Exception ex)
{
    Console.WriteLine($" ОШИБКА: {ex.Message}");
    return;
}

Console.WriteLine();
Console.WriteLine("Расчёт задолженности...");

var salaryService = new SalaryCalculationService(calendarService);
var records = salaryService.Calculate(input);

var compensationService = new CompensationCalculationService(keyRateService);
compensationService.CalculateCompensation(records, input.CalculationDate);

PrintResults(records, input);

var excelPath = Path.Combine(AppContext.BaseDirectory, "Расчет_задолженности_ЗП.xlsx");
try
{
    var excelService = new ExcelExportService();
    excelService.Export(input, records, excelPath);
    Console.WriteLine();
    Console.WriteLine($"Результаты сохранены в файл: {excelPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка при сохранении Excel: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("Нажмите любую клавишу для выхода...");
if (!Console.IsInputRedirected)
    Console.ReadKey();

static void PrintResults(List<PaymentRecord> records, SalaryInput input)
{
    Console.WriteLine();

    var divider = new string('─', 130);
    Console.WriteLine(divider);
    Console.WriteLine(
        $"{"Период",-10} {"Тип",-8} {"Дата выпл.",-12} " +
        $"{"Без инд.(net)",13} {"С инд.(net)",13} " +
        $"{"Недоплата",12} {"Дней",6} {"Компенсация",13}");
    Console.WriteLine(divider);

    foreach (var r in records)
    {
        Console.WriteLine(
            $"{r.PeriodDisplay,-10} {r.TypeDisplay,-8} {r.PaymentDate:dd.MM.yyyy}   " +
            $"{r.NetAmount,13:N2} {r.IndexedNetAmount,13:N2} " +
            $"{r.Underpayment,12:N2} {r.DelayDays,6} {r.Compensation,13:N2}");
    }

    Console.WriteLine(divider);

    var totalUnderpayment = records.Sum(r => r.Underpayment);
    var totalCompensation = records.Sum(r => r.Compensation);
    var grandTotal = totalUnderpayment + totalCompensation;

    Console.WriteLine(
        $"{"ИТОГО",-10} {"",- 8} {"",- 12} " +
        $"{"",13} {"",13} " +
        $"{totalUnderpayment,12:N2} {"",6} {totalCompensation,13:N2}");

    Console.WriteLine(divider);
    Console.WriteLine();
    Console.WriteLine($"  Общая недоплата:           {totalUnderpayment,15:N2} руб.");
    Console.WriteLine($"  Общая компенсация:         {totalCompensation,15:N2} руб.");
    Console.WriteLine($"  ─────────────────────────────────────────");
    Console.WriteLine($"  ИТОГО К ВЗЫСКАНИЮ:         {grandTotal,15:N2} руб.");
}

static decimal ReadDecimal()
{
    while (true)
    {
        var str = Console.ReadLine()?.Trim().Replace(',', '.');
        if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) && value > 0)
            return value;
        Console.Write("  Введите корректное число: ");
    }
}

static int ReadInt(int min, int max)
{
    while (true)
    {
        var str = Console.ReadLine()?.Trim();
        if (int.TryParse(str, out var value) && value >= min && value <= max)
            return value;
        Console.Write($"  Введите число от {min} до {max}: ");
    }
}

static DateTime ReadDate()
{
    while (true)
    {
        var str = Console.ReadLine()?.Trim();
        if (DateTime.TryParseExact(str, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            return date;
        Console.Write("  Введите дату в формате дд.мм.гггг: ");
    }
}
