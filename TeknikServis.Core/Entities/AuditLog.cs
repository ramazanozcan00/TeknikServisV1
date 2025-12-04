using System;

namespace TeknikServis.Core.Entities
{
    public class AuditLog : BaseEntity
    {
        public string UserId { get; set; }      // İşlemi yapanın ID'si
        public string UserName { get; set; }    // İşlemi yapanın Adı (Kullanıcı silinirse isim kalsın)
        public string Action { get; set; }      // İşlem: Create, Update, Delete
        public string Description { get; set; } // Detay: "Ahmet durumu değiştirdi"
        public string IpAddress { get; set; }   // Güvenlik için IP adresi (Opsiyonel)
        public string Module { get; set; }      
        // Hangi şubede yapıldı?
        public Guid BranchId { get; set; }
    }
}