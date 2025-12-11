using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Web.Models
{
    public class UserEditViewModel
    {
        public string Id { get; set; }

        [Required(ErrorMessage = "Ad Soyad zorunludur")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "E-Posta zorunludur")]
        [EmailAddress]
        public string Email { get; set; }

        [Display(Name = "Telefon Numarası")]
        [Phone]
        public string PhoneNumber { get; set; }

        public Guid BranchId { get; set; }
        public string UserRole { get; set; }

        public string? Password { get; set; }

        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }

        public int PrintBalance { get; set; }
        public int MailBalance { get; set; }
        public int CustomerBalance { get; set; }
        public int TicketBalance { get; set; }

        // --- GÜVENLİK ---
        [Display(Name = "E-Posta Doğrulama")]
        public bool IsEmailAuthEnabled { get; set; }

        [Display(Name = "SMS Doğrulama")]
        public bool IsSmsAuthEnabled { get; set; }

        public bool IsSidebarVisible { get; set; }

        public List<Guid> SelectedBranchIds { get; set; } = new List<Guid>();

        // Menüler
        public bool ShowHome { get; set; }
        public bool ShowCustomer { get; set; }
        public bool ShowService { get; set; }
        public bool ShowBarcode { get; set; }
        public bool ShowEDevlet { get; set; }
        public bool ShowAudit { get; set; }
        public bool ShowSupport { get; set; }
        public bool ShowStock { get; set; }

        public bool IsShipmentAuthEnabled { get; set; }

        
        public bool IsPriceOfferEnabled { get; set; }
    }
}