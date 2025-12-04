using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Models;
using TeknikServis.Web.Services;

namespace TeknikServis.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IEmailService _emailService;
        private readonly IUnitOfWork _unitOfWork;

        public AccountController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IEmailService emailService,
            IUnitOfWork unitOfWork)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _unitOfWork = unitOfWork;
        }

        // --- LOGIN (GET) ---
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // --- LOGIN İŞLEMİ (POST) ---
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Kullanıcı bulunamadı.");
                return View(model);
            }

            // --- YENİ EKLENEN: PASİF KULLANICI KONTROLÜ ---
            if (user.IsDeleted)
            {
                ModelState.AddModelError("", "Hesabınız pasife alınmıştır. Sisteme giriş yapamazsınız.");
                return View(model);
            }
            // -----------------------------------------------

            var passwordCheck = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordCheck)
            {
                ModelState.AddModelError("", "Hatalı şifre.");
                return View(model);
            }

            // 2FA KONTROLÜ
            if (user.TwoFactorEnabled)
            {
                var code = await _userManager.GenerateTwoFactorTokenAsync(user, "EmailCode");
                string mailIcerik = $"<h3>Giriş Doğrulama</h3><p>Merhaba {user.FullName},</p><p>Giriş kodunuz: <strong>{code}</strong></p>";
                await _emailService.SendEmailAsync(user.Email, "Doğrulama Kodu", mailIcerik);

                return RedirectToAction("VerifyCode", new { email = user.Email, rememberMe = model.RememberMe });
            }

            await _signInManager.SignInAsync(user, model.RememberMe);

            // Yönlendirme
            if (await _userManager.IsInRoleAsync(user, "Admin")) return RedirectToAction("Index", "Personnel", new { area = "Admin" });
            else if (await _userManager.IsInRoleAsync(user, "Technician")) return RedirectToAction("TechnicianPanel", "ServiceTicket");

            return RedirectToAction("Index", "Home");
        }

        // --- VERIFY CODE (GET) ---
        [HttpGet]
        public IActionResult VerifyCode(string email, bool rememberMe)
        {
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Login");

            var model = new VerifyCodeViewModel
            {
                Email = email,
                RememberMe = rememberMe
            };
            return View(model);
        }

        // --- VERIFY CODE (POST) ---
        [HttpPost]
        public async Task<IActionResult> VerifyCode(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return RedirectToAction("Login");

            var isCodeValid = await _userManager.VerifyTwoFactorTokenAsync(user, "EmailCode", model.Code);

            if (isCodeValid)
            {
                await _signInManager.SignInAsync(user, model.RememberMe);

                if (await _userManager.IsInRoleAsync(user, "Admin"))
                    return RedirectToAction("Index", "Personnel", new { area = "Admin" });
                else if (await _userManager.IsInRoleAsync(user, "Technician"))
                    return RedirectToAction("TechnicianPanel", "ServiceTicket");

                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError("", "Kod hatalı veya süresi dolmuş.");
            return View(model);
        }

        // --- ŞUBE DEĞİŞTİRME (SWITCH BRANCH) ---
        [HttpPost]
        public async Task<IActionResult> ChangeBranch(Guid branchId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            // Yetki Kontrolü (Ana Şube mi veya Ek Şube mi?)
            bool hasAccess = (branchId == user.BranchId);
            if (!hasAccess)
            {
                var userBranches = await _unitOfWork.Repository<UserBranch>()
                    .FindAsync(x => x.UserId == user.Id && x.BranchId == branchId);
                if (userBranches.Any()) hasAccess = true;
            }

            if (!hasAccess)
            {
                TempData["Error"] = "Bu şubeye erişim yetkiniz yok.";
                return RedirectToAction("Index", "Home");
            }

            // Yeni Claim (BranchId) Ayarla
            var principal = await _signInManager.CreateUserPrincipalAsync(user);
            var identity = principal.Identity as ClaimsIdentity;

            if (identity != null)
            {
                var existingClaim = identity.FindFirst("BranchId");
                if (existingClaim != null) identity.RemoveClaim(existingClaim);
                identity.AddClaim(new Claim("BranchId", branchId.ToString()));

                var branch = await _unitOfWork.Repository<Branch>().GetByIdAsync(branchId);
                var nameClaim = identity.FindFirst("BranchName");
                if (nameClaim != null) identity.RemoveClaim(nameClaim);
                if (branch != null) identity.AddClaim(new Claim("BranchName", branch.BranchName));
            }

            await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal);
            TempData["Success"] = "Şube değiştirildi.";

            if (User.IsInRole("Technician")) return RedirectToAction("TechnicianPanel", "ServiceTicket");

            return RedirectToAction("Index", "Home");
        }

        // --- REGISTER (Kayıt) ---
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new AppUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                CreatedDate = DateTime.Now,
                BranchId = Guid.Parse("D4346E52-9F3E-4D68-842F-186792266632"), // Varsayılan
                TwoFactorEnabled = false
            };

            string roleToAssign = "Deneme";
            if (roleToAssign == "Deneme")
            {
                user.PrintBalance = 3;
                user.MailBalance = 3;
                user.CustomerBalance = 3;
                user.TicketBalance = 3;
            }

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, roleToAssign);
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View(model);
        }

        // --- LOGOUT ---
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();
    }
}