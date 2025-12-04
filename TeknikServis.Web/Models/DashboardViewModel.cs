namespace TeknikServis.Web.Models
{
    public class DashboardViewModel
    {
        public int TotalTickets { get; set; }      // Toplam Kayıt
        public int PendingTickets { get; set; }    // Bekleyen (İşlem yapılmamış)
        public int CompletedTickets { get; set; }  // Tamamlanan
        public int InProgressTickets { get; set; } // İşlemde olan

        // İstersen son eklenen 5 kaydı da listede gösterebiliriz
        public List<TeknikServis.Core.Entities.ServiceTicket> LastTickets { get; set; }

        public decimal TotalEarnings { get; set; } // Toplam Kazanç
    }
}