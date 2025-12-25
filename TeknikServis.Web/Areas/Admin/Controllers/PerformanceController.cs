using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions; // User.GetBranchId() için gerekebilir

namespace TeknikServis.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Manager")]
    public class PerformanceController : Controller
    {
        private readonly IServiceTicketService _ticketService;
        private readonly IUnitOfWork _unitOfWork; // Şubeleri çekmek için eklendi
        private readonly UserManager<AppUser> _userManager;

        public PerformanceController(IServiceTicketService ticketService, IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
        {
            _ticketService = ticketService;
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string period = "thisMonth", Guid? branchId = null)
        {
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
            // Şube listesini doldur (Filtreleme dropdown'ı için)
            var branches = await _unitOfWork.Repository<Branch>().GetAllAsync();
            ViewBag.BranchList = new SelectList(branches, "Id", "BranchName", branchId);

            // Eğer branchId seçilmemişse, giriş yapan kullanıcının şubesini varsayılan yap
            // (İsteğe bağlı: Global Admin değilse kendi şubesine zorla)
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null && !branchId.HasValue)
            {
                // Eğer "Tümü" seçeneği gibi bir özellik istemiyorsanız burayı açın:
                branchId = currentUser.BranchId;
            }

            // Eğer dropdown'dan "Tümü" (boş) gelirse branchId null gider ve servis herkesi getirir.

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