using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;

namespace TeknikServis.Web.Identity
{
    // 6 haneli sayısal kod üretmek için Totp altyapısını kullanan özel sınıf
    public class EmailCodeTokenProvider : TotpSecurityStampBasedTokenProvider<AppUser>
    {
        public override Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<AppUser> manager, AppUser user)
        {
            // E-posta adresi varsa kod üretebilir
            return Task.FromResult(!string.IsNullOrWhiteSpace(user.Email));
        }

        // Kodun kişiye özel olması için modifier
        public override Task<string> GetUserModifierAsync(string purpose, UserManager<AppUser> manager, AppUser user)
        {
            return Task.FromResult("EmailCode:" + purpose + ":" + user.Email);
        }
    }
}