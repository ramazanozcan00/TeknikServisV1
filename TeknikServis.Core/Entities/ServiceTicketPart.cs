using System;

namespace TeknikServis.Core.Entities
{
    public class ServiceTicketPart : BaseEntity
    {
        public Guid ServiceTicketId { get; set; }
        public virtual ServiceTicket ServiceTicket { get; set; }

        public Guid SparePartId { get; set; }
        public virtual SparePart SparePart { get; set; }

        public int Quantity { get; set; } // Kaç adet kullanıldı?
        public decimal Price { get; set; } // O anki satış fiyatı (Tarihçe için)
        public decimal TotalPrice => Quantity * Price; // Ara toplam
    }
}