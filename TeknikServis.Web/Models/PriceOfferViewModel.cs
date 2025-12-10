using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using TeknikServis.Core.Entities;

namespace TeknikServis.Web.Models
{
    public class PriceOfferViewModel
    {
        public string DocumentNo { get; set; }
        public DateTime OfferDate { get; set; } = DateTime.Now;

        // --- DÜZELTME BURADA: int YERİNE Guid YAPILDI ---
        public Guid CustomerId { get; set; }
        public Guid BranchId { get; set; }
        // ------------------------------------------------
        public Guid Id { get; set; } // <--- BU SATIRI EKLEYİN
        
        public string Notes { get; set; }

        public List<SelectListItem> CustomerList { get; set; }
        public List<SparePart> Products { get; set; }
        public List<Branch> Branches { get; set; }

        public string ItemsJson { get; set; }
    }

    public class PriceOfferItemDto
    {
        // --- DÜZELTME BURADA: int YERİNE Guid? YAPILDI ---
        public Guid? ProductId { get; set; }
        // -------------------------------------------------

        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total { get; set; }
    }
}