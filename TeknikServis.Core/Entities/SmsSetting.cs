using System;
using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Core.Entities
{
    public class SmsSetting : BaseEntity
    {
        public Guid BranchId { get; set; } // Şube ID eklendi

        [Display(Name = "SMS Sağlayıcı Başlığı (Originator)")]
        [Required(ErrorMessage = "Başlık zorunludur")]
        public string SmsTitle { get; set; }

        [Display(Name = "Kullanıcı Adı / No")]
        [Required(ErrorMessage = "Kullanıcı adı zorunludur")]
        public string ApiUsername { get; set; }

        [Display(Name = "Şifre / API Key")]
        [Required(ErrorMessage = "Şifre zorunludur")]
        public string ApiPassword { get; set; }

        [Display(Name = "API URL")]
        [Required(ErrorMessage = "API URL zorunludur")]
        public string ApiUrl { get; set; } = "https://api.iletimerkezi.com/v1/send-sms";

        public bool IsActive { get; set; } = true;
    }
}