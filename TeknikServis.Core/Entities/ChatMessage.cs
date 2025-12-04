using System;

namespace TeknikServis.Core.Entities
{
    public class ChatMessage : BaseEntity
    {
        public string SenderId { get; set; }    // Gönderen Kişi ID
        public string SenderName { get; set; }  // Gönderen Adı
        public string ReceiverId { get; set; }  // Alıcı ID (Admin cevaplıyorsa kullanıcı ID, kullanıcı atıyorsa 'Admin')
        public string Message { get; set; }     // Mesaj İçeriği
        public bool IsRead { get; set; } = false;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}