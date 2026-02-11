using System.Xml.Linq;
using РасчетЗадолженностиЗП.Models;

namespace РасчетЗадолженностиЗП.Services;

public class CbKeyRateService
{
    private readonly HttpClient _httpClient;
    private List<KeyRateRecord> _rates = new();

    public CbKeyRateService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task Load(DateTime from, DateTime to)
    {
        var soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <KeyRate xmlns=""http://web.cbr.ru/"">
      <fromDate>{from:yyyy-MM-dd}</fromDate>
      <ToDate>{to:yyyy-MM-dd}</ToDate>
    </KeyRate>
  </soap:Body>
</soap:Envelope>";

        var content = new StringContent(soapEnvelope, System.Text.Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", "http://web.cbr.ru/KeyRate");

        var response = await _httpClient.PostAsync(
            "https://www.cbr.ru/DailyInfoWebServ/DailyInfo.asmx", content);
        var xml = await response.Content.ReadAsStringAsync();

        ParseResponse(xml);
    }

    private void ParseResponse(string xml)
    {
        var doc = XDocument.Parse(xml);
        XNamespace diffgram = "urn:schemas-microsoft-com:xml-diffgram-v1";
        XNamespace ns = "";

        var diffgramElement = doc.Descendants(diffgram + "diffgram").FirstOrDefault();
        if (diffgramElement == null)
            return;

        var records = diffgramElement.Descendants("KR");

        _rates = records.Select(kr =>
        {
            var dateStr = kr.Element("DT")?.Value;
            var rateStr = kr.Element("Rate")?.Value;

            if (DateTime.TryParse(dateStr, out var date) &&
                decimal.TryParse(rateStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var rate))
            {
                return new KeyRateRecord { Date = date.Date, Rate = rate };
            }

            return null;
        })
        .Where(x => x != null)
        .Cast<KeyRateRecord>()
        .OrderBy(x => x.Date)
        .ToList();
    }

    public decimal GetRate(DateTime date)
    {
        if (_rates.Count == 0)
            throw new InvalidOperationException("Ключевые ставки ЦБ не загружены.");

        var applicable = _rates.Where(r => r.Date <= date).OrderByDescending(r => r.Date).FirstOrDefault();

        if (applicable == null)
            return _rates.First().Rate;

        return applicable.Rate;
    }

    public List<(DateTime From, DateTime To, decimal Rate)> GetRatePeriods(DateTime from, DateTime to)
    {
        var result = new List<(DateTime From, DateTime To, decimal Rate)>();

        if (_rates.Count == 0)
            return result;

        var relevantChanges = _rates
            .Where(r => r.Date > from && r.Date <= to)
            .OrderBy(r => r.Date)
            .ToList();

        var currentFrom = from;
        var currentRate = GetRate(from);

        foreach (var change in relevantChanges)
        {
            var periodEnd = change.Date.AddDays(-1);
            if (periodEnd >= currentFrom)
            {
                result.Add((currentFrom, periodEnd, currentRate));
            }
            currentFrom = change.Date;
            currentRate = change.Rate;
        }

        if (currentFrom <= to)
        {
            result.Add((currentFrom, to, currentRate));
        }

        return result;
    }

    public bool IsLoaded => _rates.Count > 0;
}
