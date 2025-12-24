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
        // SERVİS DURUM SORGULAMA
        [HttpPost]
        public async Task<IActionResult> Index(string query)
        {
            if (string.IsNullOrEmpty(query)) return View();

            // ESKİ KOD: var client = _httpClientFactory.CreateClient();
            // YENİ KOD: PaymentResult metodundaki gibi SSL hatasını görmezden gelen handler kullanıyoruz.

            var handler = new HttpClientHandler();
            // Bu satır test ortamındaki SSL hatalarını (geçersiz sertifika) yoksayar.
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            using (var client = new System.Net.Http.HttpClient(handler))
            {
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
            }

            return View();
        }

        // IYZICO ÖDEME FORMU BAŞLATMA
        [HttpPost]
        public async Task<IActionResult> StartPayment(string fisNo, string ucret, string cihaz)
        {
            // Debug: Gelen ücret boşsa hemen hata ver
            if (string.IsNullOrEmpty(ucret))
            {
                ViewBag.Error = "Hata: Ücret bilgisi sayfadan gönderilmedi (Boş).";
                return View("Index");
            }

            Options options = new Options();
            options.ApiKey = _configuration["Iyzico:ApiKey"];
            options.SecretKey = _configuration["Iyzico:SecretKey"];
            options.BaseUrl = _configuration["Iyzico:BaseUrl"];

            // 1. Fiyatı Formatla (Iyzico için "1250.00" formatına çevirir)
            string cleanPrice = FormatPrice(ucret);

            // Eğer formatlama sonucu 0 çıkarsa hata ver ve gelen veriyi ekrana yaz
            if (cleanPrice == "0" || cleanPrice == "0.00")
            {
                ViewBag.Error = $"Geçersiz tutar formatı. Gelen: '{ucret}'";
                return View("Index");
            }

            CreateCheckoutFormInitializeRequest request = new CreateCheckoutFormInitializeRequest();
            request.Locale = Locale.TR.ToString();
            request.ConversationId = fisNo;
            request.Price = cleanPrice;      // Formatlanmış fiyat
            request.PaidPrice = cleanPrice;  // Formatlanmış fiyat
            request.Currency = Currency.TRY.ToString();
            request.BasketId = "B-" + fisNo;
            request.PaymentGroup = PaymentGroup.PRODUCT.ToString();

            // Callback URL: HTTPS zorunlu
            request.CallbackUrl = Url.Action("PaymentResult", "Home", null, "https");

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
            item.Price = cleanPrice; // Formatlanmış fiyat
            basketItems.Add(item);
            request.BasketItems = basketItems;

            CheckoutFormInitialize form = await CheckoutFormInitialize.Create(request, options);

            if (form.Status == "success")
            {
                return Redirect(form.PaymentPageUrl);
            }
            else
            {
                // Hata mesajına Iyzico'nun detayını ve gönderdiğimiz fiyatı ekliyoruz
                ViewBag.Error = $"Ödeme başlatılamadı: {form.ErrorMessage} (Gönderilen Tutar: {cleanPrice})";
                return View("Index");
            }
        }

        // YENİ FORMATLAMA METODU (Daha Akıllı)
        private string FormatPrice(string priceStr)
        {
            if (string.IsNullOrEmpty(priceStr)) return "0.00";
            try
            {
                // 1. Temizlik: Sadece rakam, nokta ve virgül kalsın
                string clean = System.Text.RegularExpressions.Regex.Replace(priceStr, @"[^0-9.,]", "");

                decimal result = 0;

                // 2. Format Tahmini ve Parse İşlemi
                // Eğer virgül sondaysa (Örn: 1.250,00) -> Türkçe format kabul et
                if (clean.LastIndexOf(',') > clean.LastIndexOf('.'))
                {
                    decimal.TryParse(clean, NumberStyles.Any, new CultureInfo("tr-TR"), out result);
                }
                // Eğer nokta sondaysa (Örn: 1,250.00) -> İngilizce/Global format kabul et
                else if (clean.LastIndexOf('.') > clean.LastIndexOf(','))
                {
                    decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
                }
                // Sadece virgül varsa (Örn: 150,00) -> Türkçe
                else if (clean.Contains(",") && !clean.Contains("."))
                {
                    decimal.TryParse(clean, NumberStyles.Any, new CultureInfo("tr-TR"), out result);
                }
                // Hiçbiri yoksa veya sadece nokta varsa -> Düz sayı (Invariant)
                else
                {
                    decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
                }

                // 3. Iyzico için KESİN Format: "1250.00" (Nokta ondalık, binlik ayracı yok)
                if (result > 0)
                {
                    return result.ToString("0.00", CultureInfo.InvariantCulture);
                }

                return "0.00";
            }
            catch
            {
                return "0.00";
            }
        }



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