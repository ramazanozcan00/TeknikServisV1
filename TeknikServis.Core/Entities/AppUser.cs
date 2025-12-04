using Microsoft.AspNetCore.Identity;
using System;

namespace TeknikServis.Core.Entities
{
    // IdentityUser<Guid> diyerek ID tipinin Guid olduğunu belirtiyoruz.
    // BaseEntity'den Id geliyordu, çakışmaması için BaseEntity'i kaldırıyoruz
    // veya BaseEntity içindeki Id'yi override ediyoruz. 
    // EN TEMİZİ: BaseEntity mirasını kaldırıp gerekli alanları eklemektir.

    public class AppUser : IdentityUser<Guid>
    {
        public string FullName { get; set; }

        // Şube İlişkisi (Aynen kalıyor)
        public Guid BranchId { get; set; }
        public virtual Branch Branch { get; set; }

        public bool IsSidebarVisible { get; set; } = true;

        // CreatedDate IdentityUser'da yok, manuel ekleyelim
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? UpdatedDate { get; set; } // Güncelleme tarihi

        public bool IsDeleted { get; set; } = false; //

        public int PrintBalance { get; set; } = 0; // Fiş Yazdırma Hakkı
        public int MailBalance { get; set; } = 0;  // Mail Gönderme Hakkı

        public int CustomerBalance { get; set; } = 0; // Müşteri Kayıt Hakkı
        public int TicketBalance { get; set; } = 0;

        public virtual ICollection<UserBranch> AuthorizedBranches { get; set; }
    }
}