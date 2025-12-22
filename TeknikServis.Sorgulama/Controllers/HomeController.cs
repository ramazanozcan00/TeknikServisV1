using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration; // 1. Bu kütüphaneyi ekleyin

namespace TeknikServis.Sorgulama.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration; // 2. Configuration tanımı

        // Not: İsterseniz bu API URL'sini de appsettings.json'a taşıyabilirsiniz.
        private const string ApiUrl = "https://test.ramazanozcan.com/api/TicketApi/CheckStatus?q=";

        // 3. Constructor'a IConfiguration parametresi eklendi
        public HomeController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
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
                var response = await client.GetAsync(ApiUrl + query);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<TicketResultViewModel>(jsonString);
                    return View("Result", result);
                }
                else
                {
                    ViewBag.Error = "Kayıt bulunamadı.";
                }
            }
            catch
            {
                ViewBag.Error = "Sunucuya bağlanılamadı.";
            }

            return View();
        }

        // --- IYZICO ÖDEME ENTEGRASYONU ---

    
        [HttpPost]
        public async Task<IActionResult> StartPayment(string fisNo, string ucret, string cihaz)
        {
            // 1. Ayarları Çek
            Options options = new Options();
            options.ApiKey = _configuration["Iyzico:ApiKey"];
            options.SecretKey = _configuration["Iyzico:SecretKey"];
            options.BaseUrl = _configuration["Iyzico:BaseUrl"];

            // --- FİYAT FORMATINI DÜZELTME (GÜNCELLENDİ) ---
            string cleanPrice = "0";
            try
            {
                // "TL", "₺" ve boşlukları temizle
                string rawPrice = ucret.Replace("TL", "").Replace("tl", "").Replace("₺", "").Trim();

                // Sayıyı Türkiye kültürüne (1.250,50) göre decimal'e çevir
                if (decimal.TryParse(rawPrice, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("tr-TR"), out decimal parsedPrice))
                {
                    // Iyzico için "en-US" formatına (1250.50) çevir. (Binlik ayracı yok, ondalık nokta)
                    cleanPrice = parsedPrice.ToString(new System.Globalization.CultureInfo("en-US"));
                }
                else
                {
                    // Parse edilemezse fallback: Noktaları sil (binlik), virgülü nokta yap (ondalık)
                    // Örn: "1.250,00" -> "1250.00"
                    cleanPrice = rawPrice.Replace(".", "").Replace(",", ".");
                }
            }
            catch
            {
                ViewBag.Error = "Ücret formatı hatalı.";
                return View("Index");
            }

            // 2. İstek Oluşturma
            CreateCheckoutFormInitializeRequest request = new CreateCheckoutFormInitializeRequest();
            request.Locale = Locale.TR.ToString();
            request.ConversationId = fisNo;
            request.Price = cleanPrice;
            request.PaidPrice = cleanPrice;
            request.Currency = Currency.TRY.ToString();
            request.BasketId = "B" + fisNo;
            request.PaymentGroup = PaymentGroup.PRODUCT.ToString();
            request.CallbackUrl = Url.Action("PaymentResult", "Home", null, Request.Scheme);

            request.EnabledInstallments = new List<int>() { 2, 3, 6, 9 };

            // 3. Alıcı Bilgileri (Dummy Data - Zorunlu Alanlar)
            Buyer buyer = new Buyer();
            buyer.Id = "BY789";
            buyer.Name = "Misafir";
            buyer.Surname = "Müşteri";
            buyer.GsmNumber = "+905350000000";
            buyer.Email = "misafir@musteri.com";
            buyer.IdentityNumber = "74300864791";
            buyer.LastLoginDate = "2015-10-05 12:43:35";
            buyer.RegistrationDate = "2013-04-21 15:12:09";
            buyer.RegistrationAddress = "Nidakule Göztepe, Merdivenköy Mah. Bora Sok. No:1";
            buyer.Ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "85.34.78.112";
            buyer.City = "Istanbul";
            buyer.Country = "Turkey";
            buyer.ZipCode = "34732";
            request.Buyer = buyer;

            Address billingAddress = new Address();
            billingAddress.ContactName = "Misafir Müşteri";
            billingAddress.City = "Istanbul";
            billingAddress.Country = "Turkey";
            billingAddress.Description = "Nidakule Göztepe, Merdivenköy Mah. Bora Sok. No:1";
            billingAddress.ZipCode = "34742";
            request.BillingAddress = billingAddress;
            request.ShippingAddress = billingAddress;

            // 4. Sepet (Fiyat Eşleşmeli)
            List<BasketItem> basketItems = new List<BasketItem>();
            BasketItem firstBasketItem = new BasketItem();
            firstBasketItem.Id = "BI101";
            firstBasketItem.Name = cihaz + " Tamir Hizmeti";
            firstBasketItem.Category1 = "Hizmet";
            firstBasketItem.ItemType = BasketItemType.VIRTUAL.ToString();
            firstBasketItem.Price = cleanPrice; // Buradaki fiyat yukarıdaki request.Price ile aynı olmalı
            basketItems.Add(firstBasketItem);
            request.BasketItems = basketItems;

            // 5. Başlat
            CheckoutFormInitialize checkoutFormInitialize = await CheckoutFormInitialize.Create(request, options);

            if (checkoutFormInitialize.Status == "success")
            {
                return Redirect(checkoutFormInitialize.PaymentPageUrl);
            }
            else
            {
                // Hata detayını ekrana yazdıralım
                ViewBag.Error = "Ödeme sistemi hatası: " + checkoutFormInitialize.ErrorMessage + " (Hata Kodu: " + checkoutFormInitialize.ErrorCode + ")";
                return View("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> PaymentResult(string token)
        {
            // 1. Ayarları Al
            Options options = new Options();
            options.ApiKey = _configuration["Iyzico:ApiKey"];
            options.SecretKey = _configuration["Iyzico:SecretKey"];
            options.BaseUrl = _configuration["Iyzico:BaseUrl"];

            // 2. Iyzico'ya sor: Bu işlem gerçekten başarılı mı?
            RetrieveCheckoutFormRequest request = new RetrieveCheckoutFormRequest();
            request.Token = token;

            CheckoutForm checkoutForm = await CheckoutForm.Retrieve(request, options);

            if (checkoutForm.PaymentStatus == "SUCCESS")
            {
                // --- ÖNEMLİ: API'YE GÜNCELLEME İSTEĞİ GÖNDER ---

                // Sepet ID'si "B" + FisNo şeklindeydi, "B" harfini atıp Fiş No'yu alalım.
                // Veya checkoutForm.BasketId yerine, StartPayment'te conversationId'ye FisNo vermiştik.
                string odenenFisNo = checkoutForm.ConversationId;

                if (!string.IsNullOrEmpty(odenenFisNo))
                {
                    var client = _httpClientFactory.CreateClient();

                    // API adresiniz (Test ortamı ise localhost veya test domaini)
                    // Canlıya aldığınızda burayı gerçek domain yapmalısınız.
                    string updateApiUrl = "https://test.ramazanozcan.com/api/TicketApi/UpdatePaymentStatus";

                    var updateModel = new { FisNo = odenenFisNo };
                    var jsonContent = new StringContent(JsonConvert.SerializeObject(updateModel), System.Text.Encoding.UTF8, "application/json");

                    try
                    {
                        // API'ye POST isteği atıyoruz
                        var updateResponse = await client.PostAsync(updateApiUrl, jsonContent);

                        if (!updateResponse.IsSuccessStatusCode)
                        {
                            // API güncelleyemedi ise loglanabilir ama kullanıcıya ödeme başarılı denmeli.
                        }
                    }
                    catch
                    {
                        // API'ye ulaşılamadı hatası (Loglanmalı)
                    }
                }
                // --------------------------------------------------

                ViewBag.Message = "Ödeme Başarıyla Alındı! Cihaz durumu 'Ödeme Yapıldı' olarak güncellendi.";
                return View("Success");
            }
            else
            {
                ViewBag.Error = "Ödeme Alınamadı. Hata: " + checkoutForm.ErrorMessage;
                return View("Index");
            }
        }
    }

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