using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeknikServis.Core.DTOs;
using TeknikServis.Core.Entities;

namespace TeknikServis.Core.Interfaces
{
    public interface IServiceTicketService
    {
        Task<ServiceTicket> GetTicketByFisNoAsync(string fisNo);
        Task<IEnumerable<ServiceTicket>> GetAllTicketsByBranchAsync(Guid branchId, string search = null, string status = null);

        // FİLTRELEME İÇİN GÜNCELLENEN METOT:
        Task<(IEnumerable<ServiceTicket> tickets, int totalCount)> GetAllTicketsByBranchAsync(Guid branchId, int page, int pageSize, string search, string status, DateTime? startDate = null, DateTime? endDate = null);

        Task CreateTicketAsync(ServiceTicket ticket);
        Task UpdateTicketAsync(ServiceTicket ticket);
        Task<ServiceTicket> GetTicketByIdAsync(Guid id);
        Task UpdateTicketStatusAsync(Guid ticketId, string newStatus, decimal? price = null);
        Task DeleteTicketAsync(Guid id);
        Task<List<TechnicianPerformanceDto>> GetTechnicianPerformanceStatsAsync(DateTime startDate, DateTime endDate, Guid? branchId = null);
    }
}