using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Core.Entities
{
    public class PriceOffer : BaseEntity
    {
        [Required]
        [Display(Name = "Belge No")]
        public string DocumentNo { get; set; }

        [Required]
        [Display(Name = "Tarih")]
        public DateTime OfferDate { get; set; } = DateTime.Now;

        [Display(Name = "Geçerlilik Tarihi")]
        public DateTime ValidUntil { get; set; } = DateTime.Now.AddDays(7);

        // DEĞİŞİKLİK: int -> Guid
        public Guid CustomerId { get; set; }
        public Customer Customer { get; set; }

        public Guid? BranchId { get; set; }
        public Branch Branch { get; set; }

        public string Notes { get; set; }
        public decimal TotalAmount { get; set; }

        public OfferStatus Status { get; set; } = OfferStatus.Draft;

        public ICollection<PriceOfferItem> Items { get; set; }
    }

    public enum OfferStatus { Draft, Sent, Approved, Rejected }
}