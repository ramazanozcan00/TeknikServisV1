using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions;
using TeknikServis.Web.Models;

namespace TeknikServis.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IServiceTicketService _ticketService;
        private readonly ICustomerService _customerService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrencyService _currencyService;

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

            // --- DÖVÝZ KURLARI ---
            var rates = await _currencyService.GetDailyRatesAsync();
            ViewBag.Currencies = rates;

            // --- VERÝLERÝ ÇEK ---

            // 1. Servis Fiþlerini Çek (Silinmemiþ olanlar)
            var allTicketsEnumerable = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.Customer.BranchId == branchId && !x.IsDeleted,
                           x => x.Customer,
                           x => x.DeviceBrand);

            var allTickets = allTicketsEnumerable.ToList();

            // Ýstatistikler
            int activeCount = allTickets.Count(x => x.Status != "Tamamlandý" && x.Status != "Ýptal" && x.Status != "Teslim Edildi");
            int completedCount = allTickets.Count(x => x.Status == "Tamamlandý" || x.Status == "Teslim Edildi");
            int pendingRepairCount = allTickets.Count(x => x.Status == "Bekliyor" || x.Status == "Ýþlem Bekliyor" || x.Status == "Parça Bekliyor");

            // --- GÜNCELLEME: KAYITLI MÜÞTERÝ SAYISI DÝNAMÝK HALE GETÝRÝLDÝ ---
            // Þubeye ait ve silinmemiþ müþterileri çekiyoruz.
            var customers = await _unitOfWork.Repository<Customer>().FindAsync(x => x.BranchId == branchId && !x.IsDeleted);
            int totalCustomersCount = customers.Count();
            // ----------------------------------------------------------------

            var stocks = await _unitOfWork.Repository<SparePart>().FindAsync(x => x.BranchId == branchId && x.Quantity <= 10 && !x.IsDeleted);

            // 2. ACÝL / BEKLEYEN ÝÞLER
            var urgentTickets = allTickets
                .Where(x => x.Status != "Tamamlandý" && x.Status != "Ýptal" && x.Status != "Teslim Edildi")
                .OrderBy(x => x.CreatedDate)
                .Take(10)
                .ToList();

            // 3. Son Eklenenler
            var recentTickets = allTickets.OrderByDescending(x => x.CreatedDate).Take(6).ToList();

            // 4. Finansal Veriler
            var now = DateTime.Now;
            var currentMonthStart = new DateTime(now.Year, now.Month, 1);
            decimal monthlyRevenue = allTickets
                 .Where(x => (x.Status == "Tamamlandý" || x.Status == "Teslim Edildi") &&
                             (x.InvoiceDate ?? x.UpdatedDate ?? x.CreatedDate) >= currentMonthStart)
                 .Sum(x => x.TotalPrice ?? 0);

            decimal totalRevenue = allTickets
                 .Where(x => x.Status == "Tamamlandý" || x.Status == "Teslim Edildi")
                 .Sum(x => x.TotalPrice ?? 0);

            // 5. Grafik Verileri
            var statusGroups = allTickets.GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() });
            string statusLabels = string.Join(",", statusGroups.Select(x => $"'{x.Status}'"));
            string statusCounts = string.Join(",", statusGroups.Select(x => x.Count));

            // --- MODEL OLUÞTURMA ---
            var model = new DashboardViewModel
            {
                ActiveTickets = activeCount,
                CompletedTickets = completedCount,
                PendingRepairs = pendingRepairCount,

                // Dinamik müþteri sayýsý buraya atandý
                TotalCustomers = totalCustomersCount,

                LowStockCount = stocks.Count(),

                MonthlyRevenue = monthlyRevenue,
                TotalRevenue = totalRevenue,

                RecentTickets = recentTickets,
                UrgentTickets = urgentTickets,

                TicketStatusLabels = statusLabels,
                TicketStatusCounts = statusCounts
            };

            return View(model);
        }

        // --- CANLI KUR API ---
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