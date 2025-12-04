using System;

namespace TeknikServis.Core.Entities
{
    public class TicketPhoto : BaseEntity
    {
        public string Path { get; set; } // Fotoğrafın Yolu

        public Guid ServiceTicketId { get; set; }
        public virtual ServiceTicket ServiceTicket { get; set; }
    }
}