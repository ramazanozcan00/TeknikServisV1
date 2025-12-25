using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions; // GetBranchId için gerekli
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace TeknikServis.Web.Controllers
{
    [Authorize]
    public class SparePartController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public SparePartController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // --- LİSTELEME (ŞUBEYE GÖRE & SAYFALAMALI) ---
        public async Task<IActionResult> Index(int page = 1)
        {
            Guid currentBranchId = User.GetBranchId();
            int pageSize = 10;

            // Sadece bu şubeye ait parçaları çek
            var allParts = await _unitOfWork.Repository<SparePart>()
                .FindAsync(x => x.BranchId == currentBranchId);

            // Miktara göre sırala (Azalanlar üstte - Kritik Stok)
            var sortedParts = allParts.OrderBy(x => x.Quantity).ToList();

            int totalCount = sortedParts.Count();
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var pagedParts = sortedParts.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(pagedParts);
        }

        // --- EKLEME (GET) ---
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Şubeye ait firmaları (Tedarikçileri) çekiyoruz
            var companies = await _unitOfWork.Repository<CompanySetting>()
                .FindAsync(x => x.BranchId == User.GetBranchId());

            // Dropdown için listeyi hazırlıyoruz
            ViewBag.Suppliers = new SelectList(companies.OrderBy(x => x.CompanyName), "Id", "CompanyName");

            return View();
        }

        // --- EKLEME (POST) ---
        [HttpPost]
        public async Task<IActionResult> Create(SparePart model)
        {
            if (ModelState.IsValid)
            {
                model.Id = Guid.NewGuid();
                model.CreatedDate = DateTime.Now;
                model.BranchId = User.GetBranchId();

                // SupplierId formdan otomatik olarak model içine map edilecektir.

                await _unitOfWork.Repository<SparePart>().AddAsync(model);
                await _unitOfWork.CommitAsync();

                TempData["Success"] = "Yedek parça şube stoğuna eklendi.";
                return RedirectToAction("Index");
            }

            // Hata olursa dropdown'ı tekrar doldur
            var companies = await _unitOfWork.Repository<CompanySetting>()
                .FindAsync(x => x.BranchId == User.GetBranchId());
            ViewBag.Suppliers = new SelectList(companies.OrderBy(x => x.CompanyName), "Id", "CompanyName");

            return View(model);
        }
        // --- DÜZENLEME (GET) ---
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var part = await _unitOfWork.Repository<SparePart>().GetByIdAsync(id);

            if (part == null || part.BranchId != User.GetBranchId())
            {
                return NotFound();
            }

            // Düzenleme sayfasında da tedarikçileri listele
            var companies = await _unitOfWork.Repository<CompanySetting>()
                .FindAsync(x => x.BranchId == User.GetBranchId());

            // Mevcut tedarikçiyi seçili getir (part.SupplierId)
            ViewBag.Suppliers = new SelectList(companies.OrderBy(x => x.CompanyName), "Id", "CompanyName", part.SupplierId);

            return View(part);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(SparePart model)
        {
            if (ModelState.IsValid)
            {
                var existing = await _unitOfWork.Repository<SparePart>().GetByIdAsync(model.Id);

                if (existing != null && existing.BranchId == User.GetBranchId())
                {
                    // Diğer alan güncellemeleri...
                    existing.ProductName = model.ProductName;
                    existing.StockCode = model.StockCode;
                    existing.Barcode = model.Barcode;
                    existing.PurchasePrice = model.PurchasePrice;
                    existing.SalesPrice = model.SalesPrice;
                    existing.VatRate = model.VatRate;
                    existing.UnitType = model.UnitType;
                    existing.Quantity = model.Quantity;

                    // --- Yeni Eklenen Alan ---
                    existing.SupplierId = model.SupplierId;

                    existing.UpdatedDate = DateTime.Now;

                    _unitOfWork.Repository<SparePart>().Update(existing);
                    await _unitOfWork.CommitAsync();

                    TempData["Success"] = "Stok güncellendi.";
                    return RedirectToAction("Index");
                }
            }

            // Hata durumunda dropdown'ı tekrar doldur
            var companies = await _unitOfWork.Repository<CompanySetting>()
                .FindAsync(x => x.BranchId == User.GetBranchId());
            ViewBag.Suppliers = new SelectList(companies.OrderBy(x => x.CompanyName), "Id", "CompanyName", model.SupplierId);

            return View(model);
        }

        // --- HAREKETLER (GEÇMİŞ KULLANIM) ---
        [HttpGet]
        public async Task<IActionResult> Movements(Guid id)
        {
            var part = await _unitOfWork.Repository<SparePart>().GetByIdAsync(id);

            // Güvenlik
            if (part == null || part.BranchId != User.GetBranchId())
            {
                return NotFound();
            }

            ViewBag.PartName = part.ProductName;
            ViewBag.StockCode = part.StockCode ?? "-";
            ViewBag.CurrentStock = part.Quantity;

            // ServiceTicketPart tablosundan geçmişi çek
            var history = await _unitOfWork.Repository<ServiceTicketPart>()
                .FindAsync(x => x.SparePartId == id,
                           inc => inc.ServiceTicket,
                           inc => inc.ServiceTicket.Customer);

            return View(history.OrderByDescending(x => x.CreatedDate).ToList());
        }
    }
}