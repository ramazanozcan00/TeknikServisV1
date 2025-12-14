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

        public HomeController(IServiceTicketService ticketService, ICustomerService customerService, IUnitOfWork unitOfWork)
        {
            _ticketService = ticketService;
            _customerService = customerService;
            _unitOfWork = unitOfWork;
        }

        public async Task<IActionResult> Index()
        {
            // --- TEKNÝSYEN YÖNLENDÝRMESÝ ---
            if (User.IsInRole("Technician"))
            {
                return RedirectToAction("TechnicianPanel", "ServiceTicket");
            }

            // --- BRANCH ID TANIMLAMASI (Eksik olan kýsým burasýydý) ---
            var branchId = User.GetBranchId();
            if (branchId == Guid.Empty)
            {
                // Þubesi olmayan veya oturumu düþen kullanýcýyý at
                return RedirectToAction("Logout", "Account");
            }
            // ---------------------------------------------------------

            // 1. Tüm Biletleri Çek
            var allTickets = await _ticketService.GetAllTicketsByBranchAsync(branchId);
            if (allTickets == null) allTickets = new List<ServiceTicket>();

            // 2. Müþterileri Çek
            var customers = await _customerService.GetCustomersByBranchAsync(branchId);

            // 3. Kritik Stok Sayýsýný Çek
            // (Stok miktarý 10 ve altý olanlar kritik kabul edildi)
            var lowStockParts = await _unitOfWork.Repository<SparePart>()
                .FindAsync(x => x.BranchId == branchId && x.Quantity <= 10 && !x.IsDeleted);

            // --- HESAPLAMALAR ---

            // Tamamlanmýþ Ýþler
            var completedTickets = allTickets.Where(x => x.Status == "Tamamlandý").ToList();

            // Ciro Hesaplarý (Opsiyonel: View'da kullanýlýyor)
            var now = DateTime.Now;
            var currentMonthStart = new DateTime(now.Year, now.Month, 1);

            decimal monthlyRevenue = completedTickets
                 .Where(x => (x.InvoiceDate ?? x.UpdatedDate ?? x.CreatedDate) >= currentMonthStart)
                 .Sum(x => x.TotalPrice ?? 0);

            decimal totalRevenue = completedTickets.Sum(x => x.TotalPrice ?? 0);

            // --- ACÝL KAYITLAR (YENÝ MANTIK) ---
            // Tamamlanmamýþ, Ýptal edilmemiþ kayýtlarý, ESKÝDEN YENÝYE sýrala.
            var urgentTickets = allTickets
                .Where(x => x.Status != "Tamamlandý" && x.Status != "Ýptal")
                .OrderBy(x => x.CreatedDate) // En eski tarihli en üstte (En acil)
                .Take(5)
                .ToList();

            // Durum Grafiði Verileri
            var statusGroups = allTickets.GroupBy(x => x.Status)
                                         .Select(g => new { Status = g.Key, Count = g.Count() });

            string statusLabels = string.Join(",", statusGroups.Select(x => $"'{x.Status}'"));
            string statusCounts = string.Join(",", statusGroups.Select(x => x.Count));

            // --- MODEL OLUÞTURMA ---
            var model = new DashboardViewModel
            {
                // Kartlar
                ActiveTickets = allTickets.Count(x => x.Status != "Tamamlandý" && x.Status != "Ýptal"),
                CompletedTickets = completedTickets.Count,
                PendingRepairs = allTickets.Count(x => x.Status == "Bekliyor"),
                TotalCustomers = customers.Count(),
                LowStockCount = lowStockParts.Count(),

                // Finansal
                MonthlyRevenue = monthlyRevenue,
                TotalRevenue = totalRevenue,

                // Listeler
                RecentTickets = allTickets.OrderByDescending(x => x.CreatedDate).Take(6).ToList(),
                UrgentTickets = urgentTickets, // <-- Acil kayýtlar listesi

                // Grafikler
                TicketStatusLabels = statusLabels,
                TicketStatusCounts = statusCounts
            };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}