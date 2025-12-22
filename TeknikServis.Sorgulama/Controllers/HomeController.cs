using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Text;
using System;

namespace TeknikServis.Sorgulama.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        // API URL'lerini kendi ortamınıza (Localhost veya Canlı) göre düzenleyin.
        private const string ApiCheckUrl = "https://test.ramazanozcan.com/api/TicketApi/CheckStatus?q=";
        private const string ApiUpdateUrl = "https://test.ramazanozcan.com/api/TicketApi/UpdatePaymentStatus";

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
                var response = await client.GetAsync(ApiCheckUrl + query);

                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var result = JsonConvert.DeserializeObject<TicketResultViewModel>(jsonString);

                    if (TempData["SuccessMessage"] != null)
                    {
                        ViewBag.Message = TempData["SuccessMessage"];
                    }

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

        // --- IYZICO ÖDEME BAŞLATMA ---
        [HttpPost]
        public async Task<IActionResult> StartPayment(string fisNo, string ucret, string cihaz)
        {
            Options options = new Options();
            options.ApiKey = _configuration["Iyzico:ApiKey"];
            options.SecretKey = _configuration["Iyzico:SecretKey"];
            options.BaseUrl = _configuration["Iyzico:BaseUrl"];

            // Ücreti Iyzico formatına çevir (Örn: 1250.50)
            string cleanPrice = "0";
            try
            {
                string rawPrice = ucret.Replace("TL", "").Replace("tl", "").Replace("₺", "").Trim();
                if (decimal.TryParse(rawPrice, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("tr-TR"), out decimal parsedPrice))
                {
                    cleanPrice = parsedPrice.ToString(new System.Globalization.CultureInfo("en-US"));
                }
                else
                {
                    cleanPrice = rawPrice.Replace(".", "").Replace(",", ".");
                }
            }
            catch
            {
                ViewBag.Error = "Ücret formatı hatalı.";
                return View("Index");
            }

            CreateCheckoutFormInitializeRequest request = new CreateCheckoutFormInitializeRequest();
            request.Locale = Locale.TR.ToString();
            request.ConversationId = fisNo;
            request.Price = cleanPrice;
            request.PaidPrice = cleanPrice;
            request.Currency = Currency.TRY.ToString();
            request.BasketId = "B" + fisNo;
            request.PaymentGroup = PaymentGroup.PRODUCT.ToString();

            // ÖNEMLİ: Callback URL'i dinamik olarak üretiyoruz.
            // Localhost'ta HTTPS portunda çalıştığınızdan emin olun.
            request.CallbackUrl = Url.Action("PaymentResult", "Home", null, Request.Scheme);

            request.EnabledInstallments = new List<int>() { 2, 3, 6, 9 };

            // Alıcı Bilgileri (Zorunlu alanlar - Sabit veri kullanılabilir)
            Buyer buyer = new Buyer();
            buyer.Id = "BY789";
            buyer.Name = "Misafir";
            buyer.Surname = "Müşteri";
            buyer.GsmNumber = "+905350000000";
            buyer.Email = "misafir@musteri.com";
            buyer.IdentityNumber = "74300864791";
            buyer.LastLoginDate = "2015-10-05 12:43:35";
            buyer.RegistrationDate = "2013-04-21 15:12:09";
            buyer.RegistrationAddress = "Teknik Servis";
            buyer.Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "85.34.78.112";
            buyer.City = "Istanbul";
            buyer.Country = "Turkey";
            buyer.ZipCode = "34732";
            request.Buyer = buyer;

            Address billingAddress = new Address();
            billingAddress.ContactName = "Misafir Müşteri";
            billingAddress.City = "Istanbul";
            billingAddress.Country = "Turkey";
            billingAddress.Description = "Teknik Servis";
            billingAddress.ZipCode = "34742";
            request.BillingAddress = billingAddress;
            request.ShippingAddress = billingAddress;

            List<BasketItem> basketItems = new List<BasketItem>();
            BasketItem firstBasketItem = new BasketItem();
            firstBasketItem.Id = "BI101";
            firstBasketItem.Name = cihaz + " Tamir Hizmeti";
            firstBasketItem.Category1 = "Hizmet";
            firstBasketItem.ItemType = BasketItemType.VIRTUAL.ToString();
            firstBasketItem.Price = cleanPrice;
            basketItems.Add(firstBasketItem);
            request.BasketItems = basketItems;

            CheckoutFormInitialize checkoutFormInitialize = await CheckoutFormInitialize.Create(request, options);

            if (checkoutFormInitialize.Status == "success")
            {
                return Redirect(checkoutFormInitialize.PaymentPageUrl);
            }
            else
            {
                ViewBag.Error = "Ödeme sistemi hatası: " + checkoutFormInitialize.ErrorMessage;
                return View("Index");
            }
        }

        // --- IYZICO SONUÇ DÖNÜŞÜ ---
        [HttpPost]
        [IgnoreAntiforgeryToken] // <--- BU ÇOK ÖNEMLİ: Iyzico'dan gelen isteği kabul et
        public async Task<IActionResult> PaymentResult([FromForm] string token)
        {
            // Token'ı parametreden veya form verisinden yakalamaya çalış
            if (string.IsNullOrEmpty(token) && Request.Form.ContainsKey("token"))
            {
                token = Request.Form["token"];
            }

            if (string.IsNullOrEmpty(token))
            {
                ViewBag.Error = "Teknik Hata: Ödeme token'ı alınamadı. (Lütfen https:// kullandığınızdan emin olun)";
                return View("Index");
            }

            Options options = new Options();
            options.ApiKey = _configuration["Iyzico:ApiKey"];
            options.SecretKey = _configuration["Iyzico:SecretKey"];
            options.BaseUrl = _configuration["Iyzico:BaseUrl"];

            RetrieveCheckoutFormRequest request = new RetrieveCheckoutFormRequest();
            request.Token = token;

            CheckoutForm checkoutForm = await CheckoutForm.Retrieve(request, options);

            if (checkoutForm.PaymentStatus == "SUCCESS")
            {
                string odenenFisNo = checkoutForm.ConversationId;

                if (!string.IsNullOrEmpty(odenenFisNo))
                {
                    // API'ye ödeme başarılı bilgisini gönder
                    var client = _httpClientFactory.CreateClient();
                    var updateModel = new { FisNo = odenenFisNo };
                    var jsonContent = new StringContent(JsonConvert.SerializeObject(updateModel), Encoding.UTF8, "application/json");

                    try
                    {
                        await client.PostAsync(ApiUpdateUrl, jsonContent);
                    }
                    catch { /* Log */ }

                    // Kullanıcıyı tekrar sorgulama sonucuna yönlendir
                    TempData["SuccessMessage"] = "Ödemeniz başarıyla alındı! Durum güncellendi.";

                    // Otomatik olarak tekrar sorgulama yaptırıp Result ekranına dönüyoruz
                    // Not: POST action'ı redirect ile çağıramayız, bu yüzden Index GET'e atıp kullanıcıya tekrar sorgulatabiliriz 
                    // VEYA API'den veriyi tekrar çekip Result view'ını döndürebiliriz (En temiz yöntem bu).

                    try
                    {
                        var refreshResponse = await client.GetAsync(ApiCheckUrl + odenenFisNo);
                        if (refreshResponse.IsSuccessStatusCode)
                        {
                            var refreshJson = await refreshResponse.Content.ReadAsStringAsync();
                            var refreshResult = JsonConvert.DeserializeObject<TicketResultViewModel>(refreshJson);
                            ViewBag.Message = "Ödeme Başarılı! Teşekkür ederiz.";
                            return View("Result", refreshResult);
                        }
                    }
                    catch { }
                }
            }

            ViewBag.Error = "Ödeme başarısız: " + checkoutForm.ErrorMessage;
            return View("Index");
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
        public string Ucret { get; set; }
    }
}