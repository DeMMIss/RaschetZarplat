using System.Text.Json.Serialization;

namespace РасчетЗадолженностиЗП.Models;

public class ConfigData
{
    [JsonPropertyName("salary")]
    public SalaryConfig? Salary { get; set; }

    [JsonPropertyName("indexation")]
    public IndexationConfig? Indexation { get; set; }

    [JsonPropertyName("calculation")]
    public CalculationConfig? Calculation { get; set; }

    [JsonPropertyName("holidayWork")]
    public List<string>? HolidayWork { get; set; }

    [JsonPropertyName("sickLeaves")]
    public List<SickLeaveConfig>? SickLeaves { get; set; }

    [JsonPropertyName("vacations")]
    public List<VacationConfig>? Vacations { get; set; }
}

public class SalaryConfig
{
    [JsonPropertyName("monthlySalary")]
    public decimal MonthlySalary { get; set; }

    [JsonPropertyName("salaryType")]
    public string SalaryType { get; set; } = "Net";

    [JsonPropertyName("advancePayDay")]
    public int AdvancePayDay { get; set; }

    [JsonPropertyName("settlementPayDay")]
    public int SettlementPayDay { get; set; }

    [JsonPropertyName("probationSalary")]
    public decimal? ProbationSalary { get; set; }

    [JsonPropertyName("probationPeriodMonths")]
    public int? ProbationPeriodMonths { get; set; }
}

public class IndexationConfig
{
    [JsonPropertyName("hireDate")]
    public string? HireDate { get; set; }

    [JsonPropertyName("baseSalaryNet")]
    public decimal? BaseSalaryNet { get; set; }

    [JsonPropertyName("indexationEvents")]
    public List<IndexationEventConfig>? IndexationEvents { get; set; }
}

public class IndexationEventConfig
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("percent")]
    public decimal Percent { get; set; }

    [JsonPropertyName("isPerformed")]
    public bool IsPerformed { get; set; }
}

public class CalculationConfig
{
    [JsonPropertyName("calculationDate")]
    public string CalculationDate { get; set; } = string.Empty;
}

public class SickLeaveConfig
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}

public class VacationConfig
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}
