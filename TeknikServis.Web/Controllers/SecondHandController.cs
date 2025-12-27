using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions; // Eğer extension kullanıyorsanız

namespace TeknikServis.Web.Controllers
{
    public class SecondHandController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public SecondHandController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // Robot Arayüzü (Müşteri Burayı Görecek)
        [HttpGet]
        public IActionResult AiRobot()
        {
            return View();
        }

        // Yönetici Paneli (Gelen Teklifleri Görür)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var offers = await _unitOfWork.Repository<SecondHandOffer>().GetAllAsync();
            return View(offers);
        }

        // Robotun Fiyat Hesaplama ve Kaydetme Metodu (GÜNCELLENMİŞ VERSİYON)
        [HttpPost]
        public async Task<IActionResult> SubmitOffer([FromBody] SecondHandOfferDto offerDto)
        {
            try
            {
                // --- GELİŞMİŞ FİYAT MOTORU (GÜNCEL PİYASA VERİLERİ) ---

                // 1. Model Bazlı Taban Fiyat Listesi (TL)
                // Buradaki fiyatlar "Mükemmel" durumdaki bir cihazın yaklaşık alım fiyatıdır.
                var priceList = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                {
                    // Apple Serisi
                    { "iphone 11", 12000 },
                    { "iphone 12", 17000 }, { "iphone 12 pro", 21000 }, { "iphone 12 pro max", 24000 },
                    { "iphone 13", 26000 }, { "iphone 13 pro", 33000 }, { "iphone 13 pro max", 38000 },
                    { "iphone 14", 35000 }, { "iphone 14 pro", 48000 }, { "iphone 14 pro max", 55000 },
                    { "iphone 15", 45000 }, { "iphone 15 pro", 60000 }, { "iphone 15 pro max", 70000 },
                    
                    // Samsung Serisi
                    { "s20", 8000 }, { "s20 fe", 7000 },
                    { "s21", 11000 }, { "s21 fe", 10000 }, { "s21 ultra", 16000 },
                    { "s22", 15000 }, { "s22 ultra", 22000 },
                    { "s23", 24000 }, { "s23 ultra", 38000 },
                    { "s24", 32000 }, { "s24 ultra", 52000 },
                    
                    // Xiaomi / Diğer (Ortalama bir değer atayalım)
                    { "redmi note 10", 4000 }, { "redmi note 11", 5000 }, { "redmi note 12", 7000 }
                };

                // Girilen modeli normalize et (küçük harf ve boşluk temizleme)
                string inputModel = (offerDto.Brand + " " + offerDto.Model).ToLower();
                decimal basePrice = 0;

                // Model listede var mı diye içerik araması yap
                foreach (var item in priceList)
                {
                    if (inputModel.Contains(item.Key))
                    {
                        basePrice = item.Value;
                        break; // İlk eşleşen en spesifik modeli al
                    }
                }

                // Eğer model listede yoksa varsayılan bir mantık işlet
                if (basePrice == 0)
                {
                    if (inputModel.Contains("iphone")) basePrice = 10000;
                    else if (inputModel.Contains("samsung")) basePrice = 8000;
                    else basePrice = 4000; // Bilinmeyen Android cihaz taban fiyatı
                }

                // 2. Kozmetik Durum Çarpanı (Daha agresif kesintiler)
                decimal conditionMultiplier = 1.0m;
                if (offerDto.Condition.Contains("Mükemmel")) conditionMultiplier = 1.0m;      // %100
                else if (offerDto.Condition.Contains("İyi")) conditionMultiplier = 0.85m;     // %15 değer kaybı
                else if (offerDto.Condition.Contains("Kötü")) conditionMultiplier = 0.60m;    // %40 değer kaybı (Kırık/Çatlak)

                decimal currentPrice = basePrice * conditionMultiplier;

                // 3. Çalışma Durumu (Çalışmıyorsa Hurda Fiyatı)
                if (!offerDto.IsWorking)
                {
                    // Model değerliyse hurda fiyatı da yüksektir (Ekran/Anakart için)
                    currentPrice = basePrice * 0.20m; // %80 değer kaybı
                }

                // 4. Ekstra Özellikler (Sabit fiyat yerine oransal artış)
                if (offerDto.HasBox && offerDto.IsWorking) currentPrice += 500; // Kutu/Şarj etkisi
                if (offerDto.HasWarranty && offerDto.IsWorking) currentPrice += (basePrice * 0.10m); // Garanti varsa %10 daha değerli

                // 5. Son Fiyat Yuvarlama (Sonu 00 ile bitsin)
                decimal finalPrice = Math.Round(currentPrice / 100) * 100;

                // Veritabanına Kayıt
                var offer = new SecondHandOffer
                {
                    Id = Guid.NewGuid(),
                    CustomerName = offerDto.Name,
                    Phone = offerDto.Phone,
                    DeviceBrand = offerDto.Brand,
                    DeviceModel = offerDto.Model,
                    Condition = offerDto.Condition,
                    IsWorking = offerDto.IsWorking,
                    HasBox = offerDto.HasBox,
                    HasWarranty = offerDto.HasWarranty,
                    EstimatedPrice = finalPrice,
                    Status = "Bekliyor",
                    CreatedDate = DateTime.Now
                };

                await _unitOfWork.Repository<SecondHandOffer>().AddAsync(offer);
                await _unitOfWork.CommitAsync();

                return Json(new { success = true, price = finalPrice, message = "Teklif başarıyla oluşturuldu." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }

        // DTO Sınıfı (Veri transferi için)
        public class SecondHandOfferDto
        {
            public string Name { get; set; }
            public string Phone { get; set; }
            public string Brand { get; set; }
            public string Model { get; set; }
            public string Condition { get; set; }
            public bool IsWorking { get; set; }
            public bool HasBox { get; set; }
            public bool HasWarranty { get; set; }
        }
    }
}