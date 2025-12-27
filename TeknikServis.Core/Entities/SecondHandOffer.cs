using System;
using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Core.Entities
{
    public class SecondHandOffer : BaseEntity
    {
        [Display(Name = "Müşteri Adı")]
        public string CustomerName { get; set; }

        [Display(Name = "Telefon")]
        public string Phone { get; set; }

        [Display(Name = "Cihaz Markası")]
        public string DeviceBrand { get; set; }

        [Display(Name = "Cihaz Modeli")]
        public string DeviceModel { get; set; }

        [Display(Name = "Kozmetik Durum")]
        public string Condition { get; set; } // Örn: Çiziksiz, Az Çizik, Kırık

        public bool IsWorking { get; set; } // Cihaz açılıyor mu?
        public bool HasBox { get; set; }    // Kutusu var mı?
        public bool HasWarranty { get; set; } // Garantisi var mı?

        [Display(Name = "Robotun Verdiği Fiyat")]
        public decimal EstimatedPrice { get; set; }

        [Display(Name = "Durum")]
        public string Status { get; set; } = "Bekliyor"; // Bekliyor, Onaylandı, Reddedildi
    }
}