using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Web.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Ad Soyad zorunludur")]
        [Display(Name = "Ad Soyad")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "E-Posta zorunludur")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz")]
        [Display(Name = "E-Posta")]
        public string Email { get; set; }

        [Display(Name = "Telefon Numarası")]
        [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Şifre zorunludur")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Password { get; set; }

        [Compare("Password", ErrorMessage = "Şifreler uyuşmuyor")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre Tekrar")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Lütfen bir şube seçiniz")]
        [Display(Name = "Şube")]
        public Guid BranchId { get; set; }

        [Required(ErrorMessage = "Lütfen bir rol seçiniz")]
        [Display(Name = "Rol")]
        public string UserRole { get; set; }

        // --- İŞLEM YETKİLERİ ---
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }

        // --- BAKİYE HAKLARI ---
        public int PrintBalance { get; set; } = 3;
        public int MailBalance { get; set; } = 3;
        public int CustomerBalance { get; set; } = 3;
        public int TicketBalance { get; set; } = 3;

        // --- GÜVENLİK AYARLARI ---
        [Display(Name = "E-Posta ile Doğrulama")]
        public bool IsEmailAuthEnabled { get; set; } = false;

        [Display(Name = "SMS ile Doğrulama")]
        public bool IsSmsAuthEnabled { get; set; } = false;

        [Display(Name = "Sol Menü Görünsün mü?")]
        public bool IsSidebarVisible { get; set; } = true;

        public List<Guid> SelectedBranchIds { get; set; } = new List<Guid>();

        // --- MENÜLER ---
        public bool ShowHome { get; set; } = true;
        public bool ShowCustomer { get; set; } = true;
        public bool ShowService { get; set; } = true;
        public bool ShowBarcode { get; set; } = true;
        public bool ShowEDevlet { get; set; } = true;
        public bool ShowAudit { get; set; } = false;
        public bool ShowSupport { get; set; } = true;
        public bool ShowStock { get; set; } = true;
        public bool IsShipmentAuthEnabled { get; set; } = true;

        
        public bool IsPriceOfferEnabled { get; set; }



    }
}