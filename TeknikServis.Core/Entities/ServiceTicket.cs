using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeknikServis.Core.Entities
{
    public class ServiceTicket : BaseEntity
    {
        public Guid BranchId { get; set; }
        public string FisNo { get; set; }
        public Guid CustomerId { get; set; }
        public virtual Customer Customer { get; set; }

        // --- DEĞİŞEN KISIMLAR ---

        // Cihaz Türü (Yeni)
        public Guid DeviceTypeId { get; set; }
        public virtual DeviceType DeviceType { get; set; }

        // Cihaz Markası (Artık String Değil, İlişkili)
        public Guid DeviceBrandId { get; set; }
        public virtual DeviceBrand DeviceBrand { get; set; }

        // Model (Aynı Kaldı - String)
        public string DeviceModel { get; set; }

        // ------------------------
        public DateTime? InvoiceDate { get; set; } // Fatura Tarihi333
        public string Accessories { get; set; }    // Cihazla Gelen Aksesuarlar
        public string PhysicalDamage { get; set; } // Fiziksel Hasar Durumu
        public string PdfPath { get; set; }        // PDF Dosya Yolu
        public string SerialNumber { get; set; }
        public string ProblemDescription { get; set; }
        public string PhotoPath { get; set; }
        public string Status { get; set; } = "Bekliyor";

        // --- YENİ EKLENEN ALAN: Teknisyen Durumu ---
        public string TechnicianStatus { get; set; }
        // -------------------------------------------

        public decimal? TotalPrice { get; set; }
        public bool IsWarranty { get; set; } = false;

        public Guid? TechnicianId { get; set; } // Boş olabilir (Null)

        [ForeignKey("TechnicianId")]
        public virtual AppUser Technician { get; set; }

        public string TechnicianNotes { get; set; }

        // ...
        public virtual ICollection<ServiceTicketPart> UsedParts { get; set; } = new List<ServiceTicketPart>();
        public virtual ICollection<TicketPhoto> TicketPhotos { get; set; } = new List<TicketPhoto>();
    }
}