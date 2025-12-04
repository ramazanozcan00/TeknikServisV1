using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;

namespace TeknikServis.Core.Interfaces
{
    public interface IAuditLogService
    {
        // Log Kaydetme Metodu:
        // Parametreler: Kim yaptı (User), Hangi Şube (Branch), Hangi Modül, Ne İşlem (Action), Detay ve IP
        Task LogAsync(string userId, string userName, Guid branchId, string module, string action, string description, string ipAddress);

        // Logları Getirme Metodu:
        // Sadece ilgili şubenin geçmişini getirir.
        Task<IEnumerable<AuditLog>> GetLogsByBranchAsync(Guid branchId);

        Task<(IEnumerable<AuditLog> logs, int totalCount)> GetLogsByBranchAsync(Guid branchId, int page, int pageSize);

        
    }
}