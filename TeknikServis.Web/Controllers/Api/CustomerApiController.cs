using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;

namespace TeknikServis.Web.Controllers.Api
{
    // ESKİ HALİ: [Route("api/[controller]")] -> Bu "api/CustomerApi" üretiyordu.
    // YENİ HALİ: Aşağıdaki gibi sabitledik.
    [Route("api/Customer")]
    [ApiController]
    public class CustomerApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public CustomerApiController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [HttpGet("FormData")]
        // Eğer giriş yapmadan (Token göndermeden) erişim hatası alıyorsanız bu satırı ekleyin:
        // [Microsoft.AspNetCore.Authorization.AllowAnonymous] 
        public async Task<IActionResult> GetFormData()
        {
            // ESKİ HATALI KOD:
            // var companies = await _unitOfWork.Repository<CompanySetting>()...

            // YENİ DOĞRU KOD (Müşteriler tablosundan firma isimlerini çeker):
            var customers = await _unitOfWork.Repository<Customer>()
                .FindAsync(x => !string.IsNullOrEmpty(x.CompanyName));

            var companyNames = customers
                .Select(c => c.CompanyName)
                .Distinct() // Aynı isimleri teke düşürür
                .OrderBy(n => n)
                .ToList();

            return Ok(new { Companies = companyNames });
        }
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] CustomerDto model)
        {
            if (model == null) return BadRequest("Veri gönderilmedi.");

            if (string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.Phone))
                return BadRequest("Ad ve Telefon alanları zorunludur.");

            try
            {
                // 1. Şube Kontrolü (Aynı kalıyor)
                Guid targetBranchId = model.BranchId;
                if (targetBranchId == Guid.Empty)
                {
                    var defaultBranch = (await _unitOfWork.Repository<Branch>().GetAllAsync()).FirstOrDefault();
                    if (defaultBranch == null) return BadRequest("Şube bulunamadı.");
                    targetBranchId = defaultBranch.Id;
                }

                // 2. Mükerrer Kayıt Kontrolü (Aynı kalıyor)
                var existingCustomer = (await _unitOfWork.Repository<Customer>()
                    .FindAsync(x => x.Phone == model.Phone)).FirstOrDefault();

                if (existingCustomer != null)
                    return BadRequest($"Bu telefon numarası ({model.Phone}) zaten kayıtlı.");

                // --- DÜZELTİLEN KISIM: OTOMATİK TİP DEĞİŞTİRME İPTAL EDİLDİ ---

                // Mobilden ne geliyorsa onu kullan. Boşsa "Normal" yap.
                // Firma adı girilse bile "Normal" ise "Normal" kalır.
                string finalCustomerType = !string.IsNullOrEmpty(model.CustomerType)
                                           ? model.CustomerType
                                           : "Normal";

                var customer = new Customer
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName ?? "",
                    Phone = model.Phone,
                    Phone2 = model.Phone2,
                    Email = model.Email ?? "",
                    Address = model.Address,
                    City = model.City,
                    District = model.District,
                    TCNo = model.TCNo,
                    CompanyName = model.CompanyName,
                    TaxOffice = model.TaxOffice,
                    TaxNumber = model.TaxNumber,

                    CustomerType = finalCustomerType, // Müdahale etmeden kaydediyoruz

                    BranchId = targetBranchId
                };

                await _unitOfWork.Repository<Customer>().AddAsync(customer);
                await _unitOfWork.CommitAsync();

                return Ok(new { Message = "Müşteri başarıyla kaydedildi.", Id = customer.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Sunucu hatası: {ex.Message}");
            }
        }
        // --- BU METODU EKLEYİN ---
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var customers = await _unitOfWork.Repository<Customer>().GetAllAsync();

                // Mobilde Dropdown içinde göstermek için sadeleştiriyoruz
                var list = customers.Select(c => new
                {
                    Id = c.Id,
                    Text = $"{c.FirstName} {c.LastName} - {c.Phone}" // Görünecek Metin
                })
                .OrderBy(x => x.Text) // İsme göre sırala
                .ToList();

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Liste çekilemedi: " + ex.Message);
            }
        }


    }

    public class CustomerDto
    {
        public Guid BranchId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Phone2 { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string District { get; set; }
        public string TCNo { get; set; }
        public string CompanyName { get; set; }
        public string CustomerType { get; set; }
        public string TaxOffice { get; set; }
        public string TaxNumber { get; set; }
    }
}