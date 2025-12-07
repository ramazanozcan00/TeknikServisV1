using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;

namespace TeknikServis.Core.Interfaces
{
    public interface IServiceTicketService
    {
        Task<(IEnumerable<ServiceTicket> tickets, int totalCount)> GetAllTicketsByBranchAsync(Guid branchId, int page, int pageSize, string search = null, string status = null);
        Task<IEnumerable<ServiceTicket>> GetAllTicketsByBranchAsync(Guid branchId);
    
        Task<IEnumerable<ServiceTicket>> GetAllTicketsByBranchAsync(Guid branchId, string search = null, string status = null);

        Task CreateTicketAsync(ServiceTicket ticket);
        Task<ServiceTicket> GetTicketByIdAsync(Guid id);
        // Metot imzasını değiştiriyoruz: price parametresi ekledik
        Task UpdateTicketStatusAsync(Guid ticketId, string newStatus, decimal? price = null);
        Task UpdateTicketAsync(ServiceTicket ticket);
        Task<ServiceTicket> GetTicketByFisNoAsync(string fisNo);
        Task DeleteTicketAsync(Guid id);
    }
}
