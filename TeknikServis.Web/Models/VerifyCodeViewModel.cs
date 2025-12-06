using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Web.Models
{
    public class VerifyCodeViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Doğrulama kodu zorunludur.")]
        [Display(Name = "Doğrulama Kodu")]
        public string Code { get; set; }

        public bool RememberMe { get; set; }

        // Yeni Eklenen Alan: Hangi provider ile gönderildi? (EmailCode veya Phone)
        public string Provider { get; set; } = "EmailCode";
    }
}