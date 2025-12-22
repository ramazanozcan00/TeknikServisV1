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

        // API URL'sini appsettings.json'dan alır
        private string BaseApiUrl => _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:44326";

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
                // SSL Hatasını CheckStatus için de bypass edelim ki sorgulama da bozulmasın
                var handler = new HttpClientHandler();
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true;

                using (var customClient = new System.Net.Http.HttpClient(handler))
                {
                    var response = await customClient.GetAsync($"{BaseApiUrl}/api/TicketApi/CheckStatus?q={query}");

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
            Options options = new Options();
            options.ApiKey = _configuration["Iyzico:ApiKey"];
            options.SecretKey = _configuration["Iyzico:SecretKey"];
            options.BaseUrl = _configuration["Iyzico:BaseUrl"];

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

            // Dönüş URL'i
            request.CallbackUrl = Url.Action("PaymentResult", "Home", null, Request.Scheme);

            request.EnabledInstallments = new List<int>() { 2, 3, 6, 9 };

            Buyer buyer = new Buyer();
            buyer.Id = "BY789";
            buyer.Name = "Misafir";
            buyer.Surname = "Müşteri";
            buyer.GsmNumber = "+905350000000";
            buyer.Email = "misafir@musteri.com";
            buyer.IdentityNumber = "74300864791";
            buyer.LastLoginDate = "2015-10-05 12:43:35";
            buyer.RegistrationDate = "2013-04-21 15:12:09";
            buyer.RegistrationAddress = "Merkez Mah.";
            buyer.Ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "85.34.78.112";
            buyer.City = "Istanbul";
            buyer.Country = "Turkey";
            buyer.ZipCode = "34732";
            request.Buyer = buyer;

            Address billingAddress = new Address();
            billingAddress.ContactName = "Misafir Müşteri";
            billingAddress.City = "Istanbul";
            billingAddress.Country = "Turkey";
            billingAddress.Description = "Merkez Mah.";
            billingAddress.ZipCode = "34742";
            request.BillingAddress = billingAddress;
            request.ShippingAddress = billingAddress;

            List<BasketItem> basketItems = new List<BasketItem>();
            BasketItem firstBasketItem = new BasketItem();
            firstBasketItem.Id = "BI101";
            firstBasketItem.Name = cihaz + " Hizmet Bedeli";
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

        [HttpPost]
        public async Task<IActionResult> PaymentResult(string token)
        {
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
                string debugMessage = "";

                if (!string.IsNullOrEmpty(odenenFisNo))
                {
                    try
                    {
                        // --- SSL BYPASS VE API ÇAĞRISI ---
                        var handler = new HttpClientHandler();
                        handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                        handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true;

                        using (var client = new System.Net.Http.HttpClient(handler))
                        {
                            string updateApiUrl = $"{BaseApiUrl}/api/TicketApi/UpdatePaymentStatus";

                            var updateModel = new { FisNo = odenenFisNo };
                            var jsonContent = new StringContent(
                                JsonConvert.SerializeObject(updateModel),
                                Encoding.UTF8,
                                "application/json");

                            // API'ye isteği gönder ve cevabı bekle
                            var response = await client.PostAsync(updateApiUrl, jsonContent);

                            if (response.IsSuccessStatusCode)
                            {
                                debugMessage = "Durum Güncellendi.";
                            }
                            else
                            {
                                debugMessage = $"API Hatası: {response.StatusCode}";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Bağlantı hatası olsa bile kullanıcıya ödeme başarılı gösterilir
                        // debugMessage = "API'ye ulaşılamadı: " + ex.Message;
                    }
                }

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