using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Models;
using TeknikServis.Web.Services; // TenantService için
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

        // LİSTELEME (HEPSİNİ GETİR)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // IsDeleted filtresini kaldırdık, hepsini çekiyoruz.
            var users = await _userManager.Users
                .Include(u => u.Branch)
                .OrderBy(u => u.IsDeleted) // Aktifler üstte görünsün
                .ToListAsync();

            return View(users);
        }

        // --- YENİ: DURUM DEĞİŞTİRME (AKTİF/PASİF) ---
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Durumu tersine çevir (Aktifse Pasif, Pasifse Aktif yap)
            user.IsDeleted = !user.IsDeleted;
            user.UpdatedDate = DateTime.Now;

            // Eğer Pasife alınıyorsa, üzerindeki işleri boşa çıkaralım (Temizlik)
            if (user.IsDeleted)
            {
                var assignedTickets = await _unitOfWork.Repository<ServiceTicket>().FindAsync(t => t.TechnicianId == user.Id);
                foreach (var ticket in assignedTickets)
                {
                    ticket.TechnicianId = null; // İş havuza düşer
                    _unitOfWork.Repository<ServiceTicket>().Update(ticket);
                }
                await _unitOfWork.CommitAsync();
            }

            await _userManager.UpdateAsync(user);

            string durum = user.IsDeleted ? "Pasife alındı" : "Aktifleştirildi";
            TempData["Success"] = $"Personel {durum}.";

            return RedirectToAction("Index");
        }

        // EKLEME (GET)
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadDropdownsAsync();
            return View();
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

            var user = new AppUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                EmailConfirmed = true,
                BranchId = model.BranchId,

                // Haklar
                PrintBalance = model.PrintBalance,
                MailBalance = model.MailBalance,
                CustomerBalance = model.CustomerBalance,
                TicketBalance = model.TicketBalance,

                // Ayarlar
                TwoFactorEnabled = model.TwoFactorEnabled,
                IsSidebarVisible = model.IsSidebarVisible,

                // Yeni Kayıt
                IsDeleted = false,
                CreatedDate = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync(model.UserRole))
                {
                    await _roleManager.CreateAsync(new IdentityRole<Guid>(model.UserRole));
                }
                await _userManager.AddToRoleAsync(user, model.UserRole);

                var claims = new List<Claim>();
                // İşlemler
                if (model.CanCreate) claims.Add(new Claim("Permission", "Create"));
                if (model.CanEdit) claims.Add(new Claim("Permission", "Edit"));
                if (model.CanDelete) claims.Add(new Claim("Permission", "Delete"));

                // Menüler
                if (model.ShowHome) claims.Add(new Claim("MenuAccess", "Home"));
                if (model.ShowCustomer) claims.Add(new Claim("MenuAccess", "Customer"));
                if (model.ShowService) claims.Add(new Claim("MenuAccess", "Service"));
                if (model.ShowBarcode) claims.Add(new Claim("MenuAccess", "Barcode"));
                if (model.ShowEDevlet) claims.Add(new Claim("MenuAccess", "EDevlet"));
                if (model.ShowAudit) claims.Add(new Claim("MenuAccess", "Audit"));
                if (model.ShowSupport) claims.Add(new Claim("MenuAccess", "Support"));
                if (model.ShowStock) claims.Add(new Claim("MenuAccess", "Stock"));

                if (claims.Any()) await _userManager.AddClaimsAsync(user, claims);

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

            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
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

            // Silinmişse veya yoksa 404 ver
            if (user == null || user.IsDeleted) return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);
            var userClaims = await _userManager.GetClaimsAsync(user);

            var model = new UserEditViewModel
            {
                Id = user.Id.ToString(),
                FullName = user.FullName,
                Email = user.Email,
                BranchId = user.BranchId,
                UserRole = userRoles.FirstOrDefault(),
                // Yetkiler
                CanCreate = userClaims.Any(c => c.Type == "Permission" && c.Value == "Create"),
                CanEdit = userClaims.Any(c => c.Type == "Permission" && c.Value == "Edit"),
                CanDelete = userClaims.Any(c => c.Type == "Permission" && c.Value == "Delete"),
                // Menüler
                ShowHome = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Home"),
                ShowCustomer = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Customer"),
                ShowService = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Service"),
                ShowBarcode = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Barcode"),
                ShowEDevlet = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "EDevlet"),
                ShowAudit = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Audit"),
                ShowSupport = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Support"),
                ShowStock = userClaims.Any(c => c.Type == "MenuAccess" && c.Value == "Stock"),
                // Ayarlar
                PrintBalance = user.PrintBalance,
                MailBalance = user.MailBalance,
                CustomerBalance = user.CustomerBalance,
                TicketBalance = user.TicketBalance,
                TwoFactorEnabled = user.TwoFactorEnabled,
                IsSidebarVisible = user.IsSidebarVisible,

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
            user.UserName = model.Email;
            user.BranchId = model.BranchId;

            user.PrintBalance = model.PrintBalance;
            user.MailBalance = model.MailBalance;
            user.CustomerBalance = model.CustomerBalance;
            user.TicketBalance = model.TicketBalance;
            user.TwoFactorEnabled = model.TwoFactorEnabled;
            user.IsSidebarVisible = model.IsSidebarVisible;

            user.UpdatedDate = DateTime.Now; // Güncelleme Tarihi

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

                if (newClaims.Any()) await _userManager.AddClaimsAsync(user, newClaims);

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

        // SİLME (SOFT DELETE) - GÜNCELLENDİ
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                // 1. Üzerindeki işleri boşa çıkar (NULL yap)
                var assignedTickets = await _unitOfWork.Repository<ServiceTicket>().FindAsync(t => t.TechnicianId == user.Id);
                foreach (var ticket in assignedTickets)
                {
                    ticket.TechnicianId = null;
                    _unitOfWork.Repository<ServiceTicket>().Update(ticket);
                }
                await _unitOfWork.CommitAsync();

                // 2. SOFT DELETE (IsDeleted = true)
                user.IsDeleted = true;
                user.UpdatedDate = DateTime.Now;

                // DeleteAsync yerine UpdateAsync kullanıyoruz
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                    TempData["Success"] = "Personel (Soft) silindi, işler havuza aktarıldı.";
                else
                    TempData["Error"] = "Silme hatası.";
            }
            else
            {
                TempData["Error"] = "Kullanıcı bulunamadı.";
            }

            return RedirectToAction("Index");
        }

        // YARDIMCI METOT
        private async Task LoadDropdownsAsync()
        {
            var branches = await _unitOfWork.Repository<Branch>().GetAllAsync();

            // SQL'den Tüm Veritabanlarını Çek (TenantService ile)
            var databases = _tenantService.GetDatabaseList(); // Doğru liste

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