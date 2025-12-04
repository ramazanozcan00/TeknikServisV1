using System;

namespace TeknikServis.Core.Entities
{
    public class EmailSetting : BaseEntity
    {
        public string SmtpHost { get; set; }       // Örn: smtp.gmail.com
        public int SmtpPort { get; set; }          // Örn: 587
        public string SenderEmail { get; set; }    // Örn: info@teknikservis.com
        public string SenderPassword { get; set; } // Mail şifresi
        public bool EnableSsl { get; set; } = true; // Güvenli bağlantı
    }
}