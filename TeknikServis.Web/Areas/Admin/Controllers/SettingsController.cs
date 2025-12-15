using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using TeknikServis.Web.Extensions;

namespace TeknikServis.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SettingsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<AppUser> _userManager;

        public SettingsController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }

        // --- DÜZELTME 1: Şube Listeleme Mantığı ---
        private async Task<List<Branch>> GetAccessibleBranches()
        {
            // Eğer kullanıcı "Admin" rolündeyse TÜM şubeleri görsün
            if (User.IsInRole("Admin"))
            {
                var allBranchesInDb = await _unitOfWork.Repository<Branch>().GetAllAsync();
                return allBranchesInDb.OrderBy(x => x.BranchName).ToList();
            }

            // Admin değilse sadece kendi yetkili olduğu şubeleri görsün (Eski mantık)
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString)) return new List<Branch>();

            var user = await _userManager.FindByIdAsync(userIdString);
            if (user == null) return new List<Branch>();

            var accessibleBranches = new List<Branch>();

            // 1. Ana Şube
            var mainBranch = await _unitOfWork.Repository<Branch>().GetByIdAsync(user.BranchId);
            if (mainBranch != null) accessibleBranches.Add(mainBranch);

            // 2. Ek Şubeler
            var extraBranches = await _unitOfWork.Repository<UserBranch>()
                .FindAsync(x => x.UserId == user.Id, inc => inc.Branch);

            foreach (var item in extraBranches)
            {
                if (item.Branch != null && !accessibleBranches.Any(x => x.Id == item.BranchId))
                {
                    accessibleBranches.Add(item.Branch);
                }
            }

            return accessibleBranches;
        }

        // --- EMAIL (GET) ---
        [HttpGet]
        public async Task<IActionResult> Email(Guid? branchId)
        {
            var branches = await GetAccessibleBranches();
            ViewBag.Branches = branches;

            var targetBranchId = branchId ?? User.GetBranchId();
            if (targetBranchId == Guid.Empty && branches.Any()) targetBranchId = branches.First().Id;

            ViewBag.SelectedBranchId = targetBranchId;

            var settingsList = await _unitOfWork.Repository<EmailSetting>().FindAsync(x => x.BranchId == targetBranchId);
            var settings = settingsList.FirstOrDefault();

            if (settings == null)
            {
                settings = new EmailSetting { BranchId = targetBranchId };
            }

            return View(settings);
        }

        // --- EMAIL (POST) ---
        [HttpPost]
        public async Task<IActionResult> Email(EmailSetting model)
        {
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.SelectedBranchId = model.BranchId;

            if (model.BranchId == Guid.Empty)
            {
                ModelState.AddModelError("", "Şube bilgisi alınamadı.");
                return View(model);
            }

            var settingsList = await _unitOfWork.Repository<EmailSetting>().FindAsync(x => x.BranchId == model.BranchId);
            var existingSettings = settingsList.FirstOrDefault();

            if (existingSettings == null)
            {
                // Yeni Kayıt
                model.Id = Guid.NewGuid();
                model.CreatedDate = DateTime.Now;
                await _unitOfWork.Repository<EmailSetting>().AddAsync(model);
            }
            else
            {
                // Güncelleme
                existingSettings.SmtpHost = model.SmtpHost;
                existingSettings.SmtpPort = model.SmtpPort;
                existingSettings.SenderEmail = model.SenderEmail;

                // --- DÜZELTME 2: Şifre boşsa eskisini koru ---
                if (!string.IsNullOrEmpty(model.SenderPassword))
                {
                    existingSettings.SenderPassword = model.SenderPassword;
                }

                existingSettings.EnableSsl = model.EnableSsl;
                existingSettings.UpdatedDate = DateTime.Now;
                _unitOfWork.Repository<EmailSetting>().Update(existingSettings);
            }

            await _unitOfWork.CommitAsync();
            TempData["Success"] = "Mail ayarları güncellendi.";

            return RedirectToAction("Email", new { branchId = model.BranchId });
        }

        // --- SMS (GET) ---
        [HttpGet]
        public async Task<IActionResult> Sms(Guid? branchId)
        {
            var branches = await GetAccessibleBranches();
            ViewBag.Branches = branches;

            var targetBranchId = branchId ?? User.GetBranchId();
            if (targetBranchId == Guid.Empty && branches.Any()) targetBranchId = branches.First().Id;

            ViewBag.SelectedBranchId = targetBranchId;

            var settingsList = await _unitOfWork.Repository<SmsSetting>().FindAsync(x => x.BranchId == targetBranchId);
            var currentSetting = settingsList.FirstOrDefault();

            if (currentSetting == null)
            {
                currentSetting = new SmsSetting { BranchId = targetBranchId };
            }

            return View(currentSetting);
        }

        // --- SMS (POST) ---
        [HttpPost]
        public async Task<IActionResult> Sms(SmsSetting model)
        {
            ViewBag.Branches = await GetAccessibleBranches();
            ViewBag.SelectedBranchId = model.BranchId;

            // Model validasyonundan Password alanını çıkar (zorunlu olmasın, çünkü güncellemede boş olabilir)
            if (string.IsNullOrEmpty(model.ApiPassword))
            {
                ModelState.Remove("ApiPassword");
            }

            if (!ModelState.IsValid) return View(model);

            var settingsList = await _unitOfWork.Repository<SmsSetting>().FindAsync(x => x.BranchId == model.BranchId);
            var existing = settingsList.FirstOrDefault();

            if (existing == null)
            {
                model.Id = Guid.NewGuid();
                model.CreatedDate = DateTime.Now;
                await _unitOfWork.Repository<SmsSetting>().AddAsync(model);
            }
            else
            {
                existing.SmsTitle = model.SmsTitle;
                existing.ApiUsername = model.ApiUsername;

                // --- DÜZELTME 3: SMS Şifresi boşsa eskisini koru ---
                if (!string.IsNullOrEmpty(model.ApiPassword))
                {
                    existing.ApiPassword = model.ApiPassword;
                }

                existing.ApiUrl = model.ApiUrl;
                existing.IsActive = model.IsActive;
                existing.UpdatedDate = DateTime.Now;
                _unitOfWork.Repository<SmsSetting>().Update(existing);
            }

            await _unitOfWork.CommitAsync();
            TempData["Success"] = "SMS ayarları güncellendi.";

            return RedirectToAction("Sms", new { branchId = model.BranchId });
        }

        // ... Diğer metodlar (Receipt, Company) aynen kalacak ...
        [HttpGet]
        public async Task<IActionResult> Receipt()
        {
            var settings = (await _unitOfWork.Repository<ReceiptSetting>().GetAllAsync()).FirstOrDefault();
            if (settings == null) settings = new ReceiptSetting();
            return View(settings);
        }

        [HttpPost]
        public async Task<IActionResult> Receipt(ReceiptSetting model, IFormFile logoFile)
        {
            var existing = (await _unitOfWork.Repository<ReceiptSetting>().GetAllAsync()).FirstOrDefault();
            string logoPath = existing?.LogoPath;

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

            if (existing == null)
            {
                model.Id = Guid.NewGuid();
                model.LogoPath = logoPath;
                await _unitOfWork.Repository<ReceiptSetting>().AddAsync(model);
            }
            else
            {
                if (logoFile != null) existing.LogoPath = logoPath;
                existing.HeaderText = model.HeaderText;
                existing.ServiceTerms = model.ServiceTerms;
                existing.UpdatedDate = DateTime.Now;
                _unitOfWork.Repository<ReceiptSetting>().Update(existing);
            }

            await _unitOfWork.CommitAsync();
            TempData["Success"] = "Fiş ayarları güncellendi.";
            return RedirectToAction("Receipt");
        }

        [HttpGet]
        public async Task<IActionResult> Company()
        {
            var settings = (await _unitOfWork.Repository<CompanySetting>().GetAllAsync()).FirstOrDefault();
            if (settings == null) settings = new CompanySetting();
            return View(settings);
        }
    }
}