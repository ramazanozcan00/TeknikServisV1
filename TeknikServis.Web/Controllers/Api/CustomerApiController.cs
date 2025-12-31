using Microsoft.AspNetCore.Mvc;
using TeknikServis.Core.Entities;
using TeknikServis.Core.Interfaces;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;

namespace TeknikServis.Web.Controllers.Api
{
    [Route("api/Customer")]
    [ApiController]
    public class CustomerApiController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public CustomerApiController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // --- 1. FİRMA LİSTESİ (SİLİNENLER HARİÇ) ---
        [HttpGet("GetCompanies")]
        public async Task<IActionResult> GetCompanies([FromQuery] Guid? branchId)
        {
            try
            {
                IEnumerable<Customer> customers;

                if (branchId.HasValue && branchId.Value != Guid.Empty)
                {
                    // Şube ID VARSA: Şube + Firma Adı Dolu + Silinmemiş
                    customers = await _unitOfWork.Repository<Customer>()
                        .FindAsync(x => x.BranchId == branchId.Value
                                     && !string.IsNullOrEmpty(x.CompanyName)
                                     && !x.IsDeleted); // <-- SİLİNENLERİ ENGELLE
                }
                else
                {
                    // Şube ID YOKSA: Firma Adı Dolu + Silinmemiş
                    customers = await _unitOfWork.Repository<Customer>()
                        .FindAsync(x => !string.IsNullOrEmpty(x.CompanyName)
                                     && !x.IsDeleted); // <-- SİLİNENLERİ ENGELLE
                }

                var companyNames = customers
                    .Select(c => c.CompanyName)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                return Ok(companyNames);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Firma listesi çekilemedi: " + ex.Message);
            }
        }

        // --- 2. MÜŞTERİ ARAMA LİSTESİ (SİLİNENLER HARİÇ) ---
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll([FromQuery] Guid? branchId)
        {
            try
            {
                IEnumerable<Customer> customers;

                if (branchId.HasValue && branchId.Value != Guid.Empty)
                {
                    // Şube ID VARSA: Şube + Silinmemiş
                    customers = await _unitOfWork.Repository<Customer>()
                        .FindAsync(c => c.BranchId == branchId.Value && !c.IsDeleted); // <-- SİLİNENLERİ ENGELLE
                }
                else
                {
                    // Şube ID YOKSA: Sadece Silinmemiş Olanlar
                    customers = await _unitOfWork.Repository<Customer>()
                        .FindAsync(c => !c.IsDeleted); // <-- SİLİNENLERİ ENGELLE
                }

                var list = customers.Select(c => new
                {
                    Id = c.Id,
                    Text = $"{c.FirstName} {c.LastName} - {c.Phone}" +
                           (!string.IsNullOrEmpty(c.CompanyName) ? $" ({c.CompanyName})" : "")
                })
                .OrderBy(x => x.Text)
                .ToList();

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Liste çekilemedi: " + ex.Message);
            }
        }

        // --- 3. CREATE METODU (DEĞİŞİKLİK YOK) ---
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] CustomerDto model)
        {
            if (model == null) return BadRequest("Veri gönderilmedi.");
            if (string.IsNullOrWhiteSpace(model.FirstName) || string.IsNullOrWhiteSpace(model.Phone))
                return BadRequest("Ad ve Telefon alanları zorunludur.");

            try
            {
                Guid targetBranchId = model.BranchId;
                if (targetBranchId == Guid.Empty)
                {
                    var allBranches = await _unitOfWork.Repository<Branch>().GetAllAsync();
                    var defaultBranch = allBranches.FirstOrDefault();
                    if (defaultBranch == null) return BadRequest("Şube bulunamadı.");
                    targetBranchId = defaultBranch.Id;
                }

                // Silinmemişler arasında telefon kontrolü yap
                var checkPhone = await _unitOfWork.Repository<Customer>()
                    .FindAsync(x => x.Phone == model.Phone && !x.IsDeleted);

                if (checkPhone.Any())
                    return BadRequest($"Bu telefon numarası ({model.Phone}) zaten kayıtlı.");

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
                    CustomerType = !string.IsNullOrEmpty(model.CustomerType) ? model.CustomerType : "Normal",
                    BranchId = targetBranchId
                };

                await _unitOfWork.Repository<Customer>().AddAsync(customer);
                await _unitOfWork.CommitAsync();

                return Ok(new { Message = "Başarılı", Id = customer.Id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
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