using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;

namespace TeknikServis.Service.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AuditLogService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // GÜNCELLENEN METOT
        public async Task<(IEnumerable<AuditLog> logs, int totalCount)> GetLogsByBranchAsync(Guid branchId, int page, int pageSize)
        {
            // 1. Şubeye ait tüm logları çek
            // (Not: GenericRepository IQueryable dönmediği için mecburen bellekte sayfalıyoruz. 
            // Çok büyük verilerde Repository katmanına Paging eklenmelidir.)
            var allLogs = await _unitOfWork.Repository<AuditLog>().FindAsync(x => x.BranchId == branchId);

            // 2. Sırala
            var orderedLogs = allLogs.OrderByDescending(x => x.CreatedDate);

            // 3. Toplam kayıt sayısını al
            int totalCount = orderedLogs.Count();

            // 4. Sayfala (Skip/Take)
            var pagedLogs = orderedLogs
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pagedLogs, totalCount);
        }

        public Task<IEnumerable<AuditLog>> GetLogsByBranchAsync(Guid branchId)
        {
            throw new NotImplementedException();
        }

        public async Task LogAsync(string userId, string userName, Guid branchId, string module, string action, string description, string ipAddress)
        {
            var log = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserName = userName,
                BranchId = branchId,
                Module = module,
                Action = action,
                Description = description,
                IpAddress = ipAddress,
                CreatedDate = DateTime.Now,
                IsDeleted = false
            };

            await _unitOfWork.Repository<AuditLog>().AddAsync(log);
            await _unitOfWork.CommitAsync();
        }
    }
}