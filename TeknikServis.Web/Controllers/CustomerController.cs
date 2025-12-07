using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace TeknikServis.Web.Controllers
{
    public class CustomerController : Controller
    {
        private readonly ICustomerService _customerService;
        private readonly IAuditLogService _auditLogService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IUnitOfWork _unitOfWork; // Gerekli Servis

        public CustomerController(
          ICustomerService customerService,
          IAuditLogService auditLogService,
          UserManager<AppUser> userManager,
          IUnitOfWork unitOfWork) // Dependency Injection
        {
            _customerService = customerService;
            _auditLogService = auditLogService;
            _userManager = userManager;
            _unitOfWork = unitOfWork;
        }

        // --- YARDIMCI METOT: Firmaları Çeker ---
        private async Task LoadCompanyListAsync()
        {
            Guid currentBranchId = User.GetBranchId();

            // CompanySetting tablosundaki firmaları çek
            var companies = await _unitOfWork.Repository<CompanySetting>()
                .FindAsync(x => x.BranchId == currentBranchId);

            // Sadece isimleri alıp sırala
            var companyNames = companies
                .Select(c => c.CompanyName)
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();

            // View'a gönder
            ViewBag.CompanyList = companyNames;
        }

        // ARAMA VE LİSTELEME
        [HttpGet]
        public async Task<IActionResult> Index(string s, int page = 1)
        {
            Guid currentBranchId = User.GetBranchId();
            int pageSize = 10;

            var result = await _customerService.GetCustomersByBranchAsync(currentBranchId, page, pageSize, s);

            ViewBag.Search = s;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)result.totalCount / pageSize);

            return View(result.customers);
        }

        // YENİ MÜŞTERİ SAYFASI (GET)
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadCompanyListAsync(); // <-- Listeyi Yükle
            return View();
        }

        // YENİ MÜŞTERİ KAYDETME (POST)
        [HttpPost]
        public async Task<IActionResult> Create(Customer customer)
        {
            // Hak Kontrolü
            var user = await _userManager.GetUserAsync(User);
            if (await _userManager.IsInRoleAsync(user, "Deneme"))
            {
                if (user.CustomerBalance <= 0)
                {
                    TempData["Error"] = "Deneme süresi: Müşteri kayıt hakkınız dolmuştur.";
                    return RedirectToAction("Index");
                }
                user.CustomerBalance -= 1;
                await _userManager.UpdateAsync(user);
            }

            customer.BranchId = User.GetBranchId();
            customer.Id = Guid.NewGuid();

            await _customerService.CreateCustomerAsync(customer);

            // Loglama
            try
            {
                string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                string userName = User.GetFullName();
                string aciklama = $"{customer.FirstName} {customer.LastName} eklendi.";
                await _auditLogService.LogAsync(userId, userName, customer.BranchId, "Müşteri", "Ekleme", aciklama, HttpContext.Connection.RemoteIpAddress?.ToString());
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

            await LoadCompanyListAsync(); // <-- Listeyi Yükle

            return View(customer);
        }

        // DÜZENLEME İŞLEMİ (POST)
        [HttpPost]
        public async Task<IActionResult> Edit(Customer customer)
        {
            if (!ModelState.IsValid) return View(customer);

            await _customerService.UpdateCustomerAsync(customer);

            try
            {
                string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                string userName = User.GetFullName();
                string aciklama = $"{customer.FirstName} {customer.LastName} güncellendi.";
                await _auditLogService.LogAsync(userId, userName, User.GetBranchId(), "Müşteri", "Güncelleme", aciklama, HttpContext.Connection.RemoteIpAddress?.ToString());
            }
            catch { }

            TempData["Success"] = "Müşteri güncellendi.";
            return RedirectToAction("Index");
        }

        // ... Diğer metodlar (Details, Delete) aynen kalabilir ...
        public async Task<IActionResult> Delete(Guid id)
        {
            var customer = await _customerService.GetByIdAsync(id);
            await _customerService.DeleteCustomerAsync(id);
            TempData["Success"] = "Müşteri silindi.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var customer = await _customerService.GetCustomerDetailsAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }
    }
}