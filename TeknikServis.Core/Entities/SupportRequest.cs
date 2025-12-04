using System;
using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Core.Entities
{
    public class SupportRequest : BaseEntity
    {
        [Required(ErrorMessage = "Konu başlığı zorunludur")]
        public string Subject { get; set; } // Konu

        [Required(ErrorMessage = "Detaylı bilgi zorunludur")]
        public string Description { get; set; } // Detay

        public string Priority { get; set; } // Öncelik (Düşük, Orta, Yüksek)

        public string FilePath { get; set; } // Dosya Yolu

        public string AdminReply { get; set; } // Admin Cevabı
        public bool IsReplied { get; set; } = false; // Cevaplandı mı?
        public bool IsSeen { get; set; } = false;
        public Guid UserId { get; set; } // Talep Açan Kişi
        public virtual AppUser User { get; set; }
    }
}