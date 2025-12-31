using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration; // <-- BU GEREKLİ
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;

namespace TeknikServis.Web.Controllers.Api
{
    [Route("api/Auth")]
    [ApiController]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;

        // 1. EKSİK OLAN DEĞİŞKEN BURASIYDI
        private readonly IConfiguration _configuration;

        // 2. CONSTRUCTOR (YAPICI METOD) GÜNCELLENDİ
        public AuthApiController(UserManager<AppUser> userManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _configuration = configuration; // <-- ATAMA BURADA YAPILIYOR
        }

        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            // Kullanıcıyı ve Şubeyi Çek
            var user = await _userManager.Users
                .Include(u => u.Branch)
                .FirstOrDefaultAsync(u => u.UserName == model.Username);

            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                // --- TOKEN OLUŞTURMA ---
                var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                };

                var userRoles = await _userManager.GetRolesAsync(user);
                foreach (var role in userRoles)
                {
                    authClaims.Add(new Claim(ClaimTypes.Role, role));
                }

                // _configuration ARTIK HATA VERMEZ
                var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

                var token = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"],
                    audience: _configuration["Jwt:Audience"],
                    expires: DateTime.Now.AddHours(3),
                    claims: authClaims,
                    signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                // --- CEVAP ---
                return Ok(new
                {
                    token = tokenString,
                    expiration = token.ValidTo,
                    username = user.UserName,
                    userId = user.Id,
                    branchId = user.BranchId,
                    branchName = user.Branch != null ? user.Branch.BranchName : "Merkez"
                });
            }
            return Unauthorized();
        }
    }

    // DTO Sınıfı (Eğer dosyanın altında değilse buraya ekleyebilirsiniz)
    public class LoginDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}