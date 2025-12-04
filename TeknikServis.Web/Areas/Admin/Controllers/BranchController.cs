using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Services;
using System;
using System.Threading.Tasks;

namespace TeknikServis.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class BranchController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly TenantService _tenantService;

        public BranchController(IUnitOfWork unitOfWork, TenantService tenantService)
        {
            _unitOfWork = unitOfWork;
            _tenantService = tenantService;
        }

        // LİSTELEME
        public async Task<IActionResult> Index()
        {
            var branches = await _unitOfWork.Repository<Branch>().GetAllAsync();
            return View(branches);
        }

        // EKLEME SAYFASI (GET)
        [HttpGet]
        public IActionResult Create()
        {
            // Mevcut veritabanlarını listele
            ViewBag.DatabaseList = new SelectList(_tenantService.GetDatabaseList());
            return View();
        }

        // EKLEME İŞLEMİ (POST)
        [HttpPost]
        public async Task<IActionResult> Create(Branch branch, string selectedDbName)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.DatabaseList = new SelectList(_tenantService.GetDatabaseList());
                return View(branch);
            }

            try
            {
                // Seçilen veritabanını ata
                if (!string.IsNullOrEmpty(selectedDbName))
                {
                    branch.DatabaseName = selectedDbName;
                    branch.ConnectionString = _tenantService.GetConnectionString(selectedDbName);

                    // Eğer seçilen DB ana DB değilse "Özel DB" sayılır
                    // (Basit kontrol: Connection string içinde veritabanı adı farklıysa)
                    branch.HasOwnDatabase = !selectedDbName.Equals("TeknikServisDb", StringComparison.OrdinalIgnoreCase);
                    // Not: Ana DB isminiz farklıysa burayı ona göre düzeltin veya her zaman true yapın.
                }
                else
                {
                    // Boş geldiyse hata ver veya varsayılan ata
                    TempData["Error"] = "Lütfen bir veritabanı seçiniz.";
                    ViewBag.DatabaseList = new SelectList(_tenantService.GetDatabaseList());
                    return View(branch);
                }

                branch.Id = Guid.NewGuid();
                branch.CreatedDate = DateTime.Now;

                await _unitOfWork.Repository<Branch>().AddAsync(branch);
                await _unitOfWork.CommitAsync();

                TempData["Success"] = "Şube başarıyla oluşturuldu.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Hata: " + ex.Message;
                ViewBag.DatabaseList = new SelectList(_tenantService.GetDatabaseList());
                return View(branch);
            }
        }

        // DÜZENLEME SAYFASI (GET)
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var branch = await _unitOfWork.Repository<Branch>().GetByIdAsync(id);
            if (branch == null) return NotFound();
            return View(branch);
        }

        // DÜZENLEME İŞLEMİ (POST)
        [HttpPost]
        public async Task<IActionResult> Edit(Branch branch)
        {
            if (!ModelState.IsValid) return View(branch);

            var existingBranch = await _unitOfWork.Repository<Branch>().GetByIdAsync(branch.Id);
            if (existingBranch == null) return NotFound();

            existingBranch.BranchName = branch.BranchName;
            existingBranch.Phone = branch.Phone;
            existingBranch.Address = branch.Address;

            _unitOfWork.Repository<Branch>().Update(existingBranch);
            await _unitOfWork.CommitAsync();

            TempData["Success"] = "Şube bilgileri güncellendi.";
            return RedirectToAction("Index");
        }

        // SİLME İŞLEMİ
        public async Task<IActionResult> Delete(Guid id)
        {
            var branch = await _unitOfWork.Repository<Branch>().GetByIdAsync(id);
            if (branch != null)
            {
                _unitOfWork.Repository<Branch>().Remove(branch);
                await _unitOfWork.CommitAsync();
                TempData["Success"] = "Şube silindi.";
            }
            return RedirectToAction("Index");
        }
    }
}