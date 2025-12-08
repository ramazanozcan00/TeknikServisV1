using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions; // User.GetBranchId() için
using TeknikServis.Web.Helpers;
using System;
using System.Threading.Tasks;

namespace TeknikServis.Web.Controllers
{
    [Authorize]
    public class LicenseController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public LicenseController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public IActionResult Expired()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateLicense(string key)
        {
            Guid branchId = User.GetBranchId();
            var branch = await _unitOfWork.Repository<Branch>().GetByIdAsync(branchId);

            if (branch == null) return NotFound();

            // Anahtarı Doğrula
            if (LicenseHelper.ValidateKey(key, branchId, out DateTime newDate))
            {
                branch.LicenseKey = key;
                branch.LicenseEndDate = newDate;

                _unitOfWork.Repository<Branch>().Update(branch);
                await _unitOfWork.CommitAsync();

                TempData["Success"] = "Lisans başarıyla güncellendi.";
                return RedirectToAction("Index", "Home");
            }

            TempData["Error"] = "Geçersiz Lisans Anahtarı!";
            return RedirectToAction("Expired");
        }
    }
}