using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Core.Entities
{
    public class DeviceType : BaseEntity
    {
        [Required]
        public string Name { get; set; } // Örn: Telefon, Laptop, Tablet
    }
}