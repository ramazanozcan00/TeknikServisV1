using System;

namespace TeknikServis.Core.Entities
{
    public class PriceOfferItem : BaseEntity
    {
        // DEĞİŞİKLİK: int -> Guid
        public Guid PriceOfferId { get; set; }
        public PriceOffer PriceOffer { get; set; }

        public Guid? SparePartId { get; set; } // Stoktan seçilirse
        public SparePart SparePart { get; set; }

        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}