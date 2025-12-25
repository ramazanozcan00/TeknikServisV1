using System;
using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Core.Entities
{
    public class WhatsAppSetting : BaseEntity
    {
        public Guid BranchId { get; set; } // Hangi şubeye ait olduğu

        [Display(Name = "Instance Adı")]
        [Required(ErrorMessage = "Instance adı zorunludur")]
        public string InstanceName { get; set; } // Evolution API'deki instance adı

        [Display(Name = "API URL")]
        [Required(ErrorMessage = "API URL zorunludur")]
        public string ApiUrl { get; set; } = "http://localhost:8080"; // Varsayılan

        [Display(Name = "API Key")]
        public string ApiKey { get; set; } // API Key (Global Key)

        [Display(Name = "WhatsApp Kredisi")]
        public int WhatsAppCredit { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }
}