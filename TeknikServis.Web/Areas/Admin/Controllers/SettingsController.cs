using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using Microsoft.AspNetCore.Hosting; // Dosya yükleme için
using Microsoft.AspNetCore.Http;

namespace TeknikServis.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment; // Resim kaydı için gerekli

        public SettingsController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public async Task<IActionResult> Email()
        {
            // Veritabanındaki ilk ayarı getir, yoksa boş gönder
            var settings = (await _unitOfWork.Repository<EmailSetting>().GetAllAsync()).FirstOrDefault();

            if (settings == null)
            {
                settings = new EmailSetting(); // Boş model
            }

            return View(settings);
        }

        [HttpPost]
        public async Task<IActionResult> Email(EmailSetting model)
        {
            // Mevcut ayarı bul
            var existingSettings = (await _unitOfWork.Repository<EmailSetting>().GetAllAsync()).FirstOrDefault();

            if (existingSettings == null)
            {
                // Yoksa Yeni Ekle
                model.Id = Guid.NewGuid();
                model.CreatedDate = DateTime.Now;
                await _unitOfWork.Repository<EmailSetting>().AddAsync(model);
            }
            else
            {
                // Varsa Güncelle
                existingSettings.SmtpHost = model.SmtpHost;
                existingSettings.SmtpPort = model.SmtpPort;
                existingSettings.SenderEmail = model.SenderEmail;
                existingSettings.SenderPassword = model.SenderPassword;
                existingSettings.EnableSsl = model.EnableSsl;
                existingSettings.UpdatedDate = DateTime.Now;

                _unitOfWork.Repository<EmailSetting>().Update(existingSettings);
            }

            await _unitOfWork.CommitAsync();
            TempData["Success"] = "Mail ayarları başarıyla güncellendi.";
            return RedirectToAction("Email");
        }
        public async Task<IActionResult> Receipt()
        {
            var settings = (await _unitOfWork.Repository<ReceiptSetting>().GetAllAsync()).FirstOrDefault();
            if (settings == null) settings = new ReceiptSetting();
            return View(settings);
        }

        // --- FİŞ AYARLARI (POST) ---
        [HttpPost]
        public async Task<IActionResult> Receipt(ReceiptSetting model, IFormFile logoFile)
        {
            var existing = (await _unitOfWork.Repository<ReceiptSetting>().GetAllAsync()).FirstOrDefault();

            // 1. Logo Yükleme İşlemi
            string logoPath = existing?.LogoPath; // Varsayılan eski logo

            if (logoFile != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(logoFile.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await logoFile.CopyToAsync(fileStream);
                }
                logoPath = "/uploads/" + uniqueFileName;
            }

            // 2. Veritabanı Kayıt/Güncelleme
            if (existing == null)
            {
                model.Id = Guid.NewGuid();
                model.LogoPath = logoPath;
                await _unitOfWork.Repository<ReceiptSetting>().AddAsync(model);
            }
            else
            {
                if (logoFile != null) existing.LogoPath = logoPath; // Sadece yeni dosya varsa güncelle
                existing.HeaderText = model.HeaderText;
                existing.ServiceTerms = model.ServiceTerms;
                existing.UpdatedDate = DateTime.Now;
                _unitOfWork.Repository<ReceiptSetting>().Update(existing);
            }

            await _unitOfWork.CommitAsync();
            TempData["Success"] = "Fiş ayarları güncellendi.";
            return RedirectToAction("Receipt");
        }


        // Mevcut Controller içine ekleyin:

        // SMS AYARLARI (GET)
        [HttpGet]
        public async Task<IActionResult> Sms()
        {
            var settings = await _unitOfWork.Repository<SmsSetting>().GetAllAsync();
            var currentSetting = settings.FirstOrDefault();

            if (currentSetting == null)
            {
                currentSetting = new SmsSetting(); // Boş model gönder
            }

            return View(currentSetting);
        }

        // SMS AYARLARI (POST)
        [HttpPost]
        public async Task<IActionResult> Sms(SmsSetting model)
        {
            if (!ModelState.IsValid) return View(model);

            var settings = await _unitOfWork.Repository<SmsSetting>().GetAllAsync();
            var existing = settings.FirstOrDefault();

            if (existing == null)
            {
                // Yeni Kayıt
                model.Id = Guid.NewGuid();
                model.CreatedDate = DateTime.Now;
                await _unitOfWork.Repository<SmsSetting>().AddAsync(model);
            }
            else
            {
                // Güncelleme
                existing.SmsTitle = model.SmsTitle;
                existing.ApiUsername = model.ApiUsername;
                existing.ApiPassword = model.ApiPassword;
                existing.ApiUrl = model.ApiUrl;
                existing.IsActive = model.IsActive;
                existing.UpdatedDate = DateTime.Now;

                _unitOfWork.Repository<SmsSetting>().Update(existing);
            }

            await _unitOfWork.CommitAsync();
            TempData["Success"] = "SMS ayarları başarıyla güncellendi.";

            return RedirectToAction("Sms");
        }

        // ... Mevcut kodlar ...

        // FİRMA BİLGİLERİ (GET)
        [HttpGet]
        public async Task<IActionResult> Company()
        {
            // İlk kaydı getir, yoksa boş bir tane oluştur
            var settings = (await _unitOfWork.Repository<CompanySetting>().GetAllAsync()).FirstOrDefault();
            if (settings == null)
            {
                settings = new CompanySetting();
            }
            return View(settings);
        }

        
        // ... Mevcut kodlar ...
    }
}