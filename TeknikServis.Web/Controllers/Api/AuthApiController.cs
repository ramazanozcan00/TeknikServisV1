using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;

namespace TeknikServis.Web.Controllers.Api
{
    // BURASI ÇOK ÖNEMLİ: Adresi 'api/Auth' olarak sabitliyoruz
    [Route("api/Auth")]
    [ApiController]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;

        public AuthApiController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }
        [HttpGet("Test")]
        public IActionResult Test()
        {
            return Ok("API Çalışıyor!");
        }
        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            if (model == null) return BadRequest("Veri alınamadı.");
            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
                return BadRequest("Kullanıcı adı ve şifre zorunludur.");

            AppUser user = null;

            // 1. E-Posta mı?
            if (model.Username.Contains("@"))
                user = await _userManager.FindByEmailAsync(model.Username);

            // 2. Kullanıcı Adı mı?
            if (user == null)
                user = await _userManager.FindByNameAsync(model.Username);

            if (user == null) return Unauthorized("Kullanıcı bulunamadı.");

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);

            if (result.Succeeded)
            {
                return Ok(new
                {
                    UserId = user.Id,
                    Username = user.UserName,
                    FullName = user.FullName,
                    BranchId = user.BranchId,
                    Role = "Personel",
                    Message = "Giriş Başarılı"
                });
            }

            return Unauthorized("Şifre hatalı.");
        }
    }

    public class LoginDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}