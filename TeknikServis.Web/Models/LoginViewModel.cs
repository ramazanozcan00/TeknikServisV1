using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Web.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "E-Posta giriniz")]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Şifre giriniz")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        // --- BU SATIRI EKLEYİN ---
        public bool RememberMe { get; set; }
    }
}