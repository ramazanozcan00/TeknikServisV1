using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using TeknikServis.Web.Extensions;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;

namespace TeknikServis.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public TicketApiController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // 1. ŞUBEYE GÖRE SON KAYITLARI GETİR (Yeni)
        [HttpGet("List/{branchId}")]
        public async Task<IActionResult> GetList(Guid branchId)
        {
            // Sadece gönderilen şube ID'sine ait kayıtları çek
            var tickets = (await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.Customer.BranchId == branchId, // Şube Filtresi
                           inc => inc.Customer,
                           inc => inc.DeviceBrand))
                .OrderByDescending(x => x.CreatedDate)
                .Take(20) // Son 20 kayıt
                .Select(t => new
                {
                    FisNo = t.FisNo,
                    Musteri = t.Customer.FirstName + " " + t.Customer.LastName,
                    Cihaz = (t.DeviceBrand != null ? t.DeviceBrand.Name : "") + " " + t.DeviceModel,
                    Durum = t.Status,
                    Tarih = t.CreatedDate.ToString("dd.MM HH:mm")
                })
                .ToList();

            return Ok(tickets);
        }

        // 2. SORGULAMA (Aynı kalıyor)
        [HttpGet("CheckStatus")]
        public async Task<IActionResult> CheckStatus(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest("Kod girilmedi.");
            q = q.Trim();

            var ticket = (await _unitOfWork.Repository<ServiceTicket>()
                .FindAsync(x => x.FisNo == q || x.SerialNumber == q,
                           inc => inc.DeviceBrand)).FirstOrDefault();

            if (ticket == null) return NotFound("Kayıt bulunamadı.");

            return Ok(new
            {
                FisNo = ticket.FisNo,
                Cihaz = $"{(ticket.DeviceBrand?.Name ?? "")} {ticket.DeviceModel}",
                Durum = ticket.Status,
                Ariza = ticket.ProblemDescription,
                GirisTarihi = ticket.CreatedDate.ToString("dd.MM.yyyy"),
                Ucret = ticket.TotalPrice.HasValue ? ticket.TotalPrice.Value.ToString("C2") : "-"
            });
        }

        // 3. YENİ KAYIT (Şube ID alacak şekilde güncellendi)
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] TicketCreateDto model)
        {
            if (model == null || string.IsNullOrEmpty(model.Phone)) return BadRequest("Eksik bilgi.");

            // Gelen BranchId boşsa hata ver (Güvenlik)
            if (model.BranchId == Guid.Empty) return BadRequest("Şube bilgisi eksik.");

            var customer = (await _unitOfWork.Repository<Customer>()
                .FindAsync(x => x.Phone == model.Phone || x.Phone2 == model.Phone)).FirstOrDefault();

            if (customer == null)
            {
                customer = new Customer
                {
                    FirstName = model.Name,
                    LastName = "",
                    Phone = model.Phone,
                    Email = "mobil@musteri.com",
                    Address = "Mobil Kayıt",
                    CustomerType = "Normal",
                    BranchId = model.BranchId, // Giriş yapan kullanıcının şubesi
                    City = "-",
                    District = "-"
                };
                await _unitOfWork.Repository<Customer>().AddAsync(customer);
                await _unitOfWork.CommitAsync();
            }

            // Marka/Model kontrolü
            var defaultType = (await _unitOfWork.Repository<DeviceType>().GetAllAsync()).FirstOrDefault();
            var defaultBrand = (await _unitOfWork.Repository<DeviceBrand>().GetAllAsync()).FirstOrDefault();

            // Eğer veritabanı boşsa hata vermemesi için sanal ID üret (Gerçek senaryoda bu tablolar dolu olmalı)
            var typeId = defaultType?.Id ?? Guid.Empty;
            var brandId = defaultBrand?.Id ?? Guid.Empty;

            string fisNo = "MOB-" + new Random().Next(10000, 99999).ToString();

            var ticket = new ServiceTicket
            {
                CustomerId = customer.Id,
                FisNo = fisNo,
                DeviceModel = model.DeviceModel,
                ProblemDescription = model.Problem,
                Status = "Yeni Kayıt",
                CreatedDate = DateTime.Now,
                SerialNumber = "SN" + new Random().Next(100, 999),
                DeviceBrandId = brandId,
                DeviceTypeId = typeId,
                Accessories = "",
                PhysicalDamage = "",
                PdfPath = "",
                PhotoPath = "",
                TechnicianNotes = ""
            };

            await _unitOfWork.Repository<ServiceTicket>().AddAsync(ticket);
            await _unitOfWork.CommitAsync();

            return Ok(new { Message = "Kaydınız alındı!", FisNo = fisNo });
        }
    }

    public class TicketCreateDto
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public string DeviceModel { get; set; }
        public string Problem { get; set; }
        public Guid BranchId { get; set; } // Şube ID Eklendi
    }
}