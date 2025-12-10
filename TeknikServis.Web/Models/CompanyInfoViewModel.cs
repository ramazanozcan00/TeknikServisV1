using System;
using TeknikServis.Core.Entities;

namespace TeknikServis.Web.Models
{
    public class CompanyInfoViewModel
    {
        // Artık BranchInfo tablosunu kullanıyoruz
        public BranchInfo Setting { get; set; }

        // Lisans bilgileri Branch tablosundan sadece okunur gelir
        public DateTime LicenseEndDate { get; set; }
        public bool IsLicenseActive => LicenseEndDate > DateTime.Now;
        public int RemainingDays => (LicenseEndDate - DateTime.Now).Days;
    }
}