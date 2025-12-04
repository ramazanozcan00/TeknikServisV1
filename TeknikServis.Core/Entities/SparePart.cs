using System.ComponentModel.DataAnnotations;

namespace TeknikServis.Core.Entities
{
    public class SparePart : BaseEntity
    {
        [Required(ErrorMessage = "Ürün ismi zorunludur")]
        [Display(Name = "Ürün İsmi")]
        public string ProductName { get; set; }

        [Display(Name = "Stok Kodu")]
        public string StockCode { get; set; }

        [Display(Name = "Barkod")]
        public string Barcode { get; set; }

        [Display(Name = "Alış Fiyatı")]
        public decimal PurchasePrice { get; set; }

        [Display(Name = "Satış Fiyatı")]
        public decimal SalesPrice { get; set; }

        [Display(Name = "KDV (%)")]
        public int VatRate { get; set; } = 20; // Varsayılan %20

        [Display(Name = "Birim")]
        public string UnitType { get; set; } // Adet, Kg, Metre vb.

        [Display(Name = "Miktar")]
        public decimal Quantity { get; set; }

        public Guid BranchId { get; set; }
    }
}