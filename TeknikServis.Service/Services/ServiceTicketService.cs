using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;

namespace TeknikServis.Service.Services
{
    public class ServiceTicketService : IServiceTicketService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ServiceTicketService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // --- BARKOD İLE BULMA ---
        public async Task<ServiceTicket> GetTicketByFisNoAsync(string fisNo)
        {
            if (string.IsNullOrEmpty(fisNo)) return null;
            fisNo = fisNo.Trim();

            var tickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.FisNo == fisNo,
                           inc => inc.Customer,
                           inc => inc.Customer.Branch,
                           inc => inc.DeviceBrand,
                           inc => inc.DeviceType);

            return tickets.FirstOrDefault();
        }

        // --- LİSTELEME VE ARAMA ---
        public async Task<IEnumerable<ServiceTicket>> GetAllTicketsByBranchAsync(Guid branchId, string search = null, string status = null)
        {
            var tickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.Customer.BranchId == branchId,
                           inc => inc.Customer,
                           inc => inc.DeviceBrand,
                           inc => inc.DeviceType);

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                tickets = tickets.Where(x =>
                    (x.FisNo != null && x.FisNo.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.SerialNumber != null && x.SerialNumber.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.DeviceBrand != null && x.DeviceBrand.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.DeviceType != null && x.DeviceType.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.DeviceModel != null && x.DeviceModel.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.Customer != null && (x.Customer.FirstName + " " + x.Customer.LastName).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();
            }

            if (!string.IsNullOrEmpty(status))
            {
                tickets = tickets.Where(x => x.Status == status).ToList();
            }

            return tickets.OrderByDescending(x => x.CreatedDate);
        }

        // --- YENİ KAYIT OLUŞTURMA ---
        public async Task CreateTicketAsync(ServiceTicket ticket)
        {
            var customer = await _unitOfWork.Repository<Customer>()
                .GetByIdWithIncludesAsync(c => c.Id == ticket.CustomerId, c => c.Branch);

            string prefix = "TS";

            if (customer?.Branch?.BranchName != null)
            {
                var initials = customer.Branch.BranchName
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Substring(0, 1))
                    .ToArray();

                prefix = string.Join("", initials).ToUpper();
            }

            ticket.FisNo = $"{prefix}-{new Random().Next(100000, 999999)}";
            ticket.Status = "Bekliyor";

            await _unitOfWork.Repository<ServiceTicket>().AddAsync(ticket);
            await _unitOfWork.CommitAsync();
        }

        public async Task UpdateTicketAsync(ServiceTicket ticket)
        {
            var existingTicket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(ticket.Id);

            if (existingTicket != null)
            {
                // Mevcut alanlar
                existingTicket.ProblemDescription = ticket.ProblemDescription;
                existingTicket.SerialNumber = ticket.SerialNumber;
                existingTicket.IsWarranty = ticket.IsWarranty;
                existingTicket.DeviceModel = ticket.DeviceModel;

                if (!string.IsNullOrEmpty(ticket.PhotoPath))
                    existingTicket.PhotoPath = ticket.PhotoPath;

                // --- EKSİK OLAN KISIMLAR (BUNLARI EKLEYİN) ---

                // 1. Teknisyen Ataması
                existingTicket.TechnicianId = ticket.TechnicianId;

                // 2. Cihaz Bilgileri Güncellemesi
                existingTicket.DeviceBrandId = ticket.DeviceBrandId;
                existingTicket.DeviceTypeId = ticket.DeviceTypeId;
                // ----------------------------------------------

                existingTicket.UpdatedDate = DateTime.Now;

                _unitOfWork.Repository<ServiceTicket>().Update(existingTicket);
                await _unitOfWork.CommitAsync();
            }
        }

        // --- DETAY GETİRME (GÜNCELLENDİ) ---
        public async Task<ServiceTicket> GetTicketByIdAsync(Guid id)
        {
            // 1. Ana Kaydı ve İlişkileri Çek
            var ticket = await _unitOfWork.Repository<ServiceTicket>()
                .GetByIdWithIncludesAsync(x => x.Id == id,
                                          x => x.Customer,
                                          x => x.DeviceBrand,
                                          x => x.DeviceType,
                                          x => x.Technician,
                                          x => x.UsedParts); // <-- Parça listesini çekiyoruz

            // 2. Parça İsimlerini Manuel Doldur (GenericRepo kısıtlaması varsa)
            if (ticket != null && ticket.UsedParts != null)
            {
                foreach (var usedPart in ticket.UsedParts)
                {
                    // Her parçanın ismini Stok tablosundan alıyoruz
                    usedPart.SparePart = await _unitOfWork.Repository<SparePart>().GetByIdAsync(usedPart.SparePartId);
                }
            }

            return ticket;
        }

        // --- DURUM GÜNCELLEME ---
        public async Task UpdateTicketStatusAsync(Guid ticketId, string newStatus, decimal? price = null)
        {
            var ticket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(ticketId);
            if (ticket != null)
            {
                ticket.Status = newStatus;
                ticket.UpdatedDate = DateTime.Now;

                if (price.HasValue)
                {
                    ticket.TotalPrice = price.Value;
                }

                _unitOfWork.Repository<ServiceTicket>().Update(ticket);
                await _unitOfWork.CommitAsync();
            }
        }

        // Interface gereği overload
        public async Task<IEnumerable<ServiceTicket>> GetAllTicketsByBranchAsync(Guid branchId)
        {
            return await GetAllTicketsByBranchAsync(branchId, null, null);
        }

        // --- LİSTELEME VE ARAMA (SAYFALAMALI) ---
        public async Task<(IEnumerable<ServiceTicket> tickets, int totalCount)> GetAllTicketsByBranchAsync(Guid branchId, int page, int pageSize, string search = null, string status = null)
        {
            // 1. Tüm Veriyi Hazırla (Henüz çekme)
            // Not: GenericRepository IQueryable dönüyorsa en performanslısı olur. 
            // Eğer IEnumerable dönüyorsa mecburen bellekte yapacağız.
            // Varsayım: Repository FindAsync metodu veriyi çekip getiriyor (IEnumerable).

            var allTickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.Customer.BranchId == branchId,
                           inc => inc.Customer,
                           inc => inc.DeviceBrand,
                           inc => inc.DeviceType);

            // 2. Filtreleme
            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                allTickets = allTickets.Where(x =>
                    (x.FisNo != null && x.FisNo.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.SerialNumber != null && x.SerialNumber.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.DeviceBrand != null && x.DeviceBrand.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.DeviceModel != null && x.DeviceModel.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.Customer != null && (x.Customer.FirstName + " " + x.Customer.LastName).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();
            }

            if (!string.IsNullOrEmpty(status))
            {
                allTickets = allTickets.Where(x => x.Status == status).ToList();
            }

            // 3. Toplam Sayı
            int totalCount = allTickets.Count();

            // 4. Sıralama ve Sayfalama
            var pagedTickets = allTickets
                .OrderByDescending(x => x.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pagedTickets, totalCount);
        }
    }
}