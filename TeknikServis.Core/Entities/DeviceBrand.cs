using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Core.Entities
{
    public class DeviceBrand : BaseEntity
    {
        [Required]
        public string Name { get; set; } // Örn: Samsung, Apple, Dell
    }
}