using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    public class ShipmentController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditLogService _auditLogService;
        private readonly UserManager<AppUser> _userManager;

        public ShipmentController(IUnitOfWork unitOfWork, IAuditLogService auditLogService, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _auditLogService = auditLogService;
            _userManager = userManager;
        }

        // LİSTELEME: Sadece Tamamlanan ve Kargodaki İşleri Getirir
        // ... Mevcut kodlar ...

        // LİSTELEME: Sayfalamalı (Pagination)
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            Guid branchId = User.GetBranchId();
            int pageSize = 10; // Sayfa başına gösterilecek kayıt sayısı

            // 1. Tüm ilgili verileri çek
            var allTickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.Customer.BranchId == branchId &&
                               (x.Status == "Tamamlandı" || x.Status == "Kargolandı"),
                           inc => inc.Customer, inc => inc.DeviceBrand, inc => inc.DeviceType);

            // 2. Sıralama yap (En yeni en üstte)
            var orderedTickets = allTickets.OrderByDescending(x => x.UpdatedDate).ToList();

            // 3. Toplam sayfa sayısını hesapla
            int totalCount = orderedTickets.Count();
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            // 4. Sadece istenen sayfadaki kayıtları al (Skip/Take)
            var pagedTickets = orderedTickets.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // 5. View'a sayfa bilgisini gönder
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(pagedTickets);
        }

        // ... Diğer metodlar (UpdateStatus, BulkPrint vs.) aynen kalsın ...

        // DURUM GÜNCELLEME: Kargoya Verildi veya Teslim Edildi
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(Guid id, string status)
        {
            var ticket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(id);
            if (ticket == null) return NotFound();

            string oldStatus = ticket.Status;
            ticket.Status = status;
            ticket.UpdatedDate = DateTime.Now;

            _unitOfWork.Repository<ServiceTicket>().Update(ticket);
            await _unitOfWork.CommitAsync();

            // Loglama
            try
            {
                string userId = _userManager.GetUserId(User);
                string userName = User.GetFullName();
                Guid branchId = User.GetBranchId();
                await _auditLogService.LogAsync(userId, userName, branchId, "Sevkiyat", "Güncelleme",
                    $"{ticket.FisNo} nolu kayıt '{status}' durumuna güncellendi.", HttpContext.Connection.RemoteIpAddress?.ToString());
            }
            catch { }

            TempData["Success"] = $"Durum '{status}' olarak güncellendi.";
            return RedirectToAction("Index");
        }

        // YAZDIRMA EKRANI: Sevkiyat Fişi
        [HttpGet]
        public async Task<IActionResult> PrintLabel(Guid id)
        {
            var ticket = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.Id == id, inc => inc.Customer, inc => inc.DeviceBrand);

            var record = ticket.FirstOrDefault();
            if (record == null) return NotFound();

            return View(record);
        }


        // ... Mevcut kodlar ...

        // YENİ: Toplu Yazdırma (Post ile ID listesi gelir)
        [HttpPost]
        public async Task<IActionResult> BulkPrint(List<Guid> selectedIds)
        {
            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["Error"] = "Lütfen en az bir kayıt seçiniz.";
                return RedirectToAction("Index");
            }

            // Seçilen kayıtları çek
            var tickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => selectedIds.Contains(x.Id),
                           inc => inc.Customer, inc => inc.DeviceBrand);

            return View(tickets);
        }

        // ... Mevcut kodlar ...

        // YENİ: Toplu Teslimat Listesi (Tek sayfa liste)
        [HttpPost]
        public async Task<IActionResult> DeliveryList(List<Guid> selectedIds)
        {
            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["Error"] = "Lütfen en az bir kayıt seçiniz.";
                return RedirectToAction("Index");
            }

            var tickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => selectedIds.Contains(x.Id),
                           inc => inc.Customer, inc => inc.DeviceBrand);

            return View(tickets);
        }

        // ... Mevcut kodlar ...
    }
}