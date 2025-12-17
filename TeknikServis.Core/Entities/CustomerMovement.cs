using System;

namespace TeknikServis.Core.Entities
{
    public class CustomerMovement : BaseEntity
    {
        public Guid CustomerId { get; set; }
        public virtual Customer Customer { get; set; }

        public Guid? ServiceTicketId { get; set; } // Hangi servisten oluştu?
        public virtual ServiceTicket ServiceTicket { get; set; }

        public decimal Amount { get; set; } // İşlem tutarı
        public string MovementType { get; set; } // "Borç" (Servis Ücreti) veya "Alacak" (Ödeme)
        public string Description { get; set; } // Açıklama

        public Guid BranchId { get; set; } // Şube ayrımı için
    }
}