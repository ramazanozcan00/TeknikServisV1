using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Security.Claims; // Claim kontrolü için gerekli
using System.Threading.Tasks;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions;

namespace TeknikServis.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize] // Rol kısıtlaması kaldırıldı, sadece giriş yapmış olmak yeterli
    public class PerformanceController : Controller
    {
        private readonly IServiceTicketService _ticketService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;

        public PerformanceController(IServiceTicketService ticketService, IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
        {
            _ticketService = ticketService;
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string period = "thisMonth", Guid? branchId = null)
        {
            // --- YETKİ KONTROLÜ ---
            // Eğer kullanıcı Admin değilse, Manager değilse VE "Performance" menü yetkisi (kutucuğu) yoksa engelle.
            if (!User.IsInRole("Admin") &&
                !User.IsInRole("Manager") &&
                !User.HasClaim(c => c.Type == "MenuAccess" && c.Value == "Performance"))
            {
                return RedirectToAction("AccessDenied", "Account", new { area = "" });
            }
            // ---------------------

            // --- Tarih Ayarları ---
            DateTime startDate, endDate;
            endDate = DateTime.Now;

            switch (period)
            {
                case "lastMonth":
                    startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    break;
                case "thisYear":
                    startDate = new DateTime(DateTime.Now.Year, 1, 1);
                    break;
                case "thisMonth":
                default:
                    startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    break;
            }

            // --- Şube Ayarları ---
            var branches = await _unitOfWork.Repository<Branch>().GetAllAsync();
            ViewBag.BranchList = new SelectList(branches, "Id", "BranchName", branchId);

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null && !branchId.HasValue)
            {
                // Eğer şube seçilmemişse kullanıcının kendi şubesini getir
                branchId = currentUser.BranchId;
            }

            // --- Verileri Çek ---
            var stats = await _ticketService.GetTechnicianPerformanceStatsAsync(startDate, endDate, branchId);

            ViewBag.Period = period;
            ViewBag.StartDate = startDate.ToShortDateString();
            ViewBag.EndDate = endDate.ToShortDateString();
            ViewBag.SelectedBranchId = branchId;

            return View(stats);
        }
    }
}