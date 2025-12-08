using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Helpers;
using System;
using System.Threading.Tasks;

namespace TeknikServis.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class LicenseController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public LicenseController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var branches = await _unitOfWork.Repository<Branch>().GetAllAsync();
            return View(branches);
        }

        [HttpPost]
        public async Task<IActionResult> Generate(Guid branchId, DateTime expiryDate)
        {
            var branch = await _unitOfWork.Repository<Branch>().GetByIdAsync(branchId);
            if (branch == null) return NotFound();

            // 1. Yeni anahtarı, seçilen tarih ile şifreleyerek üret
            string newKey = LicenseHelper.GenerateKey(branch.Id, expiryDate);

            // 2. Anahtarı veritabanına kaydet (Admin görebilsin diye)
            branch.LicenseKey = newKey;

            // 3. ÖNEMLİ DEĞİŞİKLİK: Tarihi burada GÜNCELLEMİYORUZ.
            // Böylece sistem "Süresi Dolmuş" durumunda kalmaya devam eder.
            // Kullanıcı anahtarı kendi ekranından girdiğinde Helper sınıfı içindeki tarihi çözüp güncelleyecektir.

            // branch.LicenseEndDate = expiryDate; // <--- BU SATIR İPTAL EDİLDİ

            _unitOfWork.Repository<Branch>().Update(branch);
            await _unitOfWork.CommitAsync();

            TempData["Success"] = "Lisans anahtarı üretildi (Aktifleşmedi). Kullanıcı bu anahtarı sisteme girdiğinde lisans süresi uzayacaktır.";
            return RedirectToAction("Index");
        }

        // Lisans İptal Etme
        [HttpPost]
        public async Task<IActionResult> Cancel(Guid branchId)
        {
            var branch = await _unitOfWork.Repository<Branch>().GetByIdAsync(branchId);
            if (branch == null) return NotFound();

            branch.LicenseKey = null;
            branch.LicenseEndDate = DateTime.Now.AddDays(-1);

            _unitOfWork.Repository<Branch>().Update(branch);
            await _unitOfWork.CommitAsync();

            TempData["Success"] = "Lisans iptal edildi.";
            return RedirectToAction("Index");
        }
    }
}