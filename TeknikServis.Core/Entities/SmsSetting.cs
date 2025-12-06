using System;
using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Core.Entities
{
    public class SmsSetting : BaseEntity
    {
        [Display(Name = "SMS Sağlayıcı Başlığı (Originator)")]
        [Required(ErrorMessage = "Başlık zorunludur")]
        public string SmsTitle { get; set; } // Örn: TEKNIKSVS

        [Display(Name = "Kullanıcı Adı / No")]
        [Required(ErrorMessage = "Kullanıcı adı zorunludur")]
        public string ApiUsername { get; set; }

        [Display(Name = "Şifre / API Key")]
        [Required(ErrorMessage = "Şifre zorunludur")]
        public string ApiPassword { get; set; }

        [Display(Name = "API URL")]
        [Required(ErrorMessage = "API URL zorunludur")]
        public string ApiUrl { get; set; } = "https://api.iletimerkezi.com/v1/send-sms"; // Varsayılan

        public bool IsActive { get; set; } = true;
    }
}