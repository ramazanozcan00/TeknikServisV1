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
        public async Task<IActionResult> GetFormData()
        {
            var companies = await _unitOfWork.Repository<CompanySetting>()
                .FindAsync(x => !string.IsNullOrEmpty(x.CompanyName));

            var companyNames = companies
                .Select(c => c.CompanyName)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            if (!companyNames.Any())
            {
                companyNames.Add("Bireysel");
                companyNames.Add("Kurumsal (Diğer)");
            }

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
                // Şube ID boş ise varsayılan şubeyi bul
                Guid targetBranchId = model.BranchId;
                if (targetBranchId == Guid.Empty)
                {
                    var defaultBranch = (await _unitOfWork.Repository<Branch>().GetAllAsync()).FirstOrDefault();

                    if (defaultBranch == null)
                        return BadRequest("Sistemde kayıtlı şube bulunamadı. Web panelinden şube ekleyiniz.");

                    targetBranchId = defaultBranch.Id;
                }

                // Mükerrer kayıt kontrolü
                var existingCustomer = (await _unitOfWork.Repository<Customer>()
                    .FindAsync(x => x.Phone == model.Phone)).FirstOrDefault();

                if (existingCustomer != null)
                    return BadRequest($"Bu telefon numarası ({model.Phone}) zaten kayıtlı.");

                var customer = new Customer
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName ?? "",
                    Phone = model.Phone,
                    Email = model.Email ?? "",
                    Address = model.Address,
                    City = model.City,
                    District = model.District,
                    TCNo = model.TCNo,
                    CompanyName = model.CompanyName,
                    TaxOffice = model.TaxOffice,
                    TaxNumber = model.TaxNumber,
                    CustomerType = !string.IsNullOrEmpty(model.CompanyName) ? "Kurumsal" : "Normal",
                    BranchId = targetBranchId,
                    Phone2 = model.Phone2
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