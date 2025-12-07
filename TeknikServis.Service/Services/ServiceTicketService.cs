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

            // GÜNCELLEME: Silinmiş kayıtları getirme (!x.IsDeleted)
            var tickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.FisNo == fisNo && !x.IsDeleted,
                           inc => inc.Customer,
                           inc => inc.Customer.Branch,
                           inc => inc.DeviceBrand,
                           inc => inc.DeviceType);

            return tickets.FirstOrDefault();
        }

        // --- LİSTELEME VE ARAMA ---
        public async Task<IEnumerable<ServiceTicket>> GetAllTicketsByBranchAsync(Guid branchId, string search = null, string status = null)
        {
            // GÜNCELLEME: !x.IsDeleted filtresi eklendi.
            var tickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.Customer.BranchId == branchId && !x.IsDeleted,
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

        // --- YENİ KAYIT OLUŞTURMA (BURASI KRİTİK) ---
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

            // --- FİŞ NO ÇAKIŞMA KONTROLÜ ---
            // Rastgele üretilen numaranın veritabanında (silinmiş olsa bile) olup olmadığını kontrol ediyoruz.
            string newFisNo;
            bool isUnique = false;

            do
            {
                newFisNo = $"{prefix}-{new Random().Next(100000, 999999)}";

                // Burada IsDeleted kontrolü YAPMIYORUZ. 
                // Çünkü silinmiş bir kaydın fiş numarasını tekrar kullanmak istemeyiz.
                var checkTicket = await _unitOfWork.Repository<ServiceTicket>()
                    .FindAsync(x => x.FisNo == newFisNo);

                if (!checkTicket.Any())
                {
                    isUnique = true;
                }

            } while (!isUnique);

            ticket.FisNo = newFisNo;
            ticket.Status = "Bekliyor";
            ticket.IsDeleted = false; // Yeni kayıt silinmemiş olarak başlar

            await _unitOfWork.Repository<ServiceTicket>().AddAsync(ticket);
            await _unitOfWork.CommitAsync();
        }

        // --- GÜNCELLEME ---
        public async Task UpdateTicketAsync(ServiceTicket ticket)
        {
            var existingTicket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(ticket.Id);

            if (existingTicket != null)
            {
                existingTicket.ProblemDescription = ticket.ProblemDescription;
                existingTicket.SerialNumber = ticket.SerialNumber;
                existingTicket.IsWarranty = ticket.IsWarranty;
                existingTicket.DeviceModel = ticket.DeviceModel;

                if (!string.IsNullOrEmpty(ticket.PhotoPath))
                    existingTicket.PhotoPath = ticket.PhotoPath;

                existingTicket.TechnicianId = ticket.TechnicianId;
                existingTicket.DeviceBrandId = ticket.DeviceBrandId;
                existingTicket.DeviceTypeId = ticket.DeviceTypeId;
                existingTicket.InvoiceDate = ticket.InvoiceDate;
                existingTicket.Accessories = ticket.Accessories;
                existingTicket.PhysicalDamage = ticket.PhysicalDamage;

                if (!string.IsNullOrEmpty(ticket.PdfPath))
                    existingTicket.PdfPath = ticket.PdfPath;

                existingTicket.UpdatedDate = DateTime.Now;

                _unitOfWork.Repository<ServiceTicket>().Update(existingTicket);
                await _unitOfWork.CommitAsync();
            }
        }

        // --- ID İLE GETİRME ---
        public async Task<ServiceTicket> GetTicketByIdAsync(Guid id)
        {
            // GÜNCELLEME: !x.IsDeleted filtresi (Detay sayfasına manuel linkle gidilirse boş gelsin)
            var ticket = await _unitOfWork.Repository<ServiceTicket>()
                .GetByIdWithIncludesAsync(x => x.Id == id && !x.IsDeleted,
                                          x => x.Customer,
                                          x => x.DeviceBrand,
                                          x => x.DeviceType,
                                          x => x.Technician,
                                          x => x.UsedParts);

            if (ticket != null && ticket.UsedParts != null)
            {
                foreach (var usedPart in ticket.UsedParts)
                {
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

        // --- SİLME (SOFT DELETE) ---
        // Bu metodu Interface'e eklediğinizi varsayıyorum
        public async Task DeleteTicketAsync(Guid id)
        {
            var ticket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(id);
            if (ticket != null)
            {
                // HARD DELETE YERİNE SOFT DELETE YAPIYORUZ
                ticket.IsDeleted = true;
                ticket.UpdatedDate = DateTime.Now;

                // İsterseniz durumunu da güncelleyebilirsiniz
                ticket.Status = "İptal";

                _unitOfWork.Repository<ServiceTicket>().Update(ticket);
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task<IEnumerable<ServiceTicket>> GetAllTicketsByBranchAsync(Guid branchId)
        {
            return await GetAllTicketsByBranchAsync(branchId, null, null);
        }

        // --- SAYFALAMALI LİSTELEME ---
        public async Task<(IEnumerable<ServiceTicket> tickets, int totalCount)> GetAllTicketsByBranchAsync(Guid branchId, int page, int pageSize, string search = null, string status = null)
        {
            // GÜNCELLEME: !x.IsDeleted filtresi
            var allTickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.Customer.BranchId == branchId && !x.IsDeleted,
                           inc => inc.Customer,
                           inc => inc.DeviceBrand,
                           inc => inc.DeviceType);

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

            int totalCount = allTickets.Count();

            var pagedTickets = allTickets
                .OrderByDescending(x => x.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pagedTickets, totalCount);
        }
    }
}