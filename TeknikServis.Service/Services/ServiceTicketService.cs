using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeknikServis.Core.DTOs;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;

namespace TeknikServis.Service.Services
{
    public class ServiceTicketService : IServiceTicketService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;

        public ServiceTicketService(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        public async Task<ServiceTicket> GetTicketByFisNoAsync(string fisNo)
        {
            if (string.IsNullOrEmpty(fisNo)) return null;
            fisNo = fisNo.Trim();
            var tickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.FisNo == fisNo && !x.IsDeleted,
                           inc => inc.Customer,
                           inc => inc.Customer.Branch,
                           inc => inc.DeviceBrand,
                           inc => inc.DeviceType);
            return tickets.FirstOrDefault();
        }

        public async Task<IEnumerable<ServiceTicket>> GetAllTicketsByBranchAsync(Guid branchId, string search = null, string status = null)
        {
            var tickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.Customer.BranchId == branchId && !x.IsDeleted,
                           inc => inc.Customer, inc => inc.DeviceBrand, inc => inc.DeviceType);

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                tickets = tickets.Where(x =>
                    (x.FisNo != null && x.FisNo.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Customer != null && (x.Customer.FirstName + " " + x.Customer.LastName).Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                    (x.SerialNumber != null && x.SerialNumber.Contains(search, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            if (!string.IsNullOrEmpty(status))
            {
                tickets = tickets.Where(x => x.Status == status).ToList();
            }

            return tickets.OrderByDescending(x => x.CreatedDate);
        }

        // --- SAYFALAMALI VE TARİH FİLTRELİ LİSTELEME (DÜZELTİLEN METOT) ---
        public async Task<(IEnumerable<ServiceTicket> tickets, int totalCount)> GetAllTicketsByBranchAsync(Guid branchId, int page, int pageSize, string search, string status, DateTime? startDate = null, DateTime? endDate = null)
        {
            // 1. Verileri Çek
            var tickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.Customer.BranchId == branchId && !x.IsDeleted,
                           inc => inc.Customer,
                           inc => inc.DeviceBrand,
                           inc => inc.DeviceType);

            // 2. Arama Filtresi
            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                tickets = tickets.Where(x =>
                    (x.FisNo != null && x.FisNo.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.SerialNumber != null && x.SerialNumber.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.DeviceModel != null && x.DeviceModel.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.Customer != null && (x.Customer.FirstName + " " + x.Customer.LastName).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (x.Customer != null && x.Customer.Phone.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                );
            }

            // 3. Durum Filtresi
            if (!string.IsNullOrEmpty(status))
            {
                tickets = tickets.Where(x => x.Status == status);
            }

            // 4. Tarih Filtresi (Kritik Kısım)
            if (startDate.HasValue)
            {
                // Başlangıç gününün 00:00:00 anından itibaren
                var start = startDate.Value.Date;
                tickets = tickets.Where(x => x.CreatedDate >= start);
            }

            if (endDate.HasValue)
            {
                // Bitiş gününün 23:59:59 anına kadar
                var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
                tickets = tickets.Where(x => x.CreatedDate <= end);
            }

            // 5. Sayfalama
            int totalCount = tickets.Count();

            var pagedTickets = tickets
                .OrderByDescending(x => x.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pagedTickets, totalCount);
        }

        public async Task CreateTicketAsync(ServiceTicket ticket)
        {
            var customer = await _unitOfWork.Repository<Customer>()
                .GetByIdWithIncludesAsync(c => c.Id == ticket.CustomerId, c => c.Branch);

            string prefix = "TS";
            if (customer?.Branch?.BranchName != null)
            {
                var initials = customer.Branch.BranchName
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Substring(0, 1)).ToArray();
                prefix = string.Join("", initials).ToUpper();
            }

            string newFisNo;
            bool isUnique = false;
            do
            {
                newFisNo = $"{prefix}-{new Random().Next(100000, 999999)}";
                var checkTicket = await _unitOfWork.Repository<ServiceTicket>().FindAsync(x => x.FisNo == newFisNo);
                if (!checkTicket.Any()) isUnique = true;
            } while (!isUnique);

            ticket.FisNo = newFisNo;
            ticket.Status = "Bekliyor";
            ticket.IsDeleted = false;
            await _unitOfWork.Repository<ServiceTicket>().AddAsync(ticket);
            await _unitOfWork.CommitAsync();
        }

        public async Task UpdateTicketAsync(ServiceTicket ticket)
        {
            var existingTicket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(ticket.Id);
            if (existingTicket != null)
            {
                existingTicket.ProblemDescription = ticket.ProblemDescription;
                existingTicket.SerialNumber = ticket.SerialNumber;
                existingTicket.IsWarranty = ticket.IsWarranty;
                existingTicket.DeviceModel = ticket.DeviceModel;
                if (!string.IsNullOrEmpty(ticket.PhotoPath)) existingTicket.PhotoPath = ticket.PhotoPath;
                existingTicket.TechnicianId = ticket.TechnicianId;
                existingTicket.DeviceBrandId = ticket.DeviceBrandId;
                existingTicket.DeviceTypeId = ticket.DeviceTypeId;
                existingTicket.InvoiceDate = ticket.InvoiceDate;
                existingTicket.Accessories = ticket.Accessories;
                existingTicket.PhysicalDamage = ticket.PhysicalDamage;
                if (!string.IsNullOrEmpty(ticket.PdfPath)) existingTicket.PdfPath = ticket.PdfPath;
                existingTicket.UpdatedDate = DateTime.Now;
                _unitOfWork.Repository<ServiceTicket>().Update(existingTicket);
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task<ServiceTicket> GetTicketByIdAsync(Guid id)
        {
            var ticket = await _unitOfWork.Repository<ServiceTicket>()
                .GetByIdWithIncludesAsync(x => x.Id == id && !x.IsDeleted,
                                          x => x.Customer, x => x.DeviceBrand, x => x.DeviceType,
                                          x => x.Technician, x => x.UsedParts);
            if (ticket != null && ticket.UsedParts != null)
            {
                foreach (var usedPart in ticket.UsedParts)
                    usedPart.SparePart = await _unitOfWork.Repository<SparePart>().GetByIdAsync(usedPart.SparePartId);
            }
            return ticket;
        }

        public async Task UpdateTicketStatusAsync(Guid ticketId, string newStatus, decimal? price = null)
        {
            var ticket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(ticketId);
            if (ticket != null)
            {
                ticket.Status = newStatus;
                ticket.UpdatedDate = DateTime.Now;
                if (price.HasValue) ticket.TotalPrice = price.Value;
                _unitOfWork.Repository<ServiceTicket>().Update(ticket);
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task DeleteTicketAsync(Guid id)
        {
            var ticket = await _unitOfWork.Repository<ServiceTicket>().GetByIdAsync(id);
            if (ticket != null)
            {
                ticket.IsDeleted = true;
                ticket.UpdatedDate = DateTime.Now;
                ticket.Status = "İptal";
                _unitOfWork.Repository<ServiceTicket>().Update(ticket);
                await _unitOfWork.CommitAsync();
            }
        }

        public async Task<List<TechnicianPerformanceDto>> GetTechnicianPerformanceStatsAsync(DateTime startDate, DateTime endDate, Guid? branchId = null)
        {
            var usersQuery = _userManager.Users.AsQueryable();
            if (branchId.HasValue) usersQuery = usersQuery.Where(u => u.BranchId == branchId.Value);
            var technicians = usersQuery.ToList();

            var tickets = await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(t => t.CreatedDate >= startDate && t.CreatedDate <= endDate && t.TechnicianId != null);

            var ticketList = tickets.ToList();
            var statsList = new List<TechnicianPerformanceDto>();

            foreach (var tech in technicians)
            {
                var techTickets = ticketList.Where(t => t.TechnicianId == tech.Id).ToList();
                if (!techTickets.Any()) continue;

                statsList.Add(new TechnicianPerformanceDto
                {
                    TechnicianId = tech.Id,
                    FullName = tech.FullName ?? tech.UserName,
                    TotalAssignedTickets = techTickets.Count,
                    CompletedTickets = techTickets.Count(t => t.Status == "Tamamlandı" || t.Status == "Teslim Edildi"),
                    PendingTickets = techTickets.Count(t => t.Status == "Bekliyor" || t.Status == "Parça Bekliyor" || t.Status == "İşlemde" || t.Status == "Teknisyen Onayı Bekliyor"),
                    RefundedOrCancelledTickets = techTickets.Count(t => t.Status == "İptal" || t.Status == "İade"),
                    TotalRevenue = techTickets.Where(t => (t.Status == "Tamamlandı" || t.Status == "Teslim Edildi") && t.TotalPrice.HasValue).Sum(t => t.TotalPrice.Value),
                    PotentialRevenue = techTickets.Where(t => (t.Status == "Bekliyor" || t.Status == "İşlemde") && t.TotalPrice.HasValue).Sum(t => t.TotalPrice.Value)
                });
            }
            return statsList.OrderByDescending(x => x.TotalRevenue).ToList();
        }
    }
}