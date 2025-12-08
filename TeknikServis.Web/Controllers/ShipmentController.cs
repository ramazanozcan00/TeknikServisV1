using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
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

        // LİSTELEME: Sayfalamalı (Pagination) - Şube Kontrolü Mevcut
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1)
        {
            Guid branchId = User.GetBranchId();
            int pageSize = 10;

            // Sadece kendi şubesine ait ve durumu 'Tamamlandı' veya 'Kargolandı' olanları getir
            var allTickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.Customer.BranchId == branchId &&
                               (x.Status == "Tamamlandı" || x.Status == "Kargolandı"),
                           inc => inc.Customer, inc => inc.DeviceBrand, inc => inc.DeviceType);

            var orderedTickets = allTickets.OrderByDescending(x => x.UpdatedDate).ToList();

            int totalCount = orderedTickets.Count();
            int totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var pagedTickets = orderedTickets.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(pagedTickets);
        }

        // DURUM GÜNCELLEME: Şube Kontrolü Eklendi
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(Guid id, string status)
        {
            Guid branchId = User.GetBranchId();

            // Sadece kendi şubesindeki kaydı bulup güncellemesine izin veriyoruz
            // Include Customer yapıyoruz çünkü BranchId Customer üzerinde
            var tickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.Id == id && x.Customer.BranchId == branchId, inc => inc.Customer);

            var ticket = tickets.FirstOrDefault();

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

                await _auditLogService.LogAsync(userId, userName, branchId, "Sevkiyat", "Güncelleme",
                    $"{ticket.FisNo} nolu kayıt '{status}' durumuna güncellendi.", HttpContext.Connection.RemoteIpAddress?.ToString());
            }
            catch { }

            TempData["Success"] = $"Durum '{status}' olarak güncellendi.";
            return RedirectToAction("Index");
        }

        // YAZDIRMA EKRANI: Şube Kontrolü Eklendi
        [HttpGet]
        public async Task<IActionResult> PrintLabel(Guid id)
        {
            Guid branchId = User.GetBranchId();

            // ID'si tutsa bile şubesi tutmuyorsa gelmesin
            var ticket = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.Id == id && x.Customer.BranchId == branchId,
                           inc => inc.Customer, inc => inc.DeviceBrand);

            var record = ticket.FirstOrDefault();
            if (record == null) return NotFound();

            return View(record);
        }

        // TOPLU YAZDIRMA: Şube Kontrolü Eklendi
        [HttpPost]
        public async Task<IActionResult> BulkPrint(List<Guid> selectedIds)
        {
            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["Error"] = "Lütfen en az bir kayıt seçiniz.";
                return RedirectToAction("Index");
            }

            Guid branchId = User.GetBranchId();

            // Seçilen ID'ler arasında sadece bu şubeye ait olanları getir
            var tickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => selectedIds.Contains(x.Id) && x.Customer.BranchId == branchId,
                           inc => inc.Customer, inc => inc.DeviceBrand);

            return View(tickets);
        }

        // TOPLU TESLİMAT LİSTESİ: Şube Kontrolü Eklendi
        [HttpPost]
        public async Task<IActionResult> DeliveryList(List<Guid> selectedIds)
        {
            if (selectedIds == null || !selectedIds.Any())
            {
                TempData["Error"] = "Lütfen en az bir kayıt seçiniz.";
                return RedirectToAction("Index");
            }

            Guid branchId = User.GetBranchId();

            // Sadece bu şubeye ait olanları getir
            var tickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => selectedIds.Contains(x.Id) && x.Customer.BranchId == branchId,
                           inc => inc.Customer, inc => inc.DeviceBrand);

            return View(tickets);
        }
    }
}