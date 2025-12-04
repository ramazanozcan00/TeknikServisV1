using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace TeknikServis.Web.Controllers
{
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;
        private readonly IAuditLogService _auditLogService;
        private readonly UserManager<AppUser> _userManager;

        public CustomerController(
          ICustomerService customerService,
          IAuditLogService auditLogService,
          UserManager<AppUser> userManager)
        {
            _customerService = customerService;
            _auditLogService = auditLogService;
            _userManager = userManager;
        }

        // ARAMA VE LİSTELEME (SAYFALAMALI)
        [HttpGet]
        public async Task<IActionResult> Index(string s, int page = 1)
        {
            Guid currentBranchId = User.GetBranchId();
            int pageSize = 10; // Sayfa başına 10 müşteri

            // Servisten veriyi çek
            var result = await _customerService.GetCustomersByBranchAsync(currentBranchId, page, pageSize, s);

            // ViewBag'e verileri at
            ViewBag.Search = s;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)result.totalCount / pageSize);

            return View(result.customers);
        }

        // YENİ MÜŞTERİ SAYFASI (GET)
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // YENİ MÜŞTERİ KAYDETME (POST)
        [HttpPost]
        public async Task<IActionResult> Create(Customer customer)
        {
            // --- 1. HAK KONTROLÜ (Deneme Rolü İçin) ---
            var user = await _userManager.GetUserAsync(User);
            if (await _userManager.IsInRoleAsync(user, "Deneme"))
            {
                if (user.CustomerBalance <= 0)
                {
                    TempData["Error"] = "Deneme süresi: Müşteri kayıt hakkınız dolmuştur (0).";
                    return RedirectToAction("Index");
                }

                // Hakkı varsa düş ve güncelle
                user.CustomerBalance -= 1;
                await _userManager.UpdateAsync(user);
            }
            // -------------------------------------------

            customer.BranchId = User.GetBranchId();
            customer.Id = Guid.NewGuid();

            await _customerService.CreateCustomerAsync(customer);

            // LOGLAMA
            try
            {
                string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                string userName = User.GetFullName();
                string userIp = HttpContext.Connection.RemoteIpAddress?.ToString();

                string aciklama = $"{customer.FirstName} {customer.LastName} isimli yeni müşteri eklendi.";

                await _auditLogService.LogAsync(userId, userName, customer.BranchId, "Müşteri", "Ekleme", aciklama, userIp);
            }
            catch { }

            TempData["Success"] = "Müşteri başarıyla eklendi.";
            return RedirectToAction("Index");
        }

        // DÜZENLEME SAYFASI (GET)
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var customer = await _customerService.GetByIdAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        // DÜZENLEME İŞLEMİ (POST)
        [HttpPost]
        public async Task<IActionResult> Edit(Customer customer)
        {
            if (!ModelState.IsValid)
            {
                return View(customer);
            }

            await _customerService.UpdateCustomerAsync(customer);

            // LOGLAMA İŞLEMİ
            try
            {
                string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                string userName = User.GetFullName();
                Guid branchId = User.GetBranchId();
                string userIp = HttpContext.Connection.RemoteIpAddress?.ToString();

                string aciklama = $"{customer.FirstName} {customer.LastName} isimli müşterinin bilgileri güncellendi.";

                await _auditLogService.LogAsync(userId, userName, branchId, "Müşteri", "Güncelleme", aciklama, userIp);
            }
            catch { }

            TempData["Success"] = "Müşteri bilgileri başarıyla güncellendi.";
            return RedirectToAction("Index");
        }

        // DETAY SAYFASI
        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var customer = await _customerService.GetCustomerDetailsAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        // SİLME İŞLEMİ
        public async Task<IActionResult> Delete(Guid id)
        {
            var customer = await _customerService.GetByIdAsync(id);

            await _customerService.DeleteCustomerAsync(id);

            // LOGLAMA
            if (customer != null)
            {
                try
                {
                    string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    string userName = User.GetFullName();
                    Guid branchId = User.GetBranchId();
                    string userIp = HttpContext.Connection.RemoteIpAddress?.ToString();

                    string aciklama = $"{customer.FirstName} {customer.LastName} isimli müşteri silindi.";

                    await _auditLogService.LogAsync(userId, userName, branchId, "Müşteri", "Silme", aciklama, userIp);
                }
                catch { }
            }

            TempData["Success"] = "Müşteri silindi.";
            return RedirectToAction("Index");
        }
    }
}