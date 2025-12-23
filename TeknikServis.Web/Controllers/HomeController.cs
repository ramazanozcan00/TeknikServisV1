//using Microsoft.AspNetCore.Authorization; // [Authorize] için gerekli
//using Microsoft.AspNetCore.Mvc;
//using TeknikServis.Core.Interfaces;
//using TeknikServis.Web.Extensions;
//using TeknikServis.Web.Models;

//namespace TeknikServis.Web.Controllers
//{
//    [Authorize] // Sadece giriþ yapmýþ kullanýcýlar eriþebilir
//    public class HomeController : Controller
//    {
//        private readonly IServiceTicketService _ticketService;

//        public HomeController(IServiceTicketService ticketService)
//        {
//            _ticketService = ticketService;
//        }

//        public async Task<IActionResult> Index()
//        {
//            // --- TEKNÝSYEN KONTROLÜ (YENÝ EKLENEN) ---
//            // Eðer kullanýcý Teknisyen ise Ana Sayfayý (Dashboard) görmesin,
//            // doðrudan kendi iþ listesine (TechnicianPanel) gitsin.
//            if (User.IsInRole("Technician"))
//            {
//                return RedirectToAction("TechnicianPanel", "ServiceTicket");
//            }
//            // -----------------------------------------

//            var branchId = User.GetBranchId();

//            // Eðer BranchId boþ gelirse (Hata önleyici)
//            if (branchId == Guid.Empty)
//            {
//                // Þubesi olmayan kullanýcýyý çýkýþa yönlendir
//                return RedirectToAction("Logout", "Account");
//            }

//            var allTickets = await _ticketService.GetAllTicketsByBranchAsync(branchId);

//            // Eðer veri tabanýndan null gelirse boþ liste oluþtur
//            if (allTickets == null) allTickets = new List<TeknikServis.Core.Entities.ServiceTicket>();

//            var model = new DashboardViewModel
//            {
//                TotalTickets = allTickets.Count(),
//                PendingTickets = allTickets.Count(x => x.Status == "Bekliyor"),
//                InProgressTickets = allTickets.Count(x => x.Status == "Ýþlemde" || x.Status == "Parça Bekliyor"),
//                CompletedTickets = allTickets.Count(x => x.Status == "Tamamlandý"),
//                LastTickets = allTickets.OrderByDescending(x => x.CreatedDate).Take(5).ToList(),
//                TotalEarnings = allTickets
//                    .Where(x => x.Status == "Tamamlandý" && x.TotalPrice.HasValue)
//                    .Sum(x => x.TotalPrice.Value),
//            };

//            return View(model);
//        }

//        public IActionResult Privacy()
//        {
//            return View();
//        }
//    }
//}



using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions;
using TeknikServis.Web.Models;
using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TeknikServis.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IServiceTicketService _ticketService;
        private readonly ICustomerService _customerService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrencyService _currencyService; // Yeni eklenen servis

        // Constructor güncellendi: ICurrencyService eklendi
        public HomeController(IServiceTicketService ticketService,
                              ICustomerService customerService,
                              IUnitOfWork unitOfWork,
                              ICurrencyService currencyService)
        {
            _ticketService = ticketService;
            _customerService = customerService;
            _unitOfWork = unitOfWork;
            _currencyService = currencyService;
        }

        public async Task<IActionResult> Index()
        {
            // --- TEKNÝSYEN YÖNLENDÝRMESÝ ---
            if (User.IsInRole("Technician"))
            {
                return RedirectToAction("TechnicianPanel", "ServiceTicket");
            }

            // --- BRANCH ID KONTROLÜ ---
            var branchId = User.GetBranchId();
            if (branchId == Guid.Empty)
            {
                return RedirectToAction("Logout", "Account");
            }

            // --- DÖVÝZ KURLARI (YENÝ EKLENDÝ) ---
            // Dashboard açýlýrken kurlarý çekip ViewBag'e atýyoruz
            var rates = await _currencyService.GetDailyRatesAsync();
            ViewBag.Currencies = rates;

            // 1. Tüm Biletleri Çek
            var allTickets = await _ticketService.GetAllTicketsByBranchAsync(branchId);
            if (allTickets == null) allTickets = new List<ServiceTicket>();

            // 2. Müþterileri Çek
            var customers = await _customerService.GetCustomersByBranchAsync(branchId);

            // 3. Kritik Stok Sayýsýný Çek (Miktarý <= 10)
            var lowStockParts = await _unitOfWork.Repository<SparePart>()
                .FindAsync(x => x.BranchId == branchId && x.Quantity <= 10 && !x.IsDeleted);

            // --- HESAPLAMALAR ---

            // 1. AKTÝF SERVÝSLER: 
            // "Tamamlandý", "Ýptal" VE "Teslim Edildi" olanlar HARÝÇ hepsi aktiftir.
            var activeTickets = allTickets
                .Where(x => x.Status != "Tamamlandý" && x.Status != "Ýptal" && x.Status != "Teslim Edildi")
                .ToList();

            // 2. Acil Ýþlem Bekleyen: Durumu "Bekliyor" olan ve 3 günden eski kayýtlar
            var urgentPendingCount = allTickets.Count(x => x.Status == "Bekliyor" && (DateTime.Now - x.CreatedDate).TotalDays > 3);

            // 3. TAMAMLANANLAR:
            // "Tamamlandý" VEYA "Teslim Edildi" olanlar
            var completedTickets = allTickets
                .Where(x => x.Status == "Tamamlandý" || x.Status == "Teslim Edildi")
                .ToList();

            // Ciro Hesaplarý
            var now = DateTime.Now;
            var currentMonthStart = new DateTime(now.Year, now.Month, 1);
            decimal monthlyRevenue = completedTickets
                 .Where(x => (x.InvoiceDate ?? x.UpdatedDate ?? x.CreatedDate) >= currentMonthStart)
                 .Sum(x => x.TotalPrice ?? 0);
            decimal totalRevenue = completedTickets.Sum(x => x.TotalPrice ?? 0);

            // Acil Kayýtlar Listesi
            // Listede "Teslim Edildi" statüsündekiler görünmesin
            var urgentTicketsList = allTickets
                .Where(x => x.Status != "Tamamlandý" && x.Status != "Ýptal" && x.Status != "Teslim Edildi")
                .OrderBy(x => x.CreatedDate)
                .Take(5)
                .ToList();

            // Grafik Verileri
            var statusGroups = allTickets.GroupBy(x => x.Status)
                                         .Select(g => new { Status = g.Key, Count = g.Count() });
            string statusLabels = string.Join(",", statusGroups.Select(x => $"'{x.Status}'"));
            string statusCounts = string.Join(",", statusGroups.Select(x => x.Count));

            // --- MODEL OLUÞTURMA ---
            var model = new DashboardViewModel
            {
                // Kartlar
                ActiveTickets = activeTickets.Count,
                PendingRepairs = urgentPendingCount,
                CompletedTickets = completedTickets.Count,
                TotalCustomers = customers.Count(),
                LowStockCount = lowStockParts.Count(),

                // Finansal
                MonthlyRevenue = monthlyRevenue,
                TotalRevenue = totalRevenue,

                // Listeler
                RecentTickets = allTickets.OrderByDescending(x => x.CreatedDate).Take(6).ToList(),
                UrgentTickets = urgentTicketsList,

                // Grafikler
                TicketStatusLabels = statusLabels,
                TicketStatusCounts = statusCounts
            };

            return View(model);
        }

        // --- CANLI KUR API (YENÝ EKLENDÝ) ---
        [HttpGet]
        public async Task<IActionResult> GetLiveRates()
        {
            var rates = await _currencyService.GetDailyRatesAsync();
            return Json(rates);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}