using System;

namespace TeknikServis.Core.Entities
{
    public class EmailSetting : BaseEntity
    {
        public Guid BranchId { get; set; } // Şube ID eklendi

        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public string SenderEmail { get; set; }
        public string SenderPassword { get; set; }
        public bool EnableSsl { get; set; } = true;
    }
}