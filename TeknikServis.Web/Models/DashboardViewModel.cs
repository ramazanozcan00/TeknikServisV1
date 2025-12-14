//namespace TeknikServis.Web.Models
//{
//    public class DashboardViewModel
//    {
//        public int TotalTickets { get; set; }      // Toplam Kayıt
//        public int PendingTickets { get; set; }    // Bekleyen (İşlem yapılmamış)
//        public int CompletedTickets { get; set; }  // Tamamlanan
//        public int InProgressTickets { get; set; } // İşlemde olan

//        // İstersen son eklenen 5 kaydı da listede gösterebiliriz
//        public List<TeknikServis.Core.Entities.ServiceTicket> LastTickets { get; set; }

//        public decimal TotalEarnings { get; set; } // Toplam Kazanç
//    }
//}


using System.Collections.Generic;
using TeknikServis.Core.Entities;

namespace TeknikServis.Web.Models
{
    public class DashboardViewModel
    {
        // Kartlar
        public int ActiveTickets { get; set; }
        public int CompletedTickets { get; set; }
        public int PendingRepairs { get; set; }
        public int TotalCustomers { get; set; }
        public int LowStockCount { get; set; }

        // Finansal
        public decimal MonthlyRevenue { get; set; }
        public decimal TotalRevenue { get; set; }

        // Tablolar
        public List<ServiceTicket> RecentTickets { get; set; } // Son Eklenenler
        public List<ServiceTicket> UrgentTickets { get; set; } // --- YENİ EKLENDİ (Acil Kayıtlar) ---

        // Grafikler (Chart.js)
        public string TicketStatusLabels { get; set; }
        public string TicketStatusCounts { get; set; }

        // Gelir grafiği verileri kalsa da olur, View'da kullanmayacağız.
        public string MonthlyRevenueData { get; set; }
        public string RevenueChartLabels { get; set; }
    }
}