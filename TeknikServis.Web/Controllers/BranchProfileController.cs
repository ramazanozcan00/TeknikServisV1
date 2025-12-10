using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeknikServis.Core.Entities;
using TeknikServis.Data.Context;
using TeknikServis.Web.Models;

namespace TeknikServis.Web.Controllers
{
    [Authorize]
    public class BranchProfileController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public BranchProfileController(AppDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var branchId = user.BranchId;

            // 1. Yeni tablodan (BranchInfo) veriyi çek
            var info = await _context.Set<BranchInfo>()
                                     .FirstOrDefaultAsync(x => x.BranchId == branchId);

            // Kayıt yoksa boş oluştur
            if (info == null)
            {
                info = new BranchInfo { BranchId = branchId, AccountType = AccountType.Corporate };
            }

            // 2. Lisans süresi için Branch tablosuna bak
            var branch = await _context.Branches.FindAsync(branchId);

            var model = new CompanyInfoViewModel
            {
                Setting = info, // ViewModel'deki tipi BranchInfo yapmıştık
                LicenseEndDate = branch.LicenseEndDate
            };

            ViewBag.BranchName = branch.BranchName;
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Save(CompanyInfoViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            var branchId = user.BranchId;

            model.Setting.BranchId = branchId;

            var existing = await _context.Set<BranchInfo>()
                                         .FirstOrDefaultAsync(x => x.BranchId == branchId);

            if (existing == null)
            {
                model.Setting.Id = Guid.NewGuid();
                model.Setting.CreatedDate = DateTime.Now;
                _context.Set<BranchInfo>().Add(model.Setting);
            }
            else
            {
                // Güncelleme
                existing.AccountType = model.Setting.AccountType;
                existing.FirstName = model.Setting.FirstName;
                existing.LastName = model.Setting.LastName;
                existing.TCNo = model.Setting.TCNo;
                existing.CompanyName = model.Setting.CompanyName;
                existing.TaxOffice = model.Setting.TaxOffice;
                existing.TaxNumber = model.Setting.TaxNumber;
                existing.Phone = model.Setting.Phone;
                existing.Email = model.Setting.Email;
                existing.Address = model.Setting.Address;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Şube profili kaydedildi.";

            return RedirectToAction("Index");
        }
    }
}