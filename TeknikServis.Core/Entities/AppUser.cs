using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic; // ICollection için gerekli

namespace TeknikServis.Core.Entities
{
    // IdentityUser<Guid> diyerek ID tipinin Guid olduğunu belirtiyoruz.
    public class AppUser : IdentityUser<Guid>
    {
        public string FullName { get; set; }

        // Şube İlişkisi
        public Guid BranchId { get; set; }
        public virtual Branch Branch { get; set; }

        // Görünüm Ayarları
        public bool IsSidebarVisible { get; set; } = true;

        // Loglama Alanları
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? UpdatedDate { get; set; }
        public bool IsDeleted { get; set; } = false;

        // --- BAKİYE / KULLANIM HAKLARI ---
        public int PrintBalance { get; set; } = 0; // Fiş Yazdırma Hakkı
        public int MailBalance { get; set; } = 0;  // Mail Gönderme Hakkı
        public int CustomerBalance { get; set; } = 0; // Müşteri Kayıt Hakkı
        public int TicketBalance { get; set; } = 0; // Servis Kayıt Hakkı

        // --- GÜVENLİK DOĞRULAMA AYARLARI ---
        public bool IsSmsAuthEnabled { get; set; } = false;
        public bool IsEmailAuthEnabled { get; set; } = false;

        public virtual ICollection<UserBranch> AuthorizedBranches { get; set; }
    }
}