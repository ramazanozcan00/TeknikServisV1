using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;

namespace TeknikServis.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public CustomerApiController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // --- GÜNCELLENEN AKILLI METOD ---
        [HttpGet("FormData")]
        public async Task<IActionResult> GetFormData()
        {
            // 1. CompanySetting Tablosundan Firmaları Çek
            var companies = await _unitOfWork.Repository<CompanySetting>()
                .FindAsync(x => !string.IsNullOrEmpty(x.CompanyName));

            var companyNames = companies
                .Select(c => c.CompanyName)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            // 2. Eğer veritabanı boşsa, test amaçlı bunları ekle (Listeyi dolu görmek için)
            if (!companyNames.Any())
            {
                companyNames.Add("Örnek Firma A.Ş.");
                companyNames.Add("Deneme Şirketi Ltd.");
            }

            // 3. Listeyi Gönder
            return Ok(new
            {
                Companies = companyNames
            });
        }

        // --- KAYIT METODU (AYNI KALACAK) ---
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] CustomerDto model)
        {
            if (model == null) return BadRequest("Veri gelmedi.");

            // Eğer mobilden şube ID gelmezse (0000...) varsayılan bir şube bulalım
            Guid targetBranchId = model.BranchId;
            if (targetBranchId == Guid.Empty)
            {
                var defaultBranch = (await _unitOfWork.Repository<Branch>().GetAllAsync()).FirstOrDefault();
                targetBranchId = defaultBranch?.Id ?? Guid.Empty;
            }

            var exists = (await _unitOfWork.Repository<Customer>()
                .FindAsync(x => x.Phone == model.Phone)).Any(); // Şube bağımsız telefon kontrolü daha güvenli

            if (exists) return BadRequest("Bu telefon numarası zaten kayıtlı.");

            var customer = new Customer
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Phone = model.Phone,
                Email = model.Email ?? "",
                Address = model.Address,
                City = model.City,
                District = model.District,
                TCNo = model.TCNo,
                CompanyName = model.CompanyName,
                TaxOffice = model.TaxOffice,
                TaxNumber = model.TaxNumber,
                CustomerType = !string.IsNullOrEmpty(model.CustomerType) ? model.CustomerType : "Normal",
                BranchId = targetBranchId, // Güvenli ID
                Phone2 = model.Phone2
            };

            await _unitOfWork.Repository<Customer>().AddAsync(customer);
            await _unitOfWork.CommitAsync();

            return Ok(new { Message = "Müşteri başarıyla kaydedildi.", Id = customer.Id });
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