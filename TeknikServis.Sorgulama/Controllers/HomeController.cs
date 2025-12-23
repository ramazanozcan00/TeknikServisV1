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
using System.Globalization;

namespace TeknikServis.Sorgulama.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public HomeController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            // Redirect ile gelen mesajları ViewBag'e taşı
            if (TempData["SuccessMessage"] != null)
            {
                ViewBag.Message = TempData["SuccessMessage"];
            }

            if (TempData["Error"] != null)
            {
                ViewBag.Error = TempData["Error"];
            }

            return View();
        }

        // SERVİS DURUM SORGULAMA
        [HttpPost]
        public async Task<IActionResult> Index(string query)
        {
            if (string.IsNullOrEmpty(query)) return View();

            var client = _httpClientFactory.CreateClient();
            string baseUrl = _configuration["ApiSettings:BaseUrl"];
            string checkEndpoint = _configuration["ApiSettings:CheckEndpoint"];

            if (string.IsNullOrEmpty(baseUrl))
            {
                ViewBag.Error = "API BaseUrl ayarı appsettings.json dosyasında bulunamadı.";
                return View();
            }

            try
            {
                var response = await client.GetAsync(baseUrl + checkEndpoint + query);

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
            catch (Exception ex)
            {
                ViewBag.Error = "Sunucuya bağlanılamadı: " + ex.Message;
            }

            return View();
        }

        // IYZICO ÖDEME FORMU BAŞLATMA
        [HttpPost]
        public async Task<IActionResult> StartPayment(string fisNo, string ucret, string cihaz)
        {
            Options options = new Options();
            options.ApiKey = _configuration["Iyzico:ApiKey"];
            options.SecretKey = _configuration["Iyzico:SecretKey"];
            options.BaseUrl = _configuration["Iyzico:BaseUrl"];

            string cleanPrice = FormatPrice(ucret);
            if (cleanPrice == "0")
            {
                ViewBag.Error = "Geçersiz tutar formatı.";
                return View("Index");
            }

            CreateCheckoutFormInitializeRequest request = new CreateCheckoutFormInitializeRequest();
            request.Locale = Locale.TR.ToString();
            request.ConversationId = fisNo;
            request.Price = cleanPrice;
            request.PaidPrice = cleanPrice;
            request.Currency = Currency.TRY.ToString();
            request.BasketId = "B-" + fisNo;
            request.PaymentGroup = PaymentGroup.PRODUCT.ToString();

            // Callback URL: HTTPS olduğundan emin olun
            request.CallbackUrl = Url.Action("PaymentResult", "Home", null, Request.Scheme);

            request.EnabledInstallments = new List<int>() { 2, 3, 6, 9 };

            Buyer buyer = new Buyer();
            buyer.Id = "GUEST-" + DateTime.Now.Ticks;
            buyer.Name = "Misafir";
            buyer.Surname = "Müşteri";
            buyer.GsmNumber = "+905000000000";
            buyer.Email = "misafir@teknikservis.com";
            buyer.IdentityNumber = "11111111111";
            buyer.RegistrationAddress = "Sorgulama Ekranı";
            buyer.Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "85.85.85.85";
            buyer.City = "Istanbul";
            buyer.Country = "Turkey";
            request.Buyer = buyer;

            Address billingAddress = new Address();
            billingAddress.ContactName = "Misafir Müşteri";
            billingAddress.City = "Istanbul";
            billingAddress.Country = "Turkey";
            billingAddress.Description = "Online Ödeme";
            request.BillingAddress = billingAddress;
            request.ShippingAddress = billingAddress;

            List<BasketItem> basketItems = new List<BasketItem>();
            BasketItem item = new BasketItem();
            item.Id = "SRV-" + fisNo;
            item.Name = cihaz + " Servis Bedeli";
            item.Category1 = "Hizmet";
            item.ItemType = BasketItemType.VIRTUAL.ToString();
            item.Price = cleanPrice;
            basketItems.Add(item);
            request.BasketItems = basketItems;

            CheckoutFormInitialize form = await CheckoutFormInitialize.Create(request, options);

            if (form.Status == "success")
            {
                return Redirect(form.PaymentPageUrl);
            }
            else
            {
                ViewBag.Error = "Ödeme başlatılamadı: " + form.ErrorMessage;
                return View("Index");
            }
        }

        // ... (Üst kısımlar aynı)

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> PaymentResult()
        {
            // 1. Token'ı güvenli şekilde al
            string token = null;
            try { if (Request.Form.ContainsKey("token")) token = Request.Form["token"]; } catch { }

            if (string.IsNullOrEmpty(token))
            {
                ViewBag.Error = "Teknik Hata: Ödeme token bilgisi alınamadı.";
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
                // 2. Fiş Numarasını Kurtar (ConversationId boşsa BasketId'den al)
                string odenenFisNo = checkoutForm.ConversationId;

                if (string.IsNullOrEmpty(odenenFisNo) && !string.IsNullOrEmpty(checkoutForm.BasketId))
                {
                    // StartPayment metodunda BasketId = "B-" + fisNo yapmıştık
                    odenenFisNo = checkoutForm.BasketId.Replace("B-", "");
                }

                if (string.IsNullOrEmpty(odenenFisNo))
                {
                    ViewBag.Error = "Kritik Hata: Iyzico'dan Fiş Numarası dönmedi. Lütfen yöneticiyle iletişime geçin.";
                    return View("Index");
                }

                // 3. API'ye Form Verisi Olarak Gönder (En Güvenli Yöntem)
                var handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                using (var client = new System.Net.Http.HttpClient(handler))
                {
                    string baseUrl = _configuration["ApiSettings:BaseUrl"];
                    string updateEndpoint = _configuration["ApiSettings:UpdateEndpoint"];

                    // Form içeriği hazırlama
                    var formData = new List<KeyValuePair<string, string>>();
                    formData.Add(new KeyValuePair<string, string>("fisNo", odenenFisNo));
                    var content = new FormUrlEncodedContent(formData);

                    try
                    {
                        // POST İsteği
                        var apiResponse = await client.PostAsync(baseUrl + updateEndpoint, content);

                        if (apiResponse.IsSuccessStatusCode)
                        {
                            TempData["SuccessMessage"] = "Ödeme Başarıyla Alındı. Cihaz durumu güncellendi.";

                            // Güncel veriyi çekip göster
                            string checkEndpoint = _configuration["ApiSettings:CheckEndpoint"];
                            var refreshResponse = await client.GetAsync(baseUrl + checkEndpoint + odenenFisNo);

                            if (refreshResponse.IsSuccessStatusCode)
                            {
                                var refreshJson = await refreshResponse.Content.ReadAsStringAsync();
                                var refreshResult = JsonConvert.DeserializeObject<TicketResultViewModel>(refreshJson);
                                ViewBag.Message = "Ödeme Başarılı! İşleminiz onaylandı.";
                                return View("Result", refreshResult);
                            }
                        }
                        else
                        {
                            string apiError = await apiResponse.Content.ReadAsStringAsync();
                            ViewBag.Error = $"Durum güncellenemedi! API Hatası: {apiResponse.StatusCode} - {apiError}";
                            return View("Index");
                        }
                    }
                    catch (Exception ex)
                    {
                        ViewBag.Error = $"Sunucuya bağlanılamadı: {ex.Message}";
                        return View("Index");
                    }
                }
            }

            ViewBag.Error = "Ödeme işlemi tamamlanamadı: " + checkoutForm.ErrorMessage;
            return View("Index");
        }

        // ... (Alt kısımlar aynı)
        // ... (Alt kısımlar aynı)
        private string FormatPrice(string priceStr)
        {
            if (string.IsNullOrEmpty(priceStr)) return "0";
            try
            {
                string clean = priceStr.Replace("TL", "").Replace("tl", "").Replace("₺", "").Trim();
                if (decimal.TryParse(clean, NumberStyles.Any, new CultureInfo("tr-TR"), out decimal result))
                {
                    return result.ToString(new CultureInfo("en-US"));
                }
                return clean.Replace(",", ".");
            }
            catch { return "0"; }
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