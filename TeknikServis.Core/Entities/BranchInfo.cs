using System;
using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Core.Entities
{
    // Bu tablo tamamen şubeye özel bilgileri tutar
    public class BranchInfo : BaseEntity
    {
        public Guid BranchId { get; set; }
        // public Branch Branch { get; set; } // İstenirse navigation property eklenebilir

        [Display(Name = "Hesap Türü")]
        public AccountType AccountType { get; set; } = AccountType.Corporate;

        // --- Şahıs (Bireysel) ---
        [Display(Name = "Ad")]
        public string? FirstName { get; set; }

        [Display(Name = "Soyad")]
        public string? LastName { get; set; }

        [Display(Name = "TC Kimlik No")]
        [StringLength(11)]
        public string? TCNo { get; set; }

        // --- Şirket (Kurumsal) ---
        [Display(Name = "Firma/Şube Ünvanı")]
        public string? CompanyName { get; set; }

        [Display(Name = "Vergi Dairesi")]
        public string? TaxOffice { get; set; }

        [Display(Name = "Vergi No")]
        public string? TaxNumber { get; set; }

        // --- İletişim ---
        [Display(Name = "Telefon")]
        public string? Phone { get; set; }

        [Display(Name = "E-Posta")]
        public string? Email { get; set; }

        [Display(Name = "Adres")]
        public string? Address { get; set; }
    }

    public enum AccountType
    {
        [Display(Name = "Kurumsal / Şirket")]
        Corporate = 0,
        [Display(Name = "Bireysel / Şahıs")]
        Individual = 1
    }
}