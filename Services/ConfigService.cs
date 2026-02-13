using System.Globalization;
using System.Text.Json;
using РасчетЗадолженностиЗП.Models;

namespace РасчетЗадолженностиЗП.Services;

public class ConfigService
{
    public ConfigData LoadConfig(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Файл конфигурации не найден: {path}");

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var config = JsonSerializer.Deserialize<ConfigData>(json, options);
        if (config == null)
            throw new InvalidOperationException("Не удалось десериализовать конфигурацию");

        ValidateConfig(config);
        return config;
    }

    public SalaryInput ConvertToSalaryInput(ConfigData config)
    {
        if (config.Salary == null)
            throw new InvalidOperationException("Отсутствует секция salary в конфигурации");

        var input = new SalaryInput
        {
            MonthlySalary = config.Salary.MonthlySalary,
            SalaryType = config.Salary.SalaryType.Equals("Gross", StringComparison.OrdinalIgnoreCase)
                ? SalaryType.Gross
                : SalaryType.Net,
            AdvancePayDay = config.Salary.AdvancePayDay,
            SettlementPayDay = config.Salary.SettlementPayDay,
            ProbationSalary = config.Salary.ProbationSalary,
            ProbationPeriodMonths = config.Salary.ProbationPeriodMonths
        };

        if (config.Calculation != null)
        {
            if (DateTime.TryParseExact(config.Calculation.CalculationDate, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var calcDate))
            {
                input.CalculationDate = calcDate;
            }
        }

        if (config.Indexation != null)
        {
            if (!string.IsNullOrEmpty(config.Indexation.HireDate) &&
                DateTime.TryParseExact(config.Indexation.HireDate, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var hireDate))
            {
                input.HireDate = hireDate;
                input.BaseIndexationDate = hireDate;
            }

            if (config.Indexation.BaseSalaryNet.HasValue)
            {
                input.BaseSalary = config.Indexation.BaseSalaryNet.Value;
            }

            if (config.Indexation.IndexationEvents != null)
            {
                foreach (var eventConfig in config.Indexation.IndexationEvents)
                {
                    if (DateTime.TryParseExact(eventConfig.Date, "yyyy-MM-dd",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out var eventDate))
                    {
                        input.IndexationRules.Add(new IndexationRule
                        {
                            Date = eventDate,
                            Percent = eventConfig.Percent,
                            FrequencyMonths = null,
                            IsPerformed = eventConfig.IsPerformed
                        });
                    }
                }
            }
        }

        if (config.HolidayWork != null)
        {
            foreach (var dateStr in config.HolidayWork)
            {
                if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    input.HolidayWorkDates.Add(date);
                }
            }
        }

        if (config.SickLeaves != null)
        {
            foreach (var sickLeaveConfig in config.SickLeaves)
            {
                if (DateTime.TryParseExact(sickLeaveConfig.From, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) &&
                    DateTime.TryParseExact(sickLeaveConfig.To, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
                {
                    input.SickLeaves.Add(new SickLeavePeriod
                    {
                        From = from,
                        To = to,
                        Amount = sickLeaveConfig.Amount
                    });
                }
            }
        }

        if (config.Vacations != null)
        {
            foreach (var vacationConfig in config.Vacations)
            {
                if (DateTime.TryParseExact(vacationConfig.From, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) &&
                    DateTime.TryParseExact(vacationConfig.To, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
                {
                    input.Vacations.Add(new VacationPeriod
                    {
                        From = from,
                        To = to,
                        Amount = vacationConfig.Amount
                    });
                }
            }
        }

        return input;
    }

    public void SaveConfig(SalaryInput input, string path)
    {
        var config = new ConfigData
        {
            Salary = new SalaryConfig
            {
                MonthlySalary = input.MonthlySalary,
                SalaryType = input.SalaryType == SalaryType.Gross ? "Gross" : "Net",
                AdvancePayDay = input.AdvancePayDay,
                SettlementPayDay = input.SettlementPayDay,
                ProbationSalary = input.ProbationSalary,
                ProbationPeriodMonths = input.ProbationPeriodMonths
            },
            Calculation = new CalculationConfig
            {
                CalculationDate = input.CalculationDate.ToString("yyyy-MM-dd")
            },
            Indexation = new IndexationConfig
            {
                HireDate = input.HireDate?.ToString("yyyy-MM-dd"),
                BaseSalaryNet = input.BaseSalary,
                IndexationEvents = input.IndexationRules.Select(r => new IndexationEventConfig
                {
                    Date = r.Date.ToString("yyyy-MM-dd"),
                    Percent = r.Percent,
                    IsPerformed = r.IsPerformed
                }).ToList()
            },
            HolidayWork = input.HolidayWorkDates.Select(d => d.ToString("yyyy-MM-dd")).ToList(),
            SickLeaves = input.SickLeaves.Select(sl => new SickLeaveConfig
            {
                From = sl.From.ToString("yyyy-MM-dd"),
                To = sl.To.ToString("yyyy-MM-dd"),
                Amount = sl.Amount
            }).ToList(),
            Vacations = input.Vacations.Select(v => new VacationConfig
            {
                From = v.From.ToString("yyyy-MM-dd"),
                To = v.To.ToString("yyyy-MM-dd"),
                Amount = v.Amount
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(path, json);
    }

    public void ValidateConfig(ConfigData config)
    {
        if (config.Salary == null)
            throw new InvalidOperationException("Отсутствует секция salary");

        if (config.Salary.MonthlySalary <= 0)
            throw new InvalidOperationException("monthlySalary должен быть больше 0");

        if (config.Salary.AdvancePayDay < 1 || config.Salary.AdvancePayDay > 31)
            throw new InvalidOperationException("advancePayDay должен быть от 1 до 31");

        if (config.Salary.SettlementPayDay < 1 || config.Salary.SettlementPayDay > 28)
            throw new InvalidOperationException("settlementPayDay должен быть от 1 до 28");

        if (config.Calculation == null)
            throw new InvalidOperationException("Отсутствует секция calculation");

        if (string.IsNullOrEmpty(config.Calculation.CalculationDate))
            throw new InvalidOperationException("calculationDate не указан");

        if (config.Indexation?.IndexationEvents != null)
        {
            foreach (var evt in config.Indexation.IndexationEvents)
            {
                if (string.IsNullOrEmpty(evt.Date))
                    throw new InvalidOperationException("Дата события индексации не указана");

                if (evt.Percent < 0)
                    throw new InvalidOperationException("Процент индексации не может быть отрицательным");
            }
        }
    }
}
