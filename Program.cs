using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using РасчетЗадолженностиЗП.Models;
using РасчетЗадолженностиЗП.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.InputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║   Расчёт задолженности ЗП из-за непроведённой индексации    ║");
Console.WriteLine("║            Компенсация по ст. 236 ТК РФ                     ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

SalaryInput input;
var exportCsv = false;

Console.WriteLine("Выберите режим работы:");
Console.WriteLine("  1 — Использовать конфигурационный файл");
Console.WriteLine("  2 — Ввести данные вручную");
Console.Write("Ваш выбор (1 или 2): ");

var modeChoice = Console.ReadLine()?.Trim();
if (modeChoice == "1")
{
    input = LoadFromConfig();
}
else
{
    input = InputManually();
}

Console.Write("Экспортировать результаты в CSV? (y/n): ");
var csvChoice = Console.ReadLine()?.Trim().ToLower();
exportCsv = csvChoice == "y" || csvChoice == "yes" || csvChoice == "да";

Console.WriteLine();
var baseNet = Math.Round(input.GrossSalary * 0.87m, 2);
Console.WriteLine($"  Оклад: {input.GrossSalary:N2} / {baseNet:N2} руб. (gross / net)");

if (input.IndexationRules.Count > 0)
{
    var sortedRules = input.IndexationRules.OrderBy(r => r.Date).ToList();
    foreach (var rule in sortedRules)
    {
        var status = rule.IsPerformed ? "проведена" : "не проведена";
        if (!rule.IsPerformed)
        {
            var indexedGross = input.GetIndexedGrossSalary(rule.Date);
            var indexedNet = input.GetIndexedNetSalary(rule.Date);
            Console.WriteLine($"  Индексированный оклад на {rule.Date:dd.MM.yyyy} ({status}): {indexedGross:N2} / {indexedNet:N2} руб. (gross / net)");
        }
        else
        {
            Console.WriteLine($"  Индексация на {rule.Date:dd.MM.yyyy} ({status})");
        }
    }
}
Console.WriteLine();

using var httpClient = new HttpClient();
httpClient.Timeout = TimeSpan.FromSeconds(30);

var hasIndexation = input.IndexationRules.Any(r => !r.IsPerformed);

var unperformedIndexations = input.IndexationRules.Where(r => !r.IsPerformed).ToList();
var startYear = input.HireDate?.Year ?? (hasIndexation
    ? unperformedIndexations.Min(r => r.Date).Year
    : input.BaseIndexationDate?.Year ?? input.CalculationDate.Year);

Console.Write("Загрузка производственного календаря...");
var calendarService = new ProductionCalendarService(httpClient);
try
{
    await calendarService.Load(startYear, input.CalculationDate.Year);
    Console.WriteLine(" OK");
}
catch (Exception ex)
{
    Console.WriteLine($" ОШИБКА: {ex.Message}");
    return;
}

CbKeyRateService? keyRateService = null;
if (hasIndexation)
{
    var earliestIndexation = unperformedIndexations.Count > 0
        ? unperformedIndexations.Min(r => r.Date)
        : input.BaseIndexationDate ?? input.CalculationDate.AddMonths(-12);

    Console.Write("Загрузка ключевой ставки ЦБ РФ...");
    keyRateService = new CbKeyRateService(httpClient);
    try
    {
        await keyRateService.Load(
            earliestIndexation.AddMonths(-1),
            input.CalculationDate.AddDays(1));
        Console.WriteLine(" OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($" ОШИБКА: {ex.Message}");
        return;
    }
}

Console.WriteLine();

if (hasIndexation)
{
    Console.WriteLine("Расчёт задолженности...");
}
else
{
    Console.WriteLine("Формирование отчёта по зарплате...");
}

var salaryService = new SalaryCalculationService(calendarService);
var records = salaryService.Calculate(input);

if (hasIndexation && keyRateService != null)
{
    var compensationService = new CompensationCalculationService(keyRateService);
    compensationService.CalculateCompensation(records, input.CalculationDate);
}

PrintResults(records, input, hasIndexation);

var vacationPayService = new VacationPayService();
var vacationPayResults = input.Vacations.Count > 0
    ? vacationPayService.Calculate(input)
    : new List<VacationPayResult>();
PrintVacationPay(vacationPayResults);

var unusedVacationCompensation = vacationPayService.CalculateUnusedVacationCompensation(input);
if (unusedVacationCompensation != null)
{
    PrintUnusedVacationCompensation(unusedVacationCompensation);
}

var baseDirectory = GetApplicationDirectory();
Console.WriteLine($"[DEBUG] Базовая директория: {baseDirectory}");
var excelPath = Path.Combine(baseDirectory, "Расчет_задолженности_ЗП.xlsx");
try
{
    var excelService = new ExcelExportService();
    excelService.Export(input, records, vacationPayResults, unusedVacationCompensation, excelPath);
    Console.WriteLine();
    Console.WriteLine($"Результаты сохранены в файл: {excelPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка при сохранении Excel: {ex.Message}");
}

if (exportCsv)
{
    var csvPath = Path.Combine(baseDirectory, "Расчет_задолженности_ЗП.csv");
    try
    {
        var csvService = new CsvExportService();
        csvService.Export(input, records, vacationPayResults, unusedVacationCompensation, csvPath);
        Console.WriteLine($"Результаты сохранены в файл: {csvPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при сохранении CSV: {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine("Нажмите любую клавишу для выхода...");
if (!Console.IsInputRedirected)
    Console.ReadKey();

static SalaryInput LoadFromConfig()
{
    Console.Write("Путь к файлу конфигурации (Enter для config.json в текущей директории): ");
    var configPath = Console.ReadLine()?.Trim();
    if (string.IsNullOrWhiteSpace(configPath))
    {
        configPath = Path.Combine(GetApplicationDirectory(), "config.json");
    }

    var configService = new ConfigService();
    ConfigData config;
    try
    {
        config = configService.LoadConfig(configPath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка загрузки конфига: {ex.Message}");
        Console.WriteLine("Переключение на ручной ввод...");
        return InputManually();
    }

    var input = configService.ConvertToSalaryInput(config);

    Console.WriteLine();
    Console.WriteLine("═══════════════════════════════════════════════════════════");
    Console.WriteLine("Загруженные данные из конфига:");
    Console.WriteLine("═══════════════════════════════════════════════════════════");
    Console.WriteLine($"Оклад: {input.MonthlySalary:N2} руб. ({(input.SalaryType == SalaryType.Net ? "Net" : "Gross")})");
    if (input.ProbationSalary.HasValue)
    {
        Console.WriteLine($"Оклад на испытательном сроке: {input.ProbationSalary.Value:N2} руб. ({(input.SalaryType == SalaryType.Net ? "Net" : "Gross")})");
    }
    if (input.ProbationPeriodMonths.HasValue)
    {
        Console.WriteLine($"Длительность испытательного срока: {input.ProbationPeriodMonths.Value} мес.");
        if (input.HireDate.HasValue)
        {
            var probationEnd = input.HireDate.Value.AddMonths(input.ProbationPeriodMonths.Value);
            Console.WriteLine($"Окончание испытательного срока: {probationEnd:dd.MM.yyyy}");
        }
    }
    Console.WriteLine($"День выплаты аванса: {input.AdvancePayDay}");
    Console.WriteLine($"День выплаты расчёта: {input.SettlementPayDay}");
    Console.WriteLine($"Дата расчёта: {input.CalculationDate:dd.MM.yyyy}");
    if (input.HireDate.HasValue)
    {
        Console.WriteLine($"Дата найма: {input.HireDate.Value:dd.MM.yyyy}");
    }
    if (input.BaseSalary.HasValue)
    {
        Console.WriteLine($"Базовая зарплата (net): {input.BaseSalary.Value:N2} руб.");
    }
    Console.WriteLine($"Событий индексации: {input.IndexationRules.Count}");
    if (input.IndexationRules.Count > 0)
    {
        foreach (var rule in input.IndexationRules.OrderBy(r => r.Date))
        {
            var status = rule.IsPerformed ? "проведена" : "не проведена";
            Console.WriteLine($"  - {rule.Date:dd.MM.yyyy}: {rule.Percent}% ({status})");
        }
    }
    Console.WriteLine($"Рабочих дней в праздники: {input.HolidayWorkDates.Count}");
    Console.WriteLine($"Больничных периодов: {input.SickLeaves.Count}");
    Console.WriteLine($"Отпусков: {input.Vacations.Count}");
    Console.WriteLine("═══════════════════════════════════════════════════════════");
    Console.WriteLine();
    Console.Write("Использовать эти данные? (y/n, по умолчанию y): ");
    var confirm = Console.ReadLine()?.Trim().ToLower();
    if (confirm == "n" || confirm == "no" || confirm == "нет")
    {
        return InputManually();
    }

    return input;
}

static SalaryInput InputManually()
{
    var input = new SalaryInput();

    Console.Write("Тип оклада (1 — gross, 2 — net на руки): ");
    var salaryTypeStr = Console.ReadLine()?.Trim();
    input.SalaryType = salaryTypeStr == "2" ? SalaryType.Net : SalaryType.Gross;

    Console.Write($"Размер месячного оклада ({(input.SalaryType == SalaryType.Gross ? "gross" : "net")}), руб.: ");
    input.MonthlySalary = ReadDecimal();

    Console.WriteLine();
    Console.WriteLine("Испытательный срок:");
    Console.Write("Оклад на период испытательного срока (руб., Enter чтобы пропустить): ");
    var probationSalaryStr = Console.ReadLine()?.Trim();
    if (!string.IsNullOrWhiteSpace(probationSalaryStr))
    {
        if (decimal.TryParse(probationSalaryStr.Replace(',', '.'), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var probationSalary) && probationSalary > 0)
        {
            input.ProbationSalary = probationSalary;
        }
    }

    Console.Write("Длительность испытательного срока (месяцев, Enter чтобы пропустить): ");
    var probationMonthsStr = Console.ReadLine()?.Trim();
    if (!string.IsNullOrWhiteSpace(probationMonthsStr))
    {
        if (int.TryParse(probationMonthsStr, out var probationMonths) && probationMonths > 0)
        {
            input.ProbationPeriodMonths = probationMonths;
        }
    }

    Console.Write("День выплаты аванса (число месяца, например 25): ");
    input.AdvancePayDay = ReadInt(1, 31);

    Console.Write("День выплаты расчёта (число следующего месяца, например 10): ");
    input.SettlementPayDay = ReadInt(1, 28);

    Console.Write("Дата расчёта задолженности (дд.мм.гггг): ");
    input.CalculationDate = ReadDate();

    Console.WriteLine();
    Console.WriteLine("Настройка индексации:");
    Console.Write("Дата найма в компанию (дд.мм.гггг, Enter чтобы пропустить): ");
    var hireDateStr = Console.ReadLine()?.Trim();
    if (!string.IsNullOrWhiteSpace(hireDateStr))
    {
        if (DateTime.TryParseExact(hireDateStr, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var hireDate))
        {
            input.HireDate = hireDate;
            input.BaseIndexationDate = hireDate;
        }
    }

    Console.Write("Базовая зарплата (net), руб. (Enter чтобы использовать текущий оклад): ");
    var baseSalaryStr = Console.ReadLine()?.Trim();
    if (!string.IsNullOrWhiteSpace(baseSalaryStr))
    {
        if (decimal.TryParse(baseSalaryStr.Replace(',', '.'), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var baseSalary) && baseSalary > 0)
        {
            input.BaseSalary = baseSalary;
        }
    }

    Console.WriteLine();
    Console.WriteLine("Введите события с пропущенной индексацией (Enter для завершения):");
    var eventNumber = 1;
    while (true)
    {
        Console.Write($"Событие {eventNumber} - Дата индексации (дд.мм.гггг, Enter для завершения): ");
        var dateStr = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(dateStr))
            break;

        if (!DateTime.TryParseExact(dateStr, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            Console.WriteLine("  Некорректная дата, пропущено");
            continue;
        }

        Console.Write($"  Процент индексации (%): ");
        var percent = ReadDecimal();

        Console.Write($"  Индексация была проведена? (y/n, по умолчанию n): ");
        var performedStr = Console.ReadLine()?.Trim().ToLower();
        var isPerformed = performedStr == "y" || performedStr == "yes" || performedStr == "да";

        input.IndexationRules.Add(new IndexationRule
        {
            Date = date,
            Percent = percent,
            FrequencyMonths = null,
            IsPerformed = isPerformed
        });

        eventNumber++;
    }

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
    Console.WriteLine("Введите периоды больничных (Enter для завершения):");
    var sickLeaveNumber = 1;
    while (true)
    {
        Console.Write($"Больничный {sickLeaveNumber} - Дата начала (дд.мм.гггг, Enter для завершения): ");
        var fromStr = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(fromStr))
            break;

        if (!DateTime.TryParseExact(fromStr, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var from))
        {
            Console.WriteLine("  Некорректная дата, пропущено");
            continue;
        }

        Console.Write("  Дата окончания (дд.мм.гггг): ");
        if (!DateTime.TryParseExact(Console.ReadLine()?.Trim(), "dd.MM.yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
        {
            Console.WriteLine("  Некорректная дата, пропущено");
            continue;
        }

        Console.Write("  Сумма выплаты (net), руб.: ");
        var amount = ReadDecimal();

        input.SickLeaves.Add(new SickLeavePeriod
        {
            From = from,
            To = to,
            Amount = amount
        });

        sickLeaveNumber++;
    }

    Console.WriteLine();
    Console.WriteLine("Введите периоды отпусков (Enter для завершения):");
    var vacationNumber = 1;
    while (true)
    {
        Console.Write($"Отпуск {vacationNumber} - Дата начала (дд.мм.гггг, Enter для завершения): ");
        var fromStr = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(fromStr))
            break;

        if (!DateTime.TryParseExact(fromStr, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var from))
        {
            Console.WriteLine("  Некорректная дата, пропущено");
            continue;
        }

        Console.Write("  Дата окончания (дд.мм.гггг): ");
        if (!DateTime.TryParseExact(Console.ReadLine()?.Trim(), "dd.MM.yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
        {
            Console.WriteLine("  Некорректная дата, пропущено");
            continue;
        }

        Console.Write("  Сумма отпускных (net), руб.: ");
        var amount = ReadDecimal();

        input.Vacations.Add(new VacationPeriod
        {
            From = from,
            To = to,
            Amount = amount
        });

        vacationNumber++;
    }

    return input;
}

static void PrintResults(List<PaymentRecord> records, SalaryInput input, bool hasIndexation)
{
    Console.WriteLine();

    var divider = new string('─', hasIndexation ? 130 : 90);
    Console.WriteLine(divider);
    
    if (hasIndexation)
    {
        Console.WriteLine(
            $"{"Период",-10} {"Тип",-10} {"Дата выпл.",-12} " +
            $"{"Без инд.(net)",13} {"С инд.(net)",13} " +
            $"{"Недоплата",12} {"Дней",6} {"Компенсация",13}");
        Console.WriteLine(divider);

        foreach (var r in records)
        {
            Console.WriteLine(
                $"{r.PeriodDisplay,-10} {r.TypeDisplay,-10} {r.PaymentDate:dd.MM.yyyy}   " +
                $"{r.NetAmount,13:N2} {r.IndexedNetAmount,13:N2} " +
                $"{r.Underpayment,12:N2} {r.DelayDays,6} {r.Compensation,13:N2}");
        }

        Console.WriteLine(divider);

        var totalUnderpayment = records.Sum(r => r.Underpayment);
        var totalCompensation = records.Sum(r => r.Compensation);
        var grandTotal = totalUnderpayment + totalCompensation;

        Console.WriteLine(
            $"{"ИТОГО",-10} {"",-10} {"",-12} " +
            $"{"",13} {"",13} " +
            $"{totalUnderpayment,12:N2} {"",6} {totalCompensation,13:N2}");

        Console.WriteLine(divider);
        Console.WriteLine();
        Console.WriteLine($"  Общая недоплата:           {totalUnderpayment,15:N2} руб.");
        Console.WriteLine($"  Общая компенсация:         {totalCompensation,15:N2} руб.");
        Console.WriteLine($"  ─────────────────────────────────────────");
        Console.WriteLine($"  ИТОГО К ВЗЫСКАНИЮ:         {grandTotal,15:N2} руб.");
    }
    else
    {
        Console.WriteLine(
            $"{"Период",-10} {"Тип",-10} {"Дата выпл.",-12} " +
            $"{"Сумма (gross)",15} {"Сумма (net)",15}");
        Console.WriteLine(divider);

        foreach (var r in records)
        {
            Console.WriteLine(
                $"{r.PeriodDisplay,-10} {r.TypeDisplay,-10} {r.PaymentDate:dd.MM.yyyy}   " +
                $"{r.GrossAmount,15:N2} {r.NetAmount,15:N2}");
        }

        Console.WriteLine(divider);
        var totalGross = records.Sum(r => r.GrossAmount);
        var totalNet = records.Sum(r => r.NetAmount);
        Console.WriteLine(
            $"{"ИТОГО",-10} {"",-10} {"",-12} " +
            $"{totalGross,15:N2} {totalNet,15:N2}");
        Console.WriteLine(divider);
    }
}

static void PrintVacationPay(List<VacationPayResult> results)
{
    if (results.Count == 0)
        return;

    Console.WriteLine();
    Console.WriteLine("  ─── Отпускные ───");
    var div = new string('─', 100);
    Console.WriteLine(div);
    Console.WriteLine(
        $"{"Период",-22} {"Календ.дней",12} {"Ср.дневной",12} {"Расчёт (gross)",14} {"Расчёт (net)",14} {"Выплачено",12} {"Разница",10}");
    Console.WriteLine(div);
    foreach (var r in results)
    {
        var period = $"{r.From:dd.MM.yyyy} – {r.To:dd.MM.yyyy}";
        var diff = r.DifferenceNet.HasValue ? r.DifferenceNet.Value.ToString("N2") : "—";
        Console.WriteLine(
            $"{period,-22} {r.CalendarDays,12} {r.AvgDailyGross,12:N2} {r.CalculatedGross,14:N2} {r.CalculatedNet,14:N2} {r.PaidNet,12:N2} {diff,10}");
    }
    Console.WriteLine(div);
    var totalCalcNet = results.Sum(x => x.CalculatedNet);
    var totalPaid = results.Sum(x => x.PaidNet);
    var totalDiff = results.Sum(x => x.DifferenceNet ?? 0);
    Console.WriteLine($"{"ИТОГО",-22} {results.Sum(x => x.CalendarDays),12} {"",12} {"",14} {totalCalcNet,14:N2} {totalPaid,12:N2} {totalDiff,10:N2}");
    Console.WriteLine(div);
}

static void PrintUnusedVacationCompensation(UnusedVacationCompensation compensation)
{
    Console.WriteLine();
    Console.WriteLine("  ─── Компенсация за неиспользованный отпуск при увольнении ───");
    var div = new string('─', 100);
    Console.WriteLine(div);
    Console.WriteLine($"Дата найма:              {compensation.HireDate:dd.MM.yyyy}");
    Console.WriteLine($"Дата увольнения:         {compensation.DismissalDate:dd.MM.yyyy}");
    Console.WriteLine($"Отработано месяцев:      {compensation.WorkMonths}");
    Console.WriteLine($"Заработано дней отпуска: {compensation.EarnedVacationDays}");
    Console.WriteLine($"Использовано дней:       {compensation.UsedVacationDays}");
    Console.WriteLine($"Неиспользовано дней:     {compensation.UnusedVacationDays}");
    Console.WriteLine(div);
    Console.WriteLine("Без учета индексации:");
    Console.WriteLine($"  Средний дневной заработок (gross): {compensation.AvgDailyGrossWithoutIndexation:N2} руб.");
    Console.WriteLine($"  Компенсация (gross):                 {compensation.CompensationGrossWithoutIndexation:N2} руб.");
    Console.WriteLine($"  Компенсация (net):                   {compensation.CompensationNetWithoutIndexation:N2} руб.");
    Console.WriteLine(div);
    Console.WriteLine("С учетом индексации (как должно быть):");
    Console.WriteLine($"  Средний дневной заработок (gross): {compensation.AvgDailyGross:N2} руб.");
    Console.WriteLine($"  Компенсация (gross):                 {compensation.CompensationGross:N2} руб.");
    Console.WriteLine($"  Компенсация (net):                   {compensation.CompensationNet:N2} руб.");
    Console.WriteLine(div);
    Console.WriteLine("Разница из-за непроведенной индексации:");
    Console.WriteLine($"  Разница (gross):                     {compensation.DifferenceGross:N2} руб.");
    Console.WriteLine($"  Разница (net):                       {compensation.DifferenceNet:N2} руб.");
    Console.WriteLine(div);
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

static string GetApplicationDirectory()
{
    var workDir = Environment.GetEnvironmentVariable("APP_WORK_DIR");
    if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir))
    {
        return workDir;
    }
    
    try
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var fullPath = Path.GetFullPath(processPath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                return dir;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DEBUG] Environment.ProcessPath failed: {ex.Message}");
    }

    try
    {
        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            var dir = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (dir.Length > 0)
                return dir;
        }
    }
    catch { }

    try
    {
        using var process = Process.GetCurrentProcess();
        if (process.MainModule != null && !string.IsNullOrEmpty(process.MainModule.FileName))
        {
            var fullPath = Path.GetFullPath(process.MainModule.FileName);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                return dir;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DEBUG] Process.MainModule failed: {ex.Message}");
    }

    var currentDir = Directory.GetCurrentDirectory();
    if (!string.IsNullOrEmpty(currentDir) && Directory.Exists(currentDir))
        return currentDir;

    try
    {
        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
            return baseDir;
    }
    catch { }

    return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
}
