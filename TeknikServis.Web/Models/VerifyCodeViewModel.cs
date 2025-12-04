// Dosya: TeknikServis.Web/Models/VerifyCodeViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Web.Models
{
    public class VerifyCodeViewModel
    {
        [Required]
        public string Email { get; set; }

        [Required(ErrorMessage = "Doğrulama kodu gereklidir.")]
        [Display(Name = "Doğrulama Kodu")]
        public string Code { get; set; }

        // Giriş yaparken "Beni Hatırla" işaretlendiyse bunu taşımalıyız
        public bool RememberMe { get; set; }
    }
}