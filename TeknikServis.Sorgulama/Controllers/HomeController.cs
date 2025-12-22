using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json; // NuGet'ten Newtonsoft.Json paketini yükleyin

namespace TeknikServis.Sorgulama.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        // ANA SUNUCUNUN ADRESÝNÝ BURAYA YAZIN
        // ESKÝSÝ (HATALI):
       

        // YENÝSÝ (SÝZÝN PORT NUMARANIZ 7124 ÝSE):
        private const string ApiUrl = "https://test.ramazanozcan.com/api/TicketApi/CheckStatus?q=";

        public HomeController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(string query)
        {
            if (string.IsNullOrEmpty(query)) return View();

            var client = _httpClientFactory.CreateClient();

            try
            {
                // Ana sunucuya istek at
                var response = await client.GetAsync(ApiUrl + query);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<TicketResultViewModel>(jsonString);
                    return View("Result", result);
                }
                else
                {
                    ViewBag.Error = "Kayýt bulunamadý.";
                }
            }
            catch
            {
                ViewBag.Error = "Sunucuya baðlanýlamadý.";
            }

            return View();
        }
    }

    // Gelen veriyi karþýlayacak model (Bu projede Models klasörüne de koyabilirsiniz)
    public class TicketResultViewModel
    {
        public string FisNo { get; set; }
        public string Cihaz { get; set; }
        public string SeriNo { get; set; }
        public string Durum { get; set; }
        public string Ariza { get; set; }
        public string GirisTarihi { get; set; }
        public string TahminiTeslim { get; set; }
        public string Ucret { get; set; }
    }
}