using System;
using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Core.Entities
{
    public class Announcement : BaseEntity
    {
        [Required(ErrorMessage = "Başlık zorunludur")]
        public string Title { get; set; }      // Duyuru Başlığı

        public string Content { get; set; }    // İçerik

        public bool IsActive { get; set; } = true; // Yayında mı?
    }
}