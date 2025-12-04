using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeknikServis.Core.Entities
{
    public class Customer :BaseEntity
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }

        public string? Address { get; set; } // Adres Alanı
        public string? CompanyName { get; set; } // Firma İsmi (Opsiyonel olabilir)
        public string? Phone2 { get; set; }      // Telefon 2
        public string? TCNo { get; set; }        // TC Kimlik No

        public string CustomerType { get; set; } // Normal, Esnaf, Bayi, Problemli

        public string? TaxOffice { get; set; }   // Vergi Dairesi
        public string? TaxNumber { get; set; }   // Vergi No

        public string? City { get; set; }        // İl
        public string? District { get; set; }    // İlçe


        public Guid BranchId { get; set; }
        public virtual Branch Branch { get; set; }
        public virtual ICollection<ServiceTicket> ServiceTickets { get; set; }
    }
}
