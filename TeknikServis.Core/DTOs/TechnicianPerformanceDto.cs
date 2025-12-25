using System;

namespace TeknikServis.Core.DTOs
{
    public class TechnicianPerformanceDto
    {
        public Guid TechnicianId { get; set; }
        public string FullName { get; set; }

        public int TotalAssignedTickets { get; set; }
        public int CompletedTickets { get; set; }
        public int PendingTickets { get; set; }
        public int RefundedOrCancelledTickets { get; set; }

        public decimal TotalRevenue { get; set; }
        public decimal PotentialRevenue { get; set; }

        public double CompletionRate => TotalAssignedTickets == 0 ? 0 : Math.Round((double)CompletedTickets / TotalAssignedTickets * 100, 2);
    }
}