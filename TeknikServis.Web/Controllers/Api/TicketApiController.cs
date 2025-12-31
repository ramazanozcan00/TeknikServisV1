using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;

namespace TeknikServis.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<AppUser> _userManager;

        public TicketApiController(IUnitOfWork unitOfWork, UserManager<AppUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        // --- 1. FORM VERİLERİNİ GETİREN METOD (Şube + Rol Filtreli) ---
        [HttpGet("FormData")]
        public async Task<IActionResult> GetTicketFormData([FromQuery] Guid? branchId)
        {
            try
            {
                // Cihaz Türleri
                var types = (await _unitOfWork.Repository<DeviceType>().GetAllAsync())
                            .Select(x => new { x.Id, x.Name }).OrderBy(x => x.Name).ToList();

                // Markalar
                var brands = (await _unitOfWork.Repository<DeviceBrand>().GetAllAsync())
                             .Select(x => new { x.Id, x.Name }).OrderBy(x => x.Name).ToList();

                // --- GÜNCELLENEN KISIM: SADECE TEKNİSYENLERİ GETİR ---

                // 1. Adım: Önce "Technician" rolündeki tüm kullanıcıları çek
                // (Not: Rol isminiz 'Technician' ise bunu kullanın, 'Personel' ise onu yazın)
                var usersInRole = await _userManager.GetUsersInRoleAsync("Technician");

                // 2. Adım: Şube ID geldiyse, sadece o şubedeki teknisyenleri filtrele
                if (branchId.HasValue && branchId.Value != Guid.Empty)
                {
                    usersInRole = usersInRole.Where(u => u.BranchId == branchId.Value).ToList();
                }

                // 3. Adım: Listeyi formata uygun hale getir
                var technicians = usersInRole
                                  .Select(x => new
                                  {
                                      Id = x.Id,
                                      // İsim varsa getir, yoksa Kullanıcı Adını getir
                                      Name = x.FullName ?? x.UserName
                                  })
                                  .OrderBy(x => x.Name)
                                  .ToList();

                return Ok(new { Types = types, Brands = brands, Technicians = technicians });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Veriler çekilemedi: " + ex.Message);
            }
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] ServiceTicketDto model)
        {
            if (model == null) return BadRequest("Veri yok.");

            try
            {
                string newFisNo = "SRV-" + DateTime.Now.ToString("yyyyMMdd-HHmm");

                var ticket = new ServiceTicket
                {
                    FisNo = newFisNo,
                    CustomerId = model.CustomerId,

                    // --- KRİTİK EKLEME: ŞUBE ID KAYDEDİLİYOR ---
                    BranchId = model.BranchId,

                    DeviceTypeId = model.DeviceTypeId,
                    DeviceBrandId = model.DeviceBrandId,
                    TechnicianId = model.TechnicianId,

                    DeviceModel = model.DeviceModel ?? "",
                    SerialNumber = model.SerialNo ?? "",
                    ProblemDescription = model.Problem ?? "",
                    Accessories = model.Accessories,
                    PhysicalDamage = model.PhysicalDamage,
                    IsWarranty = model.IsWarranty,

                    Status = model.TechnicianId != null ? "İşlemde" : "Bekliyor",
                    TechnicianStatus = model.TechnicianId != null ? "Atandı" : "Atanmadı",
                    CreatedDate = DateTime.Now
                };

                await _unitOfWork.Repository<ServiceTicket>().AddAsync(ticket);
                await _unitOfWork.CommitAsync();

                return Ok(new { Message = "Kayıt Başarılı", FisNo = newFisNo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Hata: " + ex.Message);
            }
        }

        // --- BU METODU EKLEYİN: ŞİRKET LİSTESİ ---
        [HttpGet("GetCompanies")]
        public async Task<IActionResult> GetCompanies()
        {
            try
            {
                // Tüm müşterileri çek
                var customers = await _unitOfWork.Repository<Customer>().GetAllAsync();

                // Sadece Şirket Adı dolu olanları al, tekrarlayanları sil (Distinct), sırala ve gönder
                var companies = customers
                    .Where(c => !string.IsNullOrEmpty(c.CompanyName))
                    .Select(c => c.CompanyName)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                return Ok(companies);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Şirket listesi çekilemedi: " + ex.Message);
            }
        }
    }

    // DTO Sınıfı
    public class ServiceTicketDto
    {
        public Guid BranchId { get; set; }
        public Guid CustomerId { get; set; }
        public Guid DeviceTypeId { get; set; }
        public Guid DeviceBrandId { get; set; }
        public Guid? TechnicianId { get; set; }

        public string DeviceModel { get; set; }
        public string SerialNo { get; set; }
        public string Problem { get; set; }
        public string Accessories { get; set; }
        public string PhysicalDamage { get; set; }
        public bool IsWarranty { get; set; }
    }
}