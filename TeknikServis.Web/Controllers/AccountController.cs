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
        private readonly ISmsService _smsService;
        private readonly IUnitOfWork _unitOfWork;

        public AccountController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IEmailService emailService,
            ISmsService smsService,
            IUnitOfWork unitOfWork)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _smsService = smsService;
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

            if (user.IsDeleted)
            {
                ModelState.AddModelError("", "Hesabınız pasife alınmıştır. Sisteme giriş yapamazsınız.");
                return View(model);
            }

            var passwordCheck = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordCheck)
            {
                ModelState.AddModelError("", "Hatalı şifre.");
                return View(model);
            }

            // --- 2FA KONTROLÜ ---
            if (user.TwoFactorEnabled)
            {
                string provider = "EmailCode";
                string code = "";
                bool isSent = false;

                // 1. SMS (Öncelikli)
                if (user.IsSmsAuthEnabled && !string.IsNullOrEmpty(user.PhoneNumber))
                {
                    provider = "Phone";
                    try
                    {
                        code = await _userManager.GenerateTwoFactorTokenAsync(user, provider);
                    }
                    catch
                    {
                        // Phone provider yoksa EmailCode token'ını SMS ile gönder
                        provider = "EmailCode";
                        code = await _userManager.GenerateTwoFactorTokenAsync(user, provider);
                    }

                    // --- İSTEK ÜZERİNE GÜNCELLENEN MESAJ İÇERİĞİ ---
                    var smsMsg = $"Merhaba {user.FullName}, Dogrulama Kodunuz : {code}";
                    await _smsService.SendSmsAsync(user.PhoneNumber, smsMsg);
                    isSent = true;
                }
                // 2. E-Posta
                else if (user.IsEmailAuthEnabled)
                {
                    provider = "EmailCode";
                    code = await _userManager.GenerateTwoFactorTokenAsync(user, provider);

                    string mailIcerik = $"<h3>Giriş Doğrulama</h3><p>Merhaba {user.FullName},</p><p>Giriş kodunuz: <strong>{code}</strong></p>";
                    await _emailService.SendEmailAsync(user.Email, "Doğrulama Kodu", mailIcerik);
                    isSent = true;
                }

                if (isSent)
                {
                    return RedirectToAction("VerifyCode", new
                    {
                        email = user.Email,
                        rememberMe = model.RememberMe,
                        provider = provider
                    });
                }
            }

            await _signInManager.SignInAsync(user, model.RememberMe);

            if (await _userManager.IsInRoleAsync(user, "Admin")) return RedirectToAction("Index", "Personnel", new { area = "Admin" });
            else if (await _userManager.IsInRoleAsync(user, "Technician")) return RedirectToAction("TechnicianPanel", "ServiceTicket");

            return RedirectToAction("Index", "Home");
        }

        // --- VERIFY CODE (GET) - GÜNCELLENDİ (ASYNC) ---
        [HttpGet]
        public async Task<IActionResult> VerifyCode(string email, bool rememberMe, string provider = "EmailCode")
        {
            if (string.IsNullOrEmpty(email)) return RedirectToAction("Login");

            // Maskeleme İşlemi için Kullanıcıyı Bul
            string maskedInfo = email; // Varsayılan E-Posta

            if (provider == "Phone")
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user != null && !string.IsNullOrEmpty(user.PhoneNumber))
                {
                    var phone = user.PhoneNumber.Trim();
                    // Son 2 haneyi al, gerisini yıldızla
                    if (phone.Length > 2)
                    {
                        maskedInfo = new string('*', phone.Length - 2) + phone.Substring(phone.Length - 2);
                    }
                    else
                    {
                        maskedInfo = phone; // Çok kısa ise olduğu gibi göster
                    }
                }
            }

            ViewBag.MaskedInfo = maskedInfo; // View'e gönderiyoruz

            var model = new VerifyCodeViewModel
            {
                Email = email,
                RememberMe = rememberMe,
                Provider = provider
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

            var isCodeValid = await _userManager.VerifyTwoFactorTokenAsync(user, model.Provider, model.Code);

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

        [HttpPost]
        public async Task<IActionResult> ChangeBranch(Guid branchId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

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
                BranchId = Guid.Parse("D4346E52-9F3E-4D68-842F-186792266632"),
                TwoFactorEnabled = false
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Deneme");
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();
    }
}