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
        [EmailAddress]
        [Display(Name = "E-Posta")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Şifre zorunludur")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Password { get; set; }

        [Compare("Password", ErrorMessage = "Şifreler uyuşmuyor")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre Tekrar")]
        public string ConfirmPassword { get; set; }

        [Display(Name = "Şube")]
        public Guid BranchId { get; set; }

        [Display(Name = "Rol")]
        public string UserRole { get; set; }

        // --- İŞLEM YETKİLERİ ---
        public bool CanCreate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }

        // --- BAKİYE HAKLARI ---
        [Display(Name = "Fiş Yazdırma Hakkı")]
        public int PrintBalance { get; set; } = 3;

        [Display(Name = "Mail Gönderme Hakkı")]
        public int MailBalance { get; set; } = 3;

        [Display(Name = "Müşteri Kayıt Hakkı")]
        public int CustomerBalance { get; set; } = 3;

        [Display(Name = "Servis Kayıt Hakkı")]
        public int TicketBalance { get; set; } = 3;

        // --- GÜVENLİK VE GÖRÜNÜM ---
        [Display(Name = "Girişte Doğrulama Kodu İstensin (2FA)")]
        public bool TwoFactorEnabled { get; set; } = false;

        [Display(Name = "Sol Menü Görünsün mü?")]
        public bool IsSidebarVisible { get; set; } = true; // <-- HATA VEREN ALAN BU

        // --- EK ŞUBELER ---
        public List<Guid> SelectedBranchIds { get; set; } = new List<Guid>();

        // --- MENÜ ERİŞİM YETKİLERİ ---
        [Display(Name = "Ana Sayfa")]
        public bool ShowHome { get; set; } = true;

        [Display(Name = "Müşteriler")]
        public bool ShowCustomer { get; set; } = true;

        [Display(Name = "Servis Kayıtları")]
        public bool ShowService { get; set; } = true;

        [Display(Name = "Barkod Tara")]
        public bool ShowBarcode { get; set; } = true;

        [Display(Name = "E-Devlet")]
        public bool ShowEDevlet { get; set; } = true;

        [Display(Name = "Geçmiş İşlemler")]
        public bool ShowAudit { get; set; } = false;

        [Display(Name = "Destek")]
        public bool ShowSupport { get; set; } = true;
      
        [Display(Name = "Yedek Parça / Stok")]
        public bool ShowStock { get; set; } = true;
    }
}