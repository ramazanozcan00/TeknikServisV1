using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using TeknikServis.Core.Entities;
using TeknikServis.Data.Context; // DbContext için gerekli

namespace TeknikServis.Web.Identity
{
    public class CustomUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser, IdentityRole<Guid>>
    {
        // DbContext'i buraya çağırıyoruz
        private readonly AppDbContext _context;

        public CustomUserClaimsPrincipalFactory(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            IOptions<IdentityOptions> optionsAccessor,
            AppDbContext context) // Constructor'a ekledik
            : base(userManager, roleManager, optionsAccessor)
        {
            _context = context;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            // 1. Şube ID'sini ekle (Zaten yapmıştık)
            identity.AddClaim(new Claim("BranchId", user.BranchId.ToString()));

            // 2. Ad Soyad ekle (Zaten yapmıştık)
            identity.AddClaim(new Claim("FullName", user.FullName ?? user.UserName));

            identity.AddClaim(new Claim("IsShipmentAuthEnabled", user.IsShipmentAuthEnabled.ToString()));
            // 3. YENİ KISIM: Şube Adını Veritabanından Bul ve Ekle
            // Kullanıcı her giriş yaptığında sadece 1 kere çalışır, performansı etkilemez.
            var branchName = _context.Branches.Find(user.BranchId)?.BranchName;

            identity.AddClaim(new Claim("BranchName", branchName ?? "Bilinmeyen Şube"));

            return identity;
        }
    }
}