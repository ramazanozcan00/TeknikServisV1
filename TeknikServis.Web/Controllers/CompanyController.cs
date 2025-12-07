using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions;

namespace TeknikServis.Web.Controllers
{
    [Authorize]
    public class CompanyController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public CompanyController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // LİSTELEME: Şubeye ait tüm firmaları getirir
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Eğer firmalar tüm şubeler için ortaksa BranchId kontrolünü kaldırabilirsiniz.
            // Burada her şubenin kendi firma listesi olduğunu varsayıyoruz.
            Guid currentBranchId = User.GetBranchId();

            var companies = await _unitOfWork.Repository<CompanySetting>()
                .FindAsync(x => x.BranchId == currentBranchId);

            return View(companies.OrderBy(x => x.CompanyName).ToList());
        }

        // EKLEME SAYFASI
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // EKLEME İŞLEMİ
        [HttpPost]
        public async Task<IActionResult> Create(CompanySetting company)
        {
            if (ModelState.IsValid)
            {
                company.Id = Guid.NewGuid();
                company.BranchId = User.GetBranchId(); // Giriş yapan şubeye ekle
                company.CreatedDate = DateTime.Now;

                await _unitOfWork.Repository<CompanySetting>().AddAsync(company);
                await _unitOfWork.CommitAsync();

                TempData["Success"] = "Firma başarıyla eklendi.";
                return RedirectToAction("Index");
            }
            return View(company);
        }

        // DÜZENLEME SAYFASI
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var company = await _unitOfWork.Repository<CompanySetting>().GetByIdAsync(id);
            if (company == null) return NotFound();
            return View(company);
        }

        // DÜZENLEME İŞLEMİ
        [HttpPost]
        public async Task<IActionResult> Edit(CompanySetting company)
        {
            var existing = await _unitOfWork.Repository<CompanySetting>().GetByIdAsync(company.Id);
            if (existing == null) return NotFound();

            if (ModelState.IsValid)
            {
                existing.CompanyName = company.CompanyName;
                existing.Phone = company.Phone;
                existing.TaxOffice = company.TaxOffice;
                existing.TaxNumber = company.TaxNumber;
                existing.Address = company.Address;
                // Varsa diğer alanları da eşleştirin
                existing.UpdatedDate = DateTime.Now;

                _unitOfWork.Repository<CompanySetting>().Update(existing);
                await _unitOfWork.CommitAsync();

                TempData["Success"] = "Firma güncellendi.";
                return RedirectToAction("Index");
            }
            return View(company);
        }

        // SİLME İŞLEMİ
        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            var company = await _unitOfWork.Repository<CompanySetting>().GetByIdAsync(id);
            if (company != null)
            {
                _unitOfWork.Repository<CompanySetting>().Remove(company);
                await _unitOfWork.CommitAsync();
                TempData["Success"] = "Firma silindi.";
            }
            return RedirectToAction("Index");
        }
    }
}