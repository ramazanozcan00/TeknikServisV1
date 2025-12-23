using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using TeknikServis.Core.DTOs;
using TeknikServis.Core.Interfaces;

namespace TeknikServis.Service.Services
{
    public class CurrencyService : ICurrencyService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public CurrencyService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<List<CurrencyDto>> GetDailyRatesAsync()
        {
            var currencyList = new List<CurrencyDto>();
            string url = "https://www.tcmb.gov.tr/kurlar/today.xml";

            try
            {
                var client = _httpClientFactory.CreateClient();
                var xmlString = await client.GetStringAsync(url);

                XDocument xDoc = XDocument.Parse(xmlString);

                // TCMB XML yapısına göre parse işlemi
                // Özellikle USD, EUR ve GBP gibi popüler kurları filtreleyebilir veya hepsini alabilirsiniz.
                var currencies = xDoc.Descendants("Currency")
                    .Where(x => x.Attribute("Kod")?.Value != "XDR"); // İsteğe bağlı filtreleme

                foreach (var item in currencies)
                {
                    var currency = new CurrencyDto
                    {
                        Code = item.Attribute("Kod")?.Value,
                        Name = item.Element("Isim")?.Value,
                        // Kültür bağımsız parse işlemi (TCMB nokta kullanır)
                        ForexBuying = ParseToDecimal(item.Element("ForexBuying")?.Value),
                        ForexSelling = ParseToDecimal(item.Element("ForexSelling")?.Value),
                        BanknoteBuying = ParseToDecimal(item.Element("BanknoteBuying")?.Value),
                        BanknoteSelling = ParseToDecimal(item.Element("BanknoteSelling")?.Value)
                    };

                    currencyList.Add(currency);
                }
            }
            catch (Exception ex)
            {
                // Hata loglama mekanizması eklenebilir
                Console.WriteLine("Döviz kuru çekilirken hata oluştu: " + ex.Message);
            }

            return currencyList;
        }

        private decimal ParseToDecimal(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            // TCMB verisi nokta (.) ondalık ayracı kullanır, bunu TR kültürüne uygun hale getiriyoruz veya Invariant kullanıyoruz.
            if (decimal.TryParse(value.Replace(".", ","), NumberStyles.Any, new CultureInfo("tr-TR"), out decimal result))
            {
                return result;
            }
            return 0;
        }
    }
}