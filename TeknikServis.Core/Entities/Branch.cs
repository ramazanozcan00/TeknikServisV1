using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeknikServis.Core.Entities
{
    public class Branch : BaseEntity
    {
        [Required]
        public string BranchName { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }

        // --- YENİ EKLENEN ALANLAR ---
        public string? DatabaseName { get; set; }     // Örn: TeknikServis_Kadikoy
        public string? ConnectionString { get; set; } // Özel bağlantı cümlesi
        public bool HasOwnDatabase { get; set; } = false; // Kendi veritabanı var mı?


        // --- İLİŞKİLER (NAVIGATIONS) ---
        // Hata veren kısımlar buralar olabilir, bunların ekli olduğundan emin olun:
        public virtual ICollection<AppUser> Employees { get; set; }
        public virtual ICollection<Customer> Customers { get; set; }
    }
}
