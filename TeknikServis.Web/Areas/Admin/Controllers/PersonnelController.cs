using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Models;
using TeknikServis.Web.Services;
using System.Text.Json;
using System.Linq;

namespace TeknikServis.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PersonnelController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TenantService _tenantService;

        public PersonnelController(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            IUnitOfWork unitOfWork,
            TenantService tenantService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _unitOfWork = unitOfWork;
            _tenantService = tenantService;
        }

        // LİSTELEME
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users
                .Include(u => u.Branch)
                .OrderBy(u => u.IsDeleted)
                .ToListAsync();

            return View(users);
        }

        // DURUM DEĞİŞTİRME
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.IsDeleted = !user.IsDeleted;
            user.UpdatedDate = DateTime.Now;

            if (user.IsDeleted)
            {
                var assignedTickets = await _unitOfWork.Repository<ServiceTicket>().FindAsync(t => t.TechnicianId == user.Id);
                foreach (var ticket in assignedTickets)
                {
                    ticket.TechnicianId = null;
                    _unitOfWork.Repository<ServiceTicket>().Update(ticket);
                }
                await _unitOfWork.CommitAsync();
            }

            await _userManager.UpdateAsync(user);
            TempData["Success"] = user.IsDeleted ? "Personel pasife alındı." : "Personel aktifleştirildi.";
            return RedirectToAction("Index");
        }

        // --- DÜZELTİLEN METOT: EKLEME (GET) ---
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadDropdownsAsync();

            // BURASI ÖNEMLİ: Yeni bir model gönderiyoruz ki varsayılan (3) değerleri formda gözüksün.
            return View(new RegisterViewModel());
        }

        // EKLEME (POST)
        [HttpPost]
        public async Task<IActionResult> Create(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync();
                return View(model);
            }

            try
            {
                var user = new AppUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    FullName = model.FullName,
                    EmailConfirmed = true,
                    BranchId = model.BranchId,
                    IsShipmentAuthEnabled = model.IsShipmentAuthEnabled,
                    // Haklar (Modelden gelen değerleri alıyoruz)
                    PrintBalance = model.PrintBalance,
                    MailBalance = model.MailBalance,
                    CustomerBalance = model.CustomerBalance,
                    TicketBalance = model.TicketBalance,

                    // Güvenlik Ayarları
                    IsEmailAuthEnabled = model.IsEmailAuthEnabled,
                    IsSmsAuthEnabled = model.IsSmsAuthEnabled,
                    TwoFactorEnabled = model.IsEmailAuthEnabled || model.IsSmsAuthEnabled,

                    // Görünüm ve Modül Yetkileri
                    IsSidebarVisible = model.IsSidebarVisible,
                    IsPriceOfferEnabled = model.IsPriceOfferEnabled, // EKLENDİ

                    IsDeleted = false,
                    CreatedDate = DateTime.Now
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Rol Atama
                    if (!string.IsNullOrEmpty(model.UserRole))
                    {
                        if (!await _roleManager.RoleExistsAsync(model.UserRole))
                        {
                            await _roleManager.CreateAsync(new IdentityRole<Guid>(model.UserRole));
                        }
                        await _userManager.AddToRoleAsync(user, model.UserRole);
                    }

                    // Yetkiler
                    var claims = new List<Claim>();
                    if (model.CanCreate) claims.Add(new Claim("Permission", "Create"));
                    if (model.CanEdit) claims.Add(new Claim("Permission", "Edit"));
                    if (model.CanDelete) claims.Add(new Claim("Permission", "Delete"));

                    if (model.ShowHome) claims.Add(new Claim("MenuAccess", "Home"));
                    if (model.ShowCustomer) claims.Add(new Claim("MenuAccess", "Customer"));
                    if (model.ShowService) claims.Add(new Claim("MenuAccess", "Service"));
                    if (model.ShowBarcode) claims.Add(new Claim("MenuAccess", "Barcode"));
                    if (model.ShowEDevlet) claims.Add(new Claim("MenuAccess", "EDevlet"));
                    if (model.ShowAudit) claims.Add(new Claim("MenuAccess", "Audit"));
                    if (model.ShowSupport) claims.Add(new Claim("MenuAccess", "Support"));
                    if (model.ShowStock) claims.Add(new Claim("MenuAccess", "Stock"));
                    if (model.ShowBranchProfile) claims.Add(new Claim("MenuAccess", "BranchProfile"));
                    if (model.ShowCompanyInfo) claims.Add(new Claim("MenuAccess", "CompanyInfo"));
                    if (model.ShowCustomerMovements) claims.Add(new Claim("MenuAccess", "CustomerMovements"));

                    if (claims.Any()) await _userManager.AddClaimsAsync(user, claims);

                    // YENİ: Performans Menüsü Yetkisini Ekle
                    if (model.IsPerformanceMenuVisible)
                    {
                        await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("MenuAccess", "Performance"));
                    }

                    // Ek Şubeler
                    if (model.SelectedBranchIds != null && model.SelectedBranchIds.Any())
                    {
                        foreach (var branchId in model.SelectedBranchIds)
                        {
                            if (branchId != model.BranchId)
                            {
                                await _unitOfWork.Repository<UserBranch>().AddAsync(new UserBranch
                                {
                                    Id = Guid.NewGuid(),
                                    CreatedDate = DateTime.Now,
                                    UserId = user.Id,
                                    BranchId = branchId
                                });
                            }
                        }
                        await _unitOfWork.CommitAsync();
                    }

                    TempData["Success"] = "Personel başarıyla oluşturuldu.";
                    return RedirectToAction("Index");
                }

                // Identity Hatalarını Göster
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            catch (Exception ex)
            {
                // Beklenmedik hata (Örn: Veritabanı kolonu eksikse burada yakalanır)
                ModelState.AddModelError("", "Kayıt Başarısız: " + ex.Message);
                if (ex.InnerException != null)
                {
                    ModelState.AddModelError("", "Detay: " + ex.InnerException.Message);
                }
            }

            await LoadDropdownsAsync();
            return View(model);
        }

        // GÜNCELLEME (GET)
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.Users
                .Include(u => u.AuthorizedBranches)
                .FirstOrDefaultAsync(u => u.Id.ToString() == id);

            if (user == null || user.IsDeleted) return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);
            var userClaims = await _userManager.GetClaimsAsync(user);

            var model = new UserEditViewModel
            {
                Id = user.Id.ToString(),
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                BranchId = user.BranchId,
                UserRole = userRoles.FirstOrDefault(),

                IsShipmentAuthEnabled = user.IsShipmentAuthEnabled,
                IsWhatsAppEnabled = user.IsWhatsAppEnabled,
                CanCreate = userClaims.Any(c => c.Type == "Permission" && c.Value == "Create"),
                CanEdit = userClaims.Any(c => c.Type == "Permission" && c.Value == "Edit"),
                CanDelete = userClaims.Any(c => c.Type == "Permission" && c.Value == "Delete"),

                ShowHome = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Home"),
                ShowCustomer = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Customer"),
                ShowService = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Service"),
                ShowBarcode = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Barcode"),
                ShowEDevlet = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "EDevlet"),
                ShowAudit = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Audit"),
                ShowSupport = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Support"),
                ShowStock = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Stock"),

                // YENİ: Mevcut claim var mı kontrol et
                IsPerformanceMenuVisible = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Performance"),

                ShowBranchProfile = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "BranchProfile"),
                ShowCompanyInfo = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "CompanyInfo"),
                ShowCustomerMovements = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "CustomerMovements"),

                PrintBalance = user.PrintBalance,
                MailBalance = user.MailBalance,
                CustomerBalance = user.CustomerBalance,
                TicketBalance = user.TicketBalance,

                IsEmailAuthEnabled = user.IsEmailAuthEnabled,
                IsSmsAuthEnabled = user.IsSmsAuthEnabled,
                IsSidebarVisible = user.IsSidebarVisible,
                IsPriceOfferEnabled = user.IsPriceOfferEnabled, // EKLENDİ

                SelectedBranchIds = user.AuthorizedBranches.Select(b => b.BranchId).ToList()
            };

            await LoadDropdownsAsync();
            return View(model);
        }

        // GÜNCELLEME (POST)
        [HttpPost]
        public async Task<IActionResult> Edit(UserEditViewModel model)
        {
            if (!ModelState.IsValid) { await LoadDropdownsAsync(); return View(model); }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null || user.IsDeleted) return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.PhoneNumber = model.PhoneNumber;
            user.UserName = model.Email;
            user.BranchId = model.BranchId;
            user.IsShipmentAuthEnabled = model.IsShipmentAuthEnabled;

            user.PrintBalance = model.PrintBalance;
            user.MailBalance = model.MailBalance;
            user.CustomerBalance = model.CustomerBalance;
            user.TicketBalance = model.TicketBalance;

            user.IsEmailAuthEnabled = model.IsEmailAuthEnabled;
            user.IsSmsAuthEnabled = model.IsSmsAuthEnabled;
            user.TwoFactorEnabled = model.IsEmailAuthEnabled || model.IsSmsAuthEnabled;
            user.IsWhatsAppEnabled = model.IsWhatsAppEnabled;
            user.IsSidebarVisible = model.IsSidebarVisible;
            user.IsPriceOfferEnabled = model.IsPriceOfferEnabled; // EKLENDİ
            user.UpdatedDate = DateTime.Now;

            if (!string.IsNullOrEmpty(model.Password))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                await _userManager.ResetPasswordAsync(user, token, model.Password);
            }

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                var currentRole = userRoles.FirstOrDefault();
                if (currentRole != model.UserRole)
                {
                    if (currentRole != null) await _userManager.RemoveFromRoleAsync(user, currentRole);
                    if (!await _roleManager.RoleExistsAsync(model.UserRole)) await _roleManager.CreateAsync(new IdentityRole<Guid>(model.UserRole));
                    await _userManager.AddToRoleAsync(user, model.UserRole);
                }

                var existingClaims = await _userManager.GetClaimsAsync(user);
                var claimsToRemove = existingClaims.Where(c => c.Type == "Permission" || c.Type == "MenuAccess").ToList();
                if (claimsToRemove.Any()) await _userManager.RemoveClaimsAsync(user, claimsToRemove);

                var newClaims = new List<Claim>();
                if (model.CanCreate) newClaims.Add(new Claim("Permission", "Create"));
                if (model.CanEdit) newClaims.Add(new Claim("Permission", "Edit"));
                if (model.CanDelete) newClaims.Add(new Claim("Permission", "Delete"));

                if (model.ShowHome) newClaims.Add(new Claim("MenuAccess", "Home"));
                if (model.ShowCustomer) newClaims.Add(new Claim("MenuAccess", "Customer"));
                if (model.ShowService) newClaims.Add(new Claim("MenuAccess", "Service"));
                if (model.ShowBarcode) newClaims.Add(new Claim("MenuAccess", "Barcode"));
                if (model.ShowEDevlet) newClaims.Add(new Claim("MenuAccess", "EDevlet"));
                if (model.ShowAudit) newClaims.Add(new Claim("MenuAccess", "Audit"));
                if (model.ShowSupport) newClaims.Add(new Claim("MenuAccess", "Support"));
                if (model.ShowStock) newClaims.Add(new Claim("MenuAccess", "Stock"));
                if (model.ShowBranchProfile) newClaims.Add(new Claim("MenuAccess", "BranchProfile"));
                if (model.ShowCompanyInfo) newClaims.Add(new Claim("MenuAccess", "CompanyInfo"));
                if (model.ShowCustomerMovements) newClaims.Add(new Claim("MenuAccess", "CustomerMovements"));

                if (newClaims.Any()) await _userManager.AddClaimsAsync(user, newClaims);

                // YENİ: Performans Menüsü Yetki Kontrolü ve Güncellemesi
                // Not: Yukarıdaki toplu silme (claimsToRemove) işlemi performans claim'ini de silebilir.
                // Bu yüzden kullanıcıdan gelen kod ile tekrar kontrol edip ekliyoruz/siliyoruz.
                var currentClaims = await _userManager.GetClaimsAsync(user);
                var perfClaim = currentClaims.FirstOrDefault(c => c.Type == "MenuAccess" && c.Value == "Performance");

                if (model.IsPerformanceMenuVisible && perfClaim == null)
                {
                    // Kutucuk işaretli ama yetki yoksa -> EKLE
                    await _userManager.AddClaimAsync(user, new System.Security.Claims.Claim("MenuAccess", "Performance"));
                }
                else if (!model.IsPerformanceMenuVisible && perfClaim != null)
                {
                    // Kutucuk boş ama yetki varsa -> SİL
                    await _userManager.RemoveClaimAsync(user, perfClaim);
                }

                var oldBranches = await _unitOfWork.Repository<UserBranch>().FindAsync(x => x.UserId == user.Id);
                foreach (var item in oldBranches) _unitOfWork.Repository<UserBranch>().Remove(item);

                if (model.SelectedBranchIds != null)
                {
                    foreach (var branchId in model.SelectedBranchIds)
                    {
                        if (branchId != model.BranchId)
                        {
                            await _unitOfWork.Repository<UserBranch>().AddAsync(new UserBranch
                            {
                                Id = Guid.NewGuid(),
                                CreatedDate = DateTime.Now,
                                UserId = user.Id,
                                BranchId = branchId
                            });
                        }
                    }
                }
                await _unitOfWork.CommitAsync();
                await _userManager.UpdateSecurityStampAsync(user);

                TempData["Success"] = "Bilgiler güncellendi.";
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            await LoadDropdownsAsync();
            return View(model);
        }

        // SİLME
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                var assignedTickets = await _unitOfWork.Repository<ServiceTicket>().FindAsync(t => t.TechnicianId == user.Id);
                foreach (var ticket in assignedTickets)
                {
                    ticket.TechnicianId = null;
                    _unitOfWork.Repository<ServiceTicket>().Update(ticket);
                }
                await _unitOfWork.CommitAsync();

                user.IsDeleted = true;
                user.UpdatedDate = DateTime.Now;

                await _userManager.UpdateAsync(user);
                TempData["Success"] = "Personel silindi.";
            }
            else
            {
                TempData["Error"] = "Kullanıcı bulunamadı.";
            }
            return RedirectToAction("Index");
        }

        private async Task LoadDropdownsAsync()
        {
            var branches = await _unitOfWork.Repository<Branch>().GetAllAsync();
            var databases = _tenantService.GetDatabaseList();

            if (!databases.Contains("Main_DB")) databases.Insert(0, "Main_DB");
            ViewBag.DatabaseList = new SelectList(databases);

            var branchData = branches.Select(b => new {
                Id = b.Id,
                Name = b.BranchName,
                DbName = b.DatabaseName ?? "Main_DB"
            }).ToList();
            ViewBag.BranchesJson = JsonSerializer.Serialize(branchData);

            ViewBag.Branches = new SelectList(branches, "Id", "BranchName");
            var roles = await _roleManager.Roles.Select(r => new { Name = r.Name }).ToListAsync();
            ViewBag.Roles = new SelectList(roles, "Name", "Name");
        }
    }
}