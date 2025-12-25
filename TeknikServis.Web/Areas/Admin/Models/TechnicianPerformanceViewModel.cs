namespace TeknikServis.Web.Areas.Admin.Models
{
    public class TechnicianPerformanceViewModel
    {
        public Guid TechnicianId { get; set; }
        public string FullName { get; set; }

        // İş İstatistikleri
        public int TotalAssignedTickets { get; set; } // Atanan Toplam İş
        public int CompletedTickets { get; set; }     // Tamamlanan İş
        public int PendingTickets { get; set; }       // Bekleyen İş
        public int RefundedOrCancelledTickets { get; set; } // İade/İptal

        // Finansal İstatistikler (Aylık/Genel)
        public decimal TotalRevenue { get; set; }     // Ciro (Sadece tamamlananlardan)
        public decimal PotentialRevenue { get; set; } // Bekleyen işlerin potansiyel cirosu

        // Performans Oranı
        public double CompletionRate => TotalAssignedTickets == 0 ? 0 : Math.Round((double)CompletedTickets / TotalAssignedTickets * 100, 2);
    }
}