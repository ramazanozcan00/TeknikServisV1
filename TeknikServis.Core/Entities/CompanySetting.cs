using System;
using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Core.Entities
{
    public class CompanySetting : BaseEntity
    {
        public Guid BranchId { get; set; }
        [Display(Name = "Firma İsmi")]
        public string CompanyName { get; set; }

        [Display(Name = "Telefon")]
        public string Phone { get; set; }

        [Display(Name = "Vergi Dairesi")]
        public string TaxOffice { get; set; }

        [Display(Name = "Vergi No")]
        public string TaxNumber { get; set; }

        [Display(Name = "Adres")]
        public string Address { get; set; }
    }
}