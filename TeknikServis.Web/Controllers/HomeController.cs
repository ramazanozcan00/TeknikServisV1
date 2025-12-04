using Microsoft.AspNetCore.Authorization; // [Authorize] için gerekli
using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions;
using TeknikServis.Web.Models;

namespace TeknikServis.Web.Controllers
{
    [Authorize] // Sadece giriþ yapmýþ kullanýcýlar eriþebilir
    public class HomeController : Controller
    {
        private readonly IServiceTicketService _ticketService;

        public HomeController(IServiceTicketService ticketService)
        {
            _ticketService = ticketService;
        }

        public async Task<IActionResult> Index()
        {
            // --- TEKNÝSYEN KONTROLÜ (YENÝ EKLENEN) ---
            // Eðer kullanýcý Teknisyen ise Ana Sayfayý (Dashboard) görmesin,
            // doðrudan kendi iþ listesine (TechnicianPanel) gitsin.
            if (User.IsInRole("Technician"))
            {
                return RedirectToAction("TechnicianPanel", "ServiceTicket");
            }
            // -----------------------------------------

            var branchId = User.GetBranchId();

            // Eðer BranchId boþ gelirse (Hata önleyici)
            if (branchId == Guid.Empty)
            {
                // Þubesi olmayan kullanýcýyý çýkýþa yönlendir
                return RedirectToAction("Logout", "Account");
            }

            var allTickets = await _ticketService.GetAllTicketsByBranchAsync(branchId);

            // Eðer veri tabanýndan null gelirse boþ liste oluþtur
            if (allTickets == null) allTickets = new List<TeknikServis.Core.Entities.ServiceTicket>();

            var model = new DashboardViewModel
            {
                TotalTickets = allTickets.Count(),
                PendingTickets = allTickets.Count(x => x.Status == "Bekliyor"),
                InProgressTickets = allTickets.Count(x => x.Status == "Ýþlemde" || x.Status == "Parça Bekliyor"),
                CompletedTickets = allTickets.Count(x => x.Status == "Tamamlandý"),
                LastTickets = allTickets.OrderByDescending(x => x.CreatedDate).Take(5).ToList(),
                TotalEarnings = allTickets
                    .Where(x => x.Status == "Tamamlandý" && x.TotalPrice.HasValue)
                    .Sum(x => x.TotalPrice.Value),
            };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}